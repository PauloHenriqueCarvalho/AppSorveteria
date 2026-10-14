using System.Diagnostics;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;

namespace SorveteriaMaui.Services.Api;

/// <summary>
/// Fase 2 vinda da API para o tablet: catálogo de estoque sem custo (<c>GET /api/estoque/itens</c>), nome da loja e
/// WhatsApp da dona (<c>GET /api/configuracoes</c>) e os avisos abertos (<c>GET /api/avisos?abertos=true</c>).
/// Baixa ao abrir o app, ao abrir a tela "Está acabando" e no ciclo de 1 min (no máximo a cada 5 min).
/// Sem internet fica o que já está no aparelho; a venda nunca espera isto.
/// </summary>
public sealed class EstoqueService(ApiCliente api, ItemEstoqueRepository itens, AvisoRepository avisos)
{
    private const string ChaveAtualizadoEm = "estoque_atualizado_em_ticks";
    private const string ChaveNomeLoja = "loja_nome";
    private const string ChaveWhatsAppDona = "loja_whatsapp_dona";
    private const string ChaveDiasContagem = "estoque_dias_contagem"; // RN-ES-05: lembrete da contagem (próximo PR)

    private readonly SemaphoreSlim _umaPorVez = new(1, 1);
    private bool _tentouAoAbrir;

