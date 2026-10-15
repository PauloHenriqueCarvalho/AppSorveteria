using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Relatorios;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Relatorios;

/// <summary>
/// Relatório de um período (Sprint 8, docs/04): faturamento por dia/semana/mês, comparativo com o período anterior,
/// horários e dias de pico e rankings de produtos, categorias e sabores (RN-RL-01/03/08/09/10). Só leitura.
/// </summary>
public sealed class RelatorioPeriodoService
{
    /// <summary>RN-RL-08: até um ano por consulta.</summary>
    public const int MaximoDias = 366;

    /// <summary>RN-RL-08: por dia, até 93 barras.</summary>
    public const int MaximoDiasPorDia = 93;

    private readonly IComandaRepository _comandas;
    private readonly IProdutoRepository _produtos;
    private readonly IEstoqueRepository _estoque;
    private readonly IClock _clock;

    public RelatorioPeriodoService(IComandaRepository comandas, IProdutoRepository produtos, IEstoqueRepository estoque, IClock clock)
    {
        _comandas = comandas;
        _produtos = produtos;
        _estoque = estoque;
        _clock = clock;
    }

    /// <summary>
    /// Sem datas = este mês até hoje. <paramref name="agrupar"/>: "dia", "semana" ou "mes"; sem ele, dia até 31 dias,
    /// semana até 92 e mês acima disso (RN-RL-08).
    /// </summary>
    public async Task<RelatorioPeriodoDto> ObterAsync(DateOnly? deDia, DateOnly? ateDia, string? agrupar, CancellationToken cancellationToken = default)
    {
        var agora = _clock.UtcNow;
        var hoje = DiaComercial.DataDe(agora);
        var ate = ateDia ?? hoje;
        var de = deDia ?? new DateOnly(ate.Year, ate.Month, 1);
        PeriodoConsulta.Validar(de, ate, agora, MaximoDias);
        if (ate > hoje)
        {
            throw new DomainException("A data final não pode passar de hoje.");
        }

        var dias = ate.DayNumber - de.DayNumber + 1;
        var agrupamento = LerAgrupamento(agrupar, dias);

        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(de, ate);
        var vendas = await _comandas.ListarFechadasParaLeituraAsync(inicioUtc, fimUtc, cancellationToken);
        var fechadas = vendas.Where(c => c.Status == StatusComanda.Fechada).ToList();
        var estornadas = vendas.Where(c => c.Status == StatusComanda.Estornada).ToList();
        var total = Moeda.Arredondar(fechadas.Sum(c => c.Total));
        var ticketMedio = CalculoRelatorio.TicketMedio(total, fechadas.Count);

        var anterior = await AnteriorAsync(de, ate, hoje, agora, agrupamento, total, fechadas.Count, ticketMedio, cancellationToken);
        var produtos = (await _produtos.ListarAsync(somenteAtivos: false, cancellationToken)).ToDictionary(p => p.Id);
        var rankingProdutos = RankingProdutos(fechadas, produtos, total);

        return new RelatorioPeriodoDto(
            de,
            ate,
            agrupamento.ToString(),
            total,
            fechadas.Count,
            ticketMedio,
            Moeda.Arredondar(estornadas.Sum(c => c.Total)),
            estornadas.Count,
            CaixaConsultaService.TotaisPorForma(fechadas),
            anterior,
            PorTrecho(fechadas, de, ate, agrupamento),
            CalculoRelatorio.PorHora(fechadas),
            PorDiaDaSemana(fechadas, de, ate),
            rankingProdutos,
            RankingCategorias(rankingProdutos, total),
            await RankingSaboresAsync(fechadas, produtos, cancellationToken));
    }

    /// <summary>Como o período é dividido nas barras (RN-RL-08).</summary>
    public enum Agrupamento
    {
        Dia,
        Semana,
        Mes,
    }

    private static Agrupamento LerAgrupamento(string? agrupar, int dias)
    {
        var agrupamento = agrupar?.Trim().ToLowerInvariant() switch
        {
            null or "" => dias <= 31 ? Agrupamento.Dia : dias <= 92 ? Agrupamento.Semana : Agrupamento.Mes,
            "dia" => Agrupamento.Dia,
            "semana" => Agrupamento.Semana,
            "mes" or "mês" => Agrupamento.Mes,
            _ => throw new DomainException($"Agrupamento inválido: \"{agrupar}\". Use dia, semana ou mes."),
        };
        if (agrupamento == Agrupamento.Dia && dias > MaximoDiasPorDia)
        {
            throw new DomainException($"Por dia, escolha até {MaximoDiasPorDia} dias (ou agrupe por semana ou mês).");
        }

        return agrupamento;
    }

