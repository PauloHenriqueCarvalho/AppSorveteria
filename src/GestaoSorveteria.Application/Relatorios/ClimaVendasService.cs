using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Clima;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Clima;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Relatorios;

/// <summary>
/// RN-CL-06: Clima × vendas — cada dia do período com clima, feriado e vendas, médias por faixa da máxima e com/sem chuva
/// (dias úteis × fins de semana e feriados) e a previsão dos próximos 7 dias. É a base da estimativa de vendas futura. Só leitura.
/// </summary>
public sealed class ClimaVendasService
{
    /// <summary>Até um ano por consulta.</summary>
    public const int MaximoDias = 366;

    /// <summary>Sem datas: os últimos 30 dias (hoje incluído).</summary>
    public const int DiasPadrao = 30;

    /// <summary>A partir daqui o dia conta como "com chuva".</summary>
    public const decimal ChuvaMinimaMm = 1m;

    public const int DiasDePrevisao = 7;

    /// <summary>Limites das faixas da máxima (°C); o limite fica na faixa de cima.</summary>
    private static readonly decimal[] LimitesDasFaixas = [20m, 25m, 30m, 35m];

    private readonly IComandaRepository _comandas;
    private readonly IProdutoRepository _produtos;
    private readonly IAvisoRepository _avisos;
    private readonly IClimaRepository _clima;
    private readonly IConfiguracaoLojaRepository _configuracao;
    private readonly IFonteClima _fonte;
    private readonly IClock _clock;

    public ClimaVendasService(
        IComandaRepository comandas,
        IProdutoRepository produtos,
        IAvisoRepository avisos,
        IClimaRepository clima,
        IConfiguracaoLojaRepository configuracao,
        IFonteClima fonte,
        IClock clock)
    {
        _comandas = comandas;
        _produtos = produtos;
        _avisos = avisos;
        _clima = clima;
        _configuracao = configuracao;
        _fonte = fonte;
        _clock = clock;
    }

    public async Task<ClimaVendasDto> ObterAsync(DateOnly? deDia, DateOnly? ateDia, CancellationToken cancellationToken = default)
    {
        var agora = _clock.UtcNow;
        var hoje = DiaComercial.DataDe(agora);
        var ate = ateDia ?? hoje;
        var de = deDia ?? ate.AddDays(-(DiasPadrao - 1));
        PeriodoConsulta.Validar(de, ate, agora, MaximoDias);
        if (ate > hoje)
        {
            throw new DomainException("A data final não pode passar de hoje.");
        }

        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(de, ate);
        var vendas = (await _comandas.ListarFechadasParaLeituraAsync(inicioUtc, fimUtc, cancellationToken))
            .Where(c => c.Status == StatusComanda.Fechada)
            .ToLookup(c => DiaComercial.DataDe(c.FechadaEm!.Value));
        var produtos = (await _produtos.ListarAsync(somenteAtivos: false, cancellationToken)).ToDictionary(p => p.Id);
        var urgentes = (await _avisos.ListarAtualizadosEntreAsync(inicioUtc, fimUtc, cancellationToken))
            .Where(a => a.Prioridade == PrioridadeAviso.Urgente && a.CriadoEm >= inicioUtc && a.CriadoEm < fimUtc)
            .GroupBy(a => DiaComercial.DataDe(a.CriadoEm))
            .ToDictionary(g => g.Key, g => g.Count());

        var configuracao = await _configuracao.ObterAsync(cancellationToken);
        var ponto = configuracao?.PontoClima;
        var climas = ponto is { } p
            ? (await _clima.ListarDiasAsync(p, de, ate, cancellationToken)).ToDictionary(c => c.Data)
            : new Dictionary<DateOnly, ClimaDia>();
        var proximos = ponto is { } q
            ? await _clima.ListarUltimaPrevisaoAsync(q, hoje.AddDays(1), hoje.AddDays(DiasDePrevisao), cancellationToken)
            : [];

        var dias = Enumerable.Range(0, ate.DayNumber - de.DayNumber + 1)
            .Select(i => de.AddDays(i))
            .Select(dia => Dia(dia, hoje, vendas[dia].ToList(), climas.GetValueOrDefault(dia), urgentes.GetValueOrDefault(dia), produtos))
            .ToList();
        var nasMedias = dias.Where(d => d.EntraNasMedias).ToList();

        return new ClimaVendasDto(
            de,
            ate,
            ClimaService.Local(configuracao),
            dias,
            PorTemperatura(nasMedias),
            PorChuva(nasMedias),
            proximos.Select(ParaDto).ToList(),
            dias.Count(d => d.Clima is null),
            _fonte.Atribuicao,
            _fonte.AtribuicaoLink);
    }

