using System.Globalization;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.Services;

/// <summary>
/// Resultado de salvar um aviso. <paramref name="FicouUrgente"/>: urgente novo, ou "Ainda falta" que subiu para urgente —
/// a tela abre <paramref name="Link"/> no WhatsApp com <paramref name="Mensagem"/> para a dona (RN-AV-06).
/// </summary>
public sealed record AvisoSalvo(AvisoLocal Linha, bool JaExistia, bool FicouUrgente, string? Mensagem, string? Link);

/// <summary>
/// "Está acabando" no aparelho (Fase 2, docs/10 §2.2–2.3): avisar item do catálogo ou "Outro", "Ainda falta" sem duplicar,
/// WhatsApp aberto e o resumo do dia no fechamento do caixa. As regras vêm do <see cref="Aviso"/> do Domain (o mesmo da API);
/// grava no SQLite com o Id gerado aqui e põe na fila — funciona sem internet. Erros de regra saem como <see cref="DomainException"/>.
/// </summary>
public sealed class AvisoAppService(
    AvisoRepository avisos,
    ItemEstoqueRepository itens,
    EstoqueService estoque,
    SessaoUsuario sessao,
    SyncService sync,
    ApiCliente api)
{
    private const string ChaveResumoPendente = "resumo_dia_pendente"; // data local (yyyy-MM-dd) do resumo que não saiu

    // Um aviso por vez: dois toques rápidos não podem criar dois avisos abertos para o mesmo item (RN-AV-03).
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    /// <summary>
    /// RN-AV-01/02/03: aviso de item do catálogo. Se o item já tem aviso aberto, vira "Ainda falta" nele (sem duplicar).
    /// <paramref name="substituiRecusadoId"/>: o aviso recusado que o atendente escolheu "Avisar de novo" (sai da lista).
    /// </summary>
    public Task<AvisoSalvo> AvisarItemAsync(Guid itemEstoqueId, int quantidade, PrioridadeAviso prioridade, string? observacao, Guid? substituiRecusadoId = null) =>
        UmaPorVezAsync(async () =>
        {
            var usuarioId = UsuarioId();
            var item = await itens.ObterAsync(itemEstoqueId)
                ?? throw new DomainException("Este item não está no tablet. Toque em Atualizar ou use \"Outro\".");

            if (await avisos.ObterAbertoDoItemAsync(item.Id) is { } aberto)
            {
                return await AindaFaltaSemTravaAsync(aberto.Id, quantidade, prioridade, observacao, usuarioId, substituiRecusadoId);
            }

            var aviso = Aviso.DoItem(item.Id, quantidade, prioridade, observacao, usuarioId, DateTime.UtcNow);
            return await GravarNovoAsync(aviso, item.Descricao, item.GrupoNome, substituiRecusadoId);
        });

    /// <summary>RN-AV-01: "Outro" — texto livre (não deduplica, RN-AV-03).</summary>
    public Task<AvisoSalvo> AvisarOutroAsync(string texto, int quantidade, PrioridadeAviso prioridade, string? observacao, Guid? substituiRecusadoId = null) =>
        UmaPorVezAsync(async () =>
        {
            var aviso = Aviso.DeTextoLivre(texto, quantidade, prioridade, observacao, UsuarioId(), DateTime.UtcNow);
            return await GravarNovoAsync(aviso, aviso.TextoLivre ?? texto.Trim(), grupoNome: null, substituiRecusadoId);
        });

    /// <summary>
    /// RN-AV-03 "Ainda falta": atualiza quantidade/observação; a prioridade só sobe (regra do Domain).
    /// <paramref name="substituiRecusadoId"/>: veio de "Avisar de novo" num recusado do mesmo item (sai da lista junto).
    /// </summary>
    public Task<AvisoSalvo> AindaFaltaAsync(Guid avisoId, int quantidade, PrioridadeAviso prioridade, string? observacao, Guid? substituiRecusadoId = null) =>
        UmaPorVezAsync(() => AindaFaltaSemTravaAsync(avisoId, quantidade, prioridade, observacao, UsuarioId(), substituiRecusadoId));

    /// <summary>RN-AV-08: o link do WhatsApp foi aberto (nunca "entregue"). Grava só a primeira vez e põe na fila.</summary>
    public Task MarcarWhatsAppAbertoAsync(Guid avisoId) =>
        UmaPorVezAsync(async () =>
        {
            if (await avisos.ObterAsync(avisoId) is null)
            {
                return true; // já não está no tablet (fechado e trocado pela lista do servidor): nada a marcar
            }

            await avisos.AlterarAsync(avisoId, aviso => aviso.WhatsAppAbertoEm ??= DateTime.UtcNow);
            sync.Disparar();
            return true;
        });

    /// <summary>Aviso recusado que o atendente já viu: sai da lista do tablet (o motivo continua gravado).</summary>
    public Task DispensarRecusadoAsync(Guid avisoId) => avisos.DispensarRecusadoAsync(avisoId);

    // ---------------------------------------------------------------- resumo do dia (RN-AV-07)

    /// <summary>
    /// Envia a fila primeiro (o resumo conta só as vendas que já chegaram ao servidor) e pede o resumo de hoje, com o texto
    /// e o link para o WhatsApp da dona. Sem internet, servidor dormindo ou sessão vencida: <see cref="ApiException"/>.
    /// </summary>
    public async Task<ResumoDoDiaResponse> ResumoDoDiaAsync()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            throw new ApiIndisponivelException("Sem internet: o resumo precisa das vendas do dia no servidor.");
        }

        if (!await sync.SincronizarAgoraAsync())
        {
            var estado = await sync.EstadoAsync();
            if (estado.PrecisaPin)
            {
                throw new SessaoExpiradaException();
            }

            throw new ApiIndisponivelException(string.IsNullOrWhiteSpace(estado.UltimaMensagem)
                ? "Não deu para enviar as vendas agora. Tente de novo em 1 minuto."
                : estado.UltimaMensagem);
        }

        return await api.ObterResumoDoDiaAsync();
    }

    /// <summary>O resumo de hoje ainda não saiu (falhou no fechamento): a aba Caixa mostra o lembrete.</summary>
    public bool ResumoPendenteHoje => Preferences.Default.Get(ChaveResumoPendente, string.Empty) == Hoje();

    public void MarcarResumoPendente() => Preferences.Default.Set(ChaveResumoPendente, Hoje());

    public void LimparResumoPendente() => Preferences.Default.Remove(ChaveResumoPendente);

    // ---------------------------------------------------------------- apoio

    private async Task<AvisoSalvo> GravarNovoAsync(Aviso aviso, string descricao, string? grupoNome, Guid? substituiRecusadoId)
    {
        var linha = new AvisoLocal
        {
            Id = aviso.Id,
            ItemEstoqueId = aviso.ItemEstoqueId,
            TextoLivre = aviso.TextoLivre,
            Descricao = descricao,
            GrupoNome = grupoNome,
            QuantidadeAtual = aviso.QuantidadeAtual,
            Prioridade = aviso.Prioridade,
            Observacao = aviso.Observacao,
            CriadoPorUsuarioId = aviso.CriadoPorUsuarioId,
            CriadoPorNome = NomeAtendente(),
            CriadoEm = aviso.CriadoEm,
            AtualizadoPorUsuarioId = aviso.AtualizadoPorUsuarioId,
            AtualizadoEm = aviso.AtualizadoEm,
            Status = StatusAviso.Aberto,
            PendenteEnvio = true,
            Versao = 1,
        };

        await avisos.InserirAsync(linha, substituiRecusadoId);
        sync.Disparar(); // sobe em seguida; sem internet fica na fila
        return Resultado(linha, jaExistia: false, ficouUrgente: linha.Prioridade == PrioridadeAviso.Urgente);
    }

    private async Task<AvisoSalvo> AindaFaltaSemTravaAsync(
        Guid avisoId, int quantidade, PrioridadeAviso prioridade, string? observacao, Guid usuarioId, Guid? substituiRecusadoId)
    {
        var nome = NomeAtendente();
        var virouUrgente = false;
        var linha = await avisos.AlterarAsync(avisoId, aviso =>
        {
            if (aviso.Status != StatusAviso.Aberto)
            {
                throw new DomainException("Este aviso já foi fechado. Faça um aviso novo.");
            }

            if (aviso.EhRecusado)
            {
                throw new DomainException("Este aviso foi recusado pelo sistema. Toque nele na lista e escolha \"Avisar de novo\".");
            }

            // Relógio do tablet atrasado em relação a quem gravou antes: a atualização nunca fica "mais velha" que o aviso
            // (o servidor só aplica a quantidade se a informação for mais nova — RN-AV-09).
            var agora = DateTime.UtcNow;
            if (agora < aviso.AtualizadoEm)
            {
                agora = aviso.AtualizadoEm;
            }

            var dominio = Restaurar(aviso, usuarioId);
            virouUrgente = dominio.AindaFalta(quantidade, prioridade, observacao, usuarioId, agora);

            aviso.QuantidadeAtual = dominio.QuantidadeAtual;
            aviso.Prioridade = dominio.Prioridade;
            aviso.Observacao = dominio.Observacao;
            aviso.AtualizadoPorUsuarioId = usuarioId;
            aviso.AtualizadoPorNome = nome;
            aviso.AtualizadoEm = dominio.AtualizadoEm;
        }, substituiRecusadoId);

        sync.Disparar();
        return Resultado(linha, jaExistia: true, ficouUrgente: virouUrgente);
    }

    /// <summary>Remonta o <see cref="Aviso"/> do Domain com o estado gravado, para aplicar o "Ainda falta" pela mesma regra da API.</summary>
    private static Aviso Restaurar(AvisoLocal linha, Guid usuarioId) =>
        linha.ItemEstoqueId is { } itemId
            ? Aviso.DoItem(itemId, linha.QuantidadeAtual, linha.Prioridade, linha.Observacao, linha.CriadoPorUsuarioId, linha.CriadoEm, linha.Id)
            : Aviso.DeTextoLivre(linha.TextoLivre ?? linha.Descricao, linha.QuantidadeAtual, linha.Prioridade, linha.Observacao,
                linha.CriadoPorUsuarioId ?? usuarioId, linha.CriadoEm, linha.Id);

    /// <summary>RN-AV-06: a mensagem sai do aparelho (sem internet também), com a hora local do tablet.</summary>
    private AvisoSalvo Resultado(AvisoLocal linha, bool jaExistia, bool ficouUrgente)
    {
        if (!ficouUrgente)
        {
            return new AvisoSalvo(linha, jaExistia, FicouUrgente: false, Mensagem: null, Link: null);
        }

        var mensagem = MensagensWhatsApp.Urgente(
            estoque.NomeLoja, linha.Descricao, linha.QuantidadeAtual, linha.Observacao, NomeAtendente(), DateTime.Now);
        return new AvisoSalvo(linha, jaExistia, FicouUrgente: true, mensagem, MensagensWhatsApp.Link(estoque.WhatsAppDona, mensagem));
    }

    private Guid UsuarioId() =>
        sessao.UsuarioId ?? throw new DomainException("Nenhum atendente conectado. Entre com o PIN para avisar.");

    private string NomeAtendente() => string.IsNullOrWhiteSpace(sessao.Nome) ? "Atendente" : sessao.Nome;

    private static string Hoje() => DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

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