    /// <summary>
    /// RN-RL-08: dias, semanas (segunda a domingo) ou meses do período, recortados nas pontas, em ordem.
    /// </summary>
    public static IReadOnlyList<(DateOnly De, DateOnly Ate)> Trechos(DateOnly de, DateOnly ate, Agrupamento agrupamento)
    {
        var trechos = new List<(DateOnly, DateOnly)>();
        for (var inicio = de; inicio <= ate;)
        {
            var fim = agrupamento switch
            {
                Agrupamento.Semana => inicio.AddDays(6 - ((int)inicio.DayOfWeek + 6) % 7),
                Agrupamento.Mes => new DateOnly(inicio.Year, inicio.Month, DateTime.DaysInMonth(inicio.Year, inicio.Month)),
                _ => inicio,
            };
            if (fim > ate)
            {
                fim = ate;
            }

            trechos.Add((inicio, fim));
            inicio = fim.AddDays(1);
        }

        return trechos;
    }

    /// <summary>
    /// RN-RL-09: período que começa no dia 1 e fica dentro do mês → o mesmo trecho do mês anterior (dia 1 ao mesmo dia,
    /// limitado ao fim daquele mês); qualquer outro → os mesmos N dias logo antes.
    /// </summary>
    public static (DateOnly De, DateOnly Ate) PeriodoAnterior(DateOnly de, DateOnly ate)
    {
        var dias = ate.DayNumber - de.DayNumber + 1;
        if (de.Day == 1 && de.Year == ate.Year && de.Month == ate.Month)
        {
            var inicio = de.AddMonths(-1);
            var ultimoDoMes = new DateOnly(inicio.Year, inicio.Month, DateTime.DaysInMonth(inicio.Year, inicio.Month));
            var fim = inicio.AddDays(dias - 1);
            return (inicio, fim > ultimoDoMes ? ultimoDoMes : fim);
        }

        return (de.AddDays(-dias), de.AddDays(-1));
    }

    private async Task<ComparativoPeriodoDto> AnteriorAsync(
        DateOnly de,
        DateOnly ate,
        DateOnly hoje,
        DateTime agora,
        Agrupamento agrupamento,
        decimal total,
        int quantidade,
        decimal ticketMedio,
        CancellationToken cancellationToken)
    {
        var (deAntes, ateAntes) = PeriodoAnterior(de, ate);
        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(deAntes, ateAntes);
        // Período até hoje: o último dia do anterior conta só até o mesmo horário (como no RN-RL-07).
        var ateMesmoHorario = ate == hoje;
        if (ateMesmoHorario)
        {
            fimUtc = DiaComercial.IntervaloUtc(ateAntes).InicioUtc + (agora - DiaComercial.IntervaloUtc(hoje).InicioUtc);
        }

        var vendas = await _comandas.ListarFechadasParaLeituraAsync(inicioUtc, fimUtc, cancellationToken);
        var fechadas = vendas.Where(c => c.Status == StatusComanda.Fechada).ToList();
        var totalAntes = Moeda.Arredondar(fechadas.Sum(c => c.Total));
        var ticketAntes = CalculoRelatorio.TicketMedio(totalAntes, fechadas.Count);

        return new ComparativoPeriodoDto(
            deAntes,
            ateAntes,
            ateMesmoHorario,
            totalAntes,
            fechadas.Count,
            ticketAntes,
            CalculoRelatorio.Variacao(total, totalAntes),
            CalculoRelatorio.Variacao(quantidade, fechadas.Count),
            CalculoRelatorio.Variacao(ticketMedio, ticketAntes),
            PorTrecho(fechadas, deAntes, ateAntes, agrupamento));
    }

    private static IReadOnlyList<VendasNoTrechoDto> PorTrecho(IReadOnlyList<Comanda> fechadas, DateOnly de, DateOnly ate, Agrupamento agrupamento)
    {
        var porDia = fechadas.ToLookup(c => DiaComercial.DataDe(c.FechadaEm!.Value));
        return Trechos(de, ate, agrupamento)
            .Select(t =>
            {
                var doTrecho = Enumerable.Range(0, t.Ate.DayNumber - t.De.DayNumber + 1).SelectMany(d => porDia[t.De.AddDays(d)]).ToList();
                return new VendasNoTrechoDto(t.De, t.Ate, Moeda.Arredondar(doTrecho.Sum(c => c.Total)), doTrecho.Count);
            })
            .ToList();
    }

