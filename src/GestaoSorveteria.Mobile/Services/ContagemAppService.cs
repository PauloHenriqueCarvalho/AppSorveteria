using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.Services;

/// <summary>Um item na tela da contagem: o item do catálogo, a regra dele no Domain (passo) e o que já foi contado.</summary>
public sealed record ItemParaContar(ItemEstoqueLocal Item, ItemEstoque Regra, decimal? Contado, ContagemAnterior? Anterior);

/// <summary>Tudo que a tela da contagem mostra ao abrir.</summary>
public sealed record TelaContagem(
    ContagemLocal? EmAndamento,
    IReadOnlyList<ItemParaContar> Itens,
    ContagemLocal? UltimaConcluida);

/// <summary>
/// "Hoje é dia de contagem" (RN-ES-05): <paramref name="Mostrar"/> no dia da semana que a API mandou (véspera do pedido),
/// até haver contagem concluída hoje no tablet. <paramref name="EmAndamento"/>: há contagem salva para continuar.
/// </summary>
public sealed record LembreteContagem(bool Mostrar, bool EmAndamento);

/// <summary>
/// Contagem semanal do freezer no tablet (Fase 2, RN-ES-05/06, docs/10 §2.1). As regras vêm do <see cref="Contagem"/> e do
/// <see cref="ItemEstoque"/> do Domain (as mesmas da API): caixa de meia em meia, balde/pote/pacote inteiros, só os grupos
/// Contagem e SaldoPelaVenda, concluir com pelo menos um item contado e, depois, não muda mais. Grava no SQLite com o Id gerado
/// aqui e põe na fila — funciona sem internet. Erros de regra saem como <see cref="DomainException"/> (mensagem pronta).
/// </summary>
public sealed class ContagemAppService(
    ContagemRepository contagens,
    ItemEstoqueRepository itens,
    EstoqueService estoque,
    SessaoUsuario sessao,
    SyncService sync)
{
    /// <summary>
    /// O tablet não conhece o fornecedor (o atendente não vê custo nem fornecedor). O <see cref="ItemEstoque"/> montado aqui
    /// serve só para aplicar as regras de quantidade do Domain; nada dele é gravado nem enviado.
    /// </summary>
    private static readonly Guid FornecedorDesconhecidoNoTablet = new("5f0e2a51-0c0a-4c55-9a3e-7a3b51c0a7e1");

    // Um registro por vez: toques rápidos em + não podem se perder nem criar duas contagens.
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    /// <summary>
    /// A lista na ordem da planilha (grupo → item): itens ativos dos grupos que entram na contagem, mais os que já foram
    /// contados na contagem em andamento (mesmo desativados depois, como no servidor). Campo vazio = ainda não contado.
    /// </summary>
    public async Task<TelaContagem> CarregarAsync()
    {
        var emAndamento = await contagens.ObterEmAndamentoAsync();
        List<ItemContagemLocal> linhas = emAndamento is null ? new() : await contagens.ListarLinhasAsync(emAndamento.Id);
        var contados = linhas.GroupBy(l => l.ItemEstoqueId).ToDictionary(g => g.Key, g => g.First().Embalagens);
        var anteriores = await contagens.ListarAnterioresPorItemAsync();
        var catalogo = await itens.ListarTodosAsync();

        var lista = catalogo
            .Where(i => i.ModoControleGrupo != ModoControle.SoAviso && (i.Ativo || contados.ContainsKey(i.Id)))
            .Select(i => new ItemParaContar(i, Regra(i), contados.GetValueOrDefault(i.Id), anteriores.GetValueOrDefault(i.Id)))
            .ToList();

        return new TelaContagem(emAndamento, lista, await contagens.ObterUltimaConcluidaAsync());
    }

    /// <summary>
    /// − e +: soma (ou tira) um passo do Domain (0,5 caixa; 1 balde/pote/pacote) ao que está gravado. Campo vazio + = 1 passo;
    /// − no campo vazio não muda nada (a tela nem chama). Nunca fica negativo. Devolve a contagem (criada no primeiro item) e o
    /// valor gravado. A conta parte do que está no banco, não da tela: toques rápidos não se perdem.
    /// </summary>
    public Task<(ContagemLocal Contagem, decimal? Contado)> SomarAsync(Guid? contagemId, ItemEstoqueLocal item, int sentido) =>
        RegistrarAsync(contagemId, item, (regra, atual) =>
        {
            var passo = regra.PassoEmbalagens;
            if (atual is not { } valor)
            {
                return sentido > 0 ? passo : null;
            }

            return Math.Max(0m, valor + (sentido > 0 ? passo : -passo));
        });

    /// <summary>Digitado ("2,5"), "Não tem nenhum" (0) ou apagado (<c>null</c> = volta a "não contado", RN-ES-06).</summary>
    public Task<(ContagemLocal Contagem, decimal? Contado)> DefinirAsync(Guid? contagemId, ItemEstoqueLocal item, decimal? embalagens) =>
        RegistrarAsync(contagemId, item, (_, _) => embalagens);

    /// <summary>
    /// Conclui (RN-ES-05): só vale a concluída; depois não muda mais. Item não contado fica "não contado" (não vira 0).
    /// Vai para a fila e o envio começa na hora (sem internet, sobe quando a conexão voltar).
    /// </summary>
    public async Task<ContagemLocal> ConcluirAsync(Guid contagemId)
    {
        var catalogo = (await itens.ListarTodosAsync()).ToDictionary(i => i.Id);
        var concluida = await UmaPorVezAsync(() => contagens.ConcluirAsync(contagemId, (contagem, linhas) =>
        {
            // A mesma conferência da API: remonta a contagem no Domain com o que foi contado e conclui.
            var dominio = Contagem.Iniciar(contagem.UsuarioId, contagem.IniciadaEm, OrigemContagem.App, contagem.Id);
            foreach (var linha in linhas)
            {
                if (!catalogo.TryGetValue(linha.ItemEstoqueId, out var local))
                {
                    throw new DomainException("Um item contado não está mais na lista do tablet. Toque em Atualizar e tente de novo.");
                }

                if (local.ModoControleGrupo == ModoControle.SoAviso)
                {
                    continue; // a dona tirou o grupo da contagem: a linha não vai no envio (ContagemRepository.ListarLinhasParaEnvioAsync)
                }

                dominio.Registrar(Regra(local), Grupo(local), linha.Embalagens, linhaId: linha.Id);
            }

            // Relógio do tablet que voltou para trás não pode concluir "antes" de começar.
            var agora = DateTime.UtcNow;
            dominio.Concluir(agora < contagem.IniciadaEm ? contagem.IniciadaEm : agora);
            return dominio.ConcluidaEm!.Value;
        }));

        sync.Disparar();
        return concluida;
    }

    /// <summary>"Salvar e continuar depois": tudo já está gravado no tablet; aqui só tenta enviar o parcial agora.</summary>
    public void EnviarParcial() => sync.Disparar();

    /// <summary>
    /// Deixa de lado a contagem em andamento (recusada pelo servidor, ou começada em outro dia) para começar uma nova.
    /// Nada é apagado; o que já tinha subido fica no painel como "não concluída" (não vale).
    /// </summary>
    public Task DescartarAsync(Guid contagemId) => UmaPorVezAsync(async () =>
    {
        await contagens.DescartarAsync(contagemId);
        return true;
    });

    /// <summary>RN-ES-05: "Hoje é dia de contagem" no dia da loja (America/Sao_Paulo) que a API mandou, até concluir uma hoje.</summary>
    public async Task<LembreteContagem> LembreteAsync()
    {
        var hoje = DiaDaLoja.DiaDaSemanaHoje().ToString();
        var ehDia = estoque.DiasDeContagem.Any(d => string.Equals(d, hoje, StringComparison.OrdinalIgnoreCase));
        var emAndamento = await contagens.ObterEmAndamentoAsync() is not null;
        var concluidaHoje = ehDia && await contagens.ExisteConcluidaDesdeAsync(DiaDaLoja.InicioDeHojeUtc());
        return new LembreteContagem(ehDia && !concluidaHoje, emAndamento);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<(ContagemLocal Contagem, decimal? Contado)> RegistrarAsync(
        Guid? contagemId, ItemEstoqueLocal item, Func<ItemEstoque, decimal?, decimal?> calcular)
    {
        var usuarioId = sessao.UsuarioId
            ?? throw new DomainException("Nenhum atendente conectado. Entre com o PIN para contar.");
        var nome = string.IsNullOrWhiteSpace(sessao.Nome) ? "Atendente" : sessao.Nome;
        var regra = Regra(item);
        var grupo = Grupo(item);

        var (contagem, linha) = await UmaPorVezAsync(() => contagens.RegistrarAsync(
            contagemId,
            nova: () =>
            {
                // A contagem nasce no primeiro item contado, com o Id e a hora do tablet (RN-SY-02).
                var dominio = Contagem.Iniciar(usuarioId, DateTime.UtcNow, OrigemContagem.App);
                return new ContagemLocal
                {
                    Id = dominio.Id,
                    UsuarioId = dominio.UsuarioId,
                    UsuarioNome = nome,
                    IniciadaEm = dominio.IniciadaEm,
                    AlteradaEm = dominio.IniciadaEm,
                };
            },
            item.Id,
            atual =>
            {
                var novo = calcular(regra, atual);
                // Mesma conferência do servidor (RN-ES-06): grupo que entra na contagem, meia caixa só para caixa e que
                // feche em unidades inteiras, sem negativo. Recusa aqui não grava nada.
                Contagem.Iniciar(usuarioId, DateTime.UtcNow, OrigemContagem.App).Registrar(regra, grupo, novo);
                return novo;
            }));

        return (contagem, linha.Embalagens);
    }

    /// <summary>O item do catálogo do tablet com as regras de quantidade do Domain (passo, meia caixa, máximo).</summary>
    private static ItemEstoque Regra(ItemEstoqueLocal item) =>
        ItemEstoque.Criar(item.GrupoId, FornecedorDesconhecidoNoTablet, item.Nome, item.Unidade, item.UnidadesPorEmbalagem,
            custoEmbalagem: 0m, item.Ordem, id: item.Id);

    private static GrupoEstoque Grupo(ItemEstoqueLocal item) =>
        GrupoEstoque.Criar(item.GrupoNome is { Length: >= 2 } nome ? nome : "Sem grupo", item.ModoControleGrupo, item.GrupoOrdem, item.GrupoId);

    private async Task<T> UmaPorVezAsync<T>(Func<Task<T>> operacao)
    {
        await _umaPorVez.WaitAsync();
        try
        {
            return await operacao();
        }
        finally
        {
            _umaPorVez.Release();
        }
    }
}