    /// <summary>Quando o tablet baixou tudo pela última vez (hora do aparelho); <c>null</c> se nunca baixou.</summary>
    public DateTime? UltimaAtualizacaoUtc
    {
        get
        {
            var ticks = Preferences.Default.Get(ChaveAtualizadoEm, 0L);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>RN-AV-10: nome que aparece nas mensagens ("🔴 URGENTE — Sorveteria").</summary>
    public string NomeLoja => Preferences.Default.Get(ChaveNomeLoja, string.Empty) is { Length: > 0 } nome ? nome : ConfiguracaoLoja.NomePadrao;

    /// <summary>RN-AV-10: só dígitos; <c>null</c> enquanto a dona não cadastrou (o WhatsApp abre para escolher o contato).</summary>
    public string? WhatsAppDona => Preferences.Default.Get(ChaveWhatsAppDona, string.Empty) is { Length: > 0 } numero ? numero : null;

    /// <summary>RN-ES-05: dias ("Sunday"…) em que o app mostra "Hoje é dia de contagem". Guardado para a tela de contagem.</summary>
    public IReadOnlyList<string> DiasDeContagem =>
        Preferences.Default.Get(ChaveDiasContagem, string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Baixa e grava catálogo, configuração e avisos abertos, um por vez. Falhas saem como <see cref="ApiException"/>
    /// (mensagem pronta para a tela); o que já foi gravado antes da falha fica.
    /// </summary>
    public async Task AtualizarAsync(CancellationToken cancellationToken = default)
    {
        await _umaPorVez.WaitAsync(cancellationToken);
        try
        {
            var catalogo = await api.ObterCatalogoEstoqueAsync(cancellationToken);
            await itens.SubstituirTudoAsync(ParaLinhas(catalogo));
            Preferences.Default.Set(ChaveDiasContagem, string.Join(',', catalogo.DiasDeContagem ?? []));

            var configuracao = await api.ObterConfiguracaoLojaAsync(cancellationToken);
            Preferences.Default.Set(ChaveNomeLoja, configuracao.NomeLoja ?? string.Empty);
            Preferences.Default.Set(ChaveWhatsAppDona, configuracao.WhatsAppDona ?? string.Empty);

            // Hora anotada antes de pedir: aviso confirmado pela fila depois disto pode não estar na resposta (não fecha).
            var consultadoEm = DateTime.UtcNow;
            var abertos = await api.ObterAvisosAbertosAsync(cancellationToken);
            await avisos.AplicarAbertosDoServidorAsync(
                abertos.Where(a => a.Status == nameof(StatusAviso.Aberto)).Select(ParaLinha).ToList(), consultadoEm);

            Preferences.Default.Set(ChaveAtualizadoEm, DateTime.UtcNow.Ticks);
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    /// <summary>No ciclo de 1 min do <see cref="SyncService"/>: no máximo a cada <paramref name="idade"/>. Nunca lança (o ciclo não pode parar).</summary>
    public async Task AtualizarSeAntigoAsync(TimeSpan idade)
    {
        var ultima = UltimaAtualizacaoUtc;
        if (ultima is not null && DateTime.UtcNow - ultima < idade)
        {
            return;
        }

        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        try
        {
            await AtualizarAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Estoque/avisos não atualizados no ciclo: {ex.Message}");
        }
    }

    /// <summary>Volta a permitir a atualização automática (chamado quando o app volta do segundo plano).</summary>
    public void PermitirNovaAtualizacaoAoAbrir() => _tentouAoAbrir = false;

    /// <summary>
    /// Ao abrir o app, uma vez por vez e em segundo plano. Fica quieto em qualquer falha: o catálogo de produtos já avisa
    /// sessão vencida, e a tela "Está acabando" mostra a situação da lista quando é aberta.
    /// </summary>
    public async Task AtualizarAoAbrirAsync()
    {
        if (_tentouAoAbrir)
        {
            return;
        }

        _tentouAoAbrir = true;
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        try
        {
            await AtualizarAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Estoque/avisos não atualizados ao abrir: {ex.Message}");
        }
    }

    private static List<ItemEstoqueLocal> ParaLinhas(CatalogoEstoqueAppResponse catalogo)
    {
        var grupos = (catalogo.Grupos ?? []).GroupBy(g => g.Id).ToDictionary(g => g.Key, g => g.First());
        return (catalogo.Itens ?? []).Select(item =>
        {
            var grupo = grupos.GetValueOrDefault(item.GrupoId);
            return new ItemEstoqueLocal
            {
                Id = item.Id,
                GrupoId = item.GrupoId,
                GrupoNome = grupo?.Nome ?? string.Empty,
                GrupoOrdem = grupo?.Ordem ?? int.MaxValue,
                ModoControleGrupo = Enum.TryParse<ModoControle>(grupo?.ModoControle, out var modo) ? modo : ModoControle.Contagem,
                Nome = item.Nome,
                Unidade = Enum.TryParse<UnidadeCompra>(item.Unidade, out var unidade) ? unidade : UnidadeCompra.Unidade,
                UnidadesPorEmbalagem = item.UnidadesPorEmbalagem,
                Ordem = item.Ordem,
                Ativo = item.Ativo,
            };
        }).ToList();
    }

    /// <summary>
    /// Aviso aberto do servidor (painel, outro aparelho ou o próprio tablet). A API não manda o texto livre separado:
    /// sem item do catálogo, a descrição é o próprio texto (é o que o servidor confere no reenvio).
    /// </summary>
    private static AvisoLocal ParaLinha(AvisoDto dto) => new()
    {
        Id = dto.Id,
        ItemEstoqueId = dto.ItemEstoqueId,
        TextoLivre = dto.ItemEstoqueId is null ? dto.Descricao : null,
        Descricao = dto.Descricao,
        GrupoNome = dto.GrupoNome,
        QuantidadeAtual = dto.QuantidadeAtual,
        Prioridade = Enum.TryParse<PrioridadeAviso>(dto.Prioridade, out var prioridade) ? prioridade : PrioridadeAviso.Media,
        Observacao = dto.Observacao,
        CriadoPorNome = dto.CriadoPorNome,
        CriadoEm = dto.CriadoEm,
        AtualizadoPorNome = dto.AtualizadoPorNome,
        AtualizadoEm = dto.AtualizadoEm,
        Status = StatusAviso.Aberto,
        WhatsAppAbertoEm = dto.WhatsAppAbertoEm,
        PendenteEnvio = false,
    };
}