    /// <summary>RN-RL-10: segunda a domingo, só os dias da semana que o período tem (mesmo sem venda).</summary>
    private static IReadOnlyList<VendasNoDiaDaSemanaDto> PorDiaDaSemana(IReadOnlyList<Comanda> fechadas, DateOnly de, DateOnly ate)
    {
        var diasNoPeriodo = Enumerable.Range(0, ate.DayNumber - de.DayNumber + 1)
            .Select(d => de.AddDays(d).DayOfWeek)
            .GroupBy(d => d)
            .ToDictionary(g => g.Key, g => g.Count());
        var porDiaDaSemana = fechadas.ToLookup(c => DiaComercial.DataDe(c.FechadaEm!.Value).DayOfWeek);

        return new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday }
            .Where(diasNoPeriodo.ContainsKey)
            .Select(d =>
            {
                var vendas = porDiaDaSemana[d].ToList();
                var totalDoDia = Moeda.Arredondar(vendas.Sum(c => c.Total));
                return new VendasNoDiaDaSemanaDto((int)d, diasNoPeriodo[d], totalDoDia, vendas.Count, Moeda.Arredondar(totalDoDia / diasNoPeriodo[d]));
            })
            .ToList();
    }

    /// <summary>RN-RL-03: todos os produtos vendidos, por faturamento; empate → mais unidades, depois nome.</summary>
    private static IReadOnlyList<ProdutoNoRankingDto> RankingProdutos(
        IReadOnlyList<Comanda> fechadas, IReadOnlyDictionary<Guid, Domain.Produtos.Produto> produtos, decimal total) =>
        fechadas
            .SelectMany(c => c.Itens.Select(i => (Comanda: c, Item: i)))
            .GroupBy(x => x.Item.ProdutoId)
            .Select(g =>
            {
                var (descricao, categoria) = CalculoRelatorio.NomeECategoria(g.Key, produtos, g.MaxBy(x => x.Comanda.FechadaEm).Item.Descricao);
                var totalDoProduto = Moeda.Arredondar(g.Sum(x => x.Item.Subtotal));
                return new ProdutoNoRankingDto(
                    g.Key, descricao, categoria, g.Sum(x => x.Item.Quantidade), totalDoProduto, CalculoRelatorio.Percentual(totalDoProduto, total));
            })
            .OrderByDescending(p => p.Total)
            .ThenByDescending(p => p.Quantidade)
            .ThenBy(p => p.Descricao, StringComparer.CurrentCulture)
            .ToList();

    private static IReadOnlyList<CategoriaNoRankingDto> RankingCategorias(IReadOnlyList<ProdutoNoRankingDto> produtos, decimal total) =>
        produtos
            .GroupBy(p => p.Categoria)
            .Select(g =>
            {
                var totalDaCategoria = Moeda.Arredondar(g.Sum(p => p.Total));
                return new CategoriaNoRankingDto(g.Key, g.Sum(p => p.Quantidade), totalDaCategoria, CalculoRelatorio.Percentual(totalDaCategoria, total));
            })
            .OrderByDescending(c => c.Total)
            .ThenByDescending(c => c.Quantidade)
            .ThenBy(c => c.Categoria, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>RN-RL-03: sabores escolhidos na venda (potes, RN-ES-09), por unidades; empate → faturamento, depois nome.</summary>
    private async Task<IReadOnlyList<SaborNoRankingDto>> RankingSaboresAsync(
        IReadOnlyList<Comanda> fechadas, IReadOnlyDictionary<Guid, Domain.Produtos.Produto> produtos, CancellationToken cancellationToken)
    {
        var comSabor = fechadas.SelectMany(c => c.Itens).Where(i => i.ItemEstoqueId is not null).ToList();
        if (comSabor.Count == 0)
        {
            return [];
        }

        var grupos = (await _estoque.ListarGruposAsync(cancellationToken)).ToDictionary(g => g.Id);
        var itens = (await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken)).ToDictionary(i => i.Id);
        return comSabor
            .GroupBy(i => i.ItemEstoqueId!.Value)
            .Select(g =>
            {
                var item = itens.GetValueOrDefault(g.Key);
                var descricao = item is null
                    ? g.First().Descricao
                    : MensagensWhatsApp.DescricaoDoItem(grupos.GetValueOrDefault(item.GrupoId)?.Nome, item.Nome);
                var unidades = g.Sum(i => i.Quantidade * (i.ProdutoId is { } id && produtos.TryGetValue(id, out var p) ? p.UnidadesPorVenda : 1));
                return new SaborNoRankingDto(g.Key, descricao, unidades, Moeda.Arredondar(g.Sum(i => i.Subtotal)));
            })
            .OrderByDescending(s => s.Unidades)
            .ThenByDescending(s => s.Total)
            .ThenBy(s => s.Descricao, StringComparer.CurrentCulture)
            .ToList();
    }
}