    private static DiaClimaVendasDto Dia(
        DateOnly dia,
        DateOnly hoje,
        IReadOnlyList<Comanda> vendas,
        ClimaDia? clima,
        int avisosUrgentes,
        IReadOnlyDictionary<Guid, Produto> produtos)
    {
        var feriado = Feriados.Do(dia);
        var faturamento = Moeda.Arredondar(vendas.Sum(c => c.Total));
        var porCategoria = vendas
            .SelectMany(c => c.Itens)
            .GroupBy(i => CalculoRelatorio.NomeECategoria(i.ProdutoId, produtos, i.Descricao).Categoria)
            .Select(g => new CategoriaNoDiaDto(g.Key, g.Sum(i => i.Quantidade), Moeda.Arredondar(g.Sum(i => i.Subtotal))))
            .OrderByDescending(c => c.Total)
            .ThenBy(c => c.Categoria, StringComparer.CurrentCulture)
            .ToList();

        return new DiaClimaVendasDto(
            dia,
            (int)dia.DayOfWeek,
            feriado?.Nome,
            feriado?.PontoFacultativo ?? false,
            dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || feriado is not null,
            clima is null ? null : ParaDto(clima),
            faturamento,
            vendas.Count,
            CalculoRelatorio.TicketMedio(faturamento, vendas.Count),
            porCategoria,
            avisosUrgentes,
            EntraNasMedias: clima is not null && vendas.Count > 0 && dia < hoje);
    }

    /// <summary>RN-CL-06: faixas da máxima, sempre todas (com 0 dias quando não houve), da mais fria à mais quente.</summary>
    private static IReadOnlyList<FaixaTemperaturaDto> PorTemperatura(IReadOnlyList<DiaClimaVendasDto> dias)
    {
        var faixas = new List<FaixaTemperaturaDto>();
        for (var i = 0; i <= LimitesDasFaixas.Length; i++)
        {
            decimal? de = i == 0 ? null : LimitesDasFaixas[i - 1];
            decimal? ate = i == LimitesDasFaixas.Length ? null : LimitesDasFaixas[i];
            var naFaixa = dias.Where(d => (de is null || d.Clima!.TempMax >= de) && (ate is null || d.Clima!.TempMax < ate)).ToList();
            var nome = (de, ate) switch
            {
                (null, { } a) => $"Abaixo de {a:0} °C",
                ({ } d, null) => $"{d:0} °C ou mais",
                ({ } d, { } a) => $"{d:0} a {a:0} °C",
                _ => "Todas",
            };
            faixas.Add(new FaixaTemperaturaDto(nome, de, ate, Media(naFaixa, util: true), Media(naFaixa, util: false)));
        }

        return faixas;
    }

    /// <summary>RN-CL-06: com chuva (≥ 1 mm) × sem chuva; dia sem a chuva informada fica de fora.</summary>
    private static IReadOnlyList<ChuvaVendasDto> PorChuva(IReadOnlyList<DiaClimaVendasDto> dias) =>
        new[] { false, true }
            .Select(comChuva =>
            {
                var doGrupo = dias.Where(d => d.Clima!.ChuvaMm is { } mm && (mm >= ChuvaMinimaMm) == comChuva).ToList();
                return new ChuvaVendasDto(comChuva ? "Com chuva" : "Sem chuva", comChuva, Media(doGrupo, util: true), Media(doGrupo, util: false));
            })
            .ToList();

    private static MediaDiasDto Media(IEnumerable<DiaClimaVendasDto> dias, bool util)
    {
        var doTipo = dias.Where(d => d.FimDeSemanaOuFeriado != util).ToList();
        if (doTipo.Count == 0)
        {
            return new MediaDiasDto(0, null, null);
        }

        return new MediaDiasDto(
            doTipo.Count,
            Moeda.Arredondar(doTipo.Sum(d => d.Faturamento) / doTipo.Count),
            Math.Round((decimal)doTipo.Sum(d => d.Vendas) / doTipo.Count, 1, MidpointRounding.AwayFromZero));
    }

    internal static ClimaDiaDto ParaDto(ClimaDia c) =>
        new(c.Data, c.TempMax, c.TempMin, c.TempMedia, c.SensacaoMax, c.ChuvaMm, c.ChuvaHorasPct, c.NuvensPct, c.UmidadePct,
            c.Icone, c.Condicao, c.Origem.ToString(), c.Definitivo);

    internal static PrevisaoDiaDto ParaDto(PrevisaoClima p) =>
        new(p.Data, p.FeitaNoDia, p.TempMax, p.TempMin, p.ChuvaMm, p.ChanceChuvaPct, p.Icone, p.Condicao);
}
