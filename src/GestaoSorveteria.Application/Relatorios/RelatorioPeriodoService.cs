using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Relatorios;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
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
        if (deDia > hoje)
        {
            throw new DomainException("A data inicial não pode passar de hoje.");
        }

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
        var catalogo = new Catalogo(
            await _produtos.ListarAsync(somenteAtivos: false, cancellationToken),
            await _estoque.ListarGruposAsync(cancellationToken),
            await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken));
        var rankingProdutos = RankingProdutos(fechadas, catalogo, total);

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
            RankingSabores(fechadas, catalogo),
            Margem(rankingProdutos, total));
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

    /// <summary>Período de comparação e os trechos dele, pareados por posição com os do período (RN-RL-09).</summary>
    /// <param name="Cortavel">O último dia é o mesmo dia "deslocado" (não foi encurtado): pode contar só até o mesmo horário.</param>
    public sealed record Comparacao(DateOnly De, DateOnly Ate, IReadOnlyList<(DateOnly De, DateOnly Ate)> Trechos, bool Cortavel);

    /// <summary>
    /// RN-RL-09: menos de 7 dias → os mesmos dias da semana anterior (sábado com sábado); começa no dia 1 → os mesmos meses
    /// logo antes (meses inteiros contra meses inteiros; mês pela metade contra o mesmo trecho, limitado ao fim do mês);
    /// qualquer outro → os mesmos N dias logo antes.
    /// </summary>
    public static Comparacao PeriodoAnterior(DateOnly de, DateOnly ate, Agrupamento agrupamento)
    {
        var dias = ate.DayNumber - de.DayNumber + 1;
        var trechos = Trechos(de, ate, agrupamento);
        if (dias < 7)
        {
            return Deslocar(7);
        }

        if (de.Day == 1)
        {
            var meses = (ate.Year * 12 + ate.Month) - (de.Year * 12 + de.Month) + 1;
            var deAntes = de.AddMonths(-meses);
            var ultimoDoMes = ate.Day == DateTime.DaysInMonth(ate.Year, ate.Month);
            var ateAntes = ultimoDoMes ? de.AddDays(-1) : ate.AddMonths(-meses); // AddMonths encurta 30/03 → 28/02
            return new Comparacao(deAntes, ateAntes, Trechos(deAntes, ateAntes, agrupamento), !ultimoDoMes && ateAntes.Day == ate.Day);
        }

        return Deslocar(dias);

        Comparacao Deslocar(int diasAntes) =>
            new(de.AddDays(-diasAntes), ate.AddDays(-diasAntes), trechos.Select(t => (t.De.AddDays(-diasAntes), t.Ate.AddDays(-diasAntes))).ToList(), true);
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
        var anterior = PeriodoAnterior(de, ate, agrupamento);
        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(anterior.De, anterior.Ate);
        // Período até hoje: o último dia do anterior conta só até o mesmo horário (como no RN-RL-07) — se for o mesmo dia
        // deslocado; mês anterior encurtado (30/03 → 28/02) conta inteiro.
        var ateMesmoHorario = ate == hoje && anterior.Cortavel;
        if (ateMesmoHorario)
        {
            fimUtc = DiaComercial.IntervaloUtc(anterior.Ate).InicioUtc + (agora - DiaComercial.IntervaloUtc(hoje).InicioUtc);
        }

        // Só fechamento e total: o anterior não precisa de itens nem pagamentos.
        var vendas = await _comandas.ListarTotaisDasFechadasAsync(inicioUtc, fimUtc, cancellationToken);
        var totalAntes = Moeda.Arredondar(vendas.Sum(v => v.Total));
        var ticketAntes = CalculoRelatorio.TicketMedio(totalAntes, vendas.Count);
        var porDia = vendas.ToLookup(v => DiaComercial.DataDe(v.FechadaEm));

        return new ComparativoPeriodoDto(
            anterior.De,
            anterior.Ate,
            ateMesmoHorario,
            totalAntes,
            vendas.Count,
            ticketAntes,
            CalculoRelatorio.Variacao(total, totalAntes),
            CalculoRelatorio.Variacao(quantidade, vendas.Count),
            CalculoRelatorio.Variacao(ticketMedio, ticketAntes),
            anterior.Trechos.Select(t => SomarTrecho(t, porDia)).ToList());
    }

    private static VendasNoTrechoDto SomarTrecho((DateOnly De, DateOnly Ate) trecho, ILookup<DateOnly, VendaFechada> porDia)
    {
        var doTrecho = Enumerable.Range(0, trecho.Ate.DayNumber - trecho.De.DayNumber + 1).SelectMany(d => porDia[trecho.De.AddDays(d)]).ToList();
        return new VendasNoTrechoDto(trecho.De, trecho.Ate, Moeda.Arredondar(doTrecho.Sum(v => v.Total)), doTrecho.Count);
    }

    private static IReadOnlyList<VendasNoTrechoDto> PorTrecho(IReadOnlyList<Comanda> fechadas, DateOnly de, DateOnly ate, Agrupamento agrupamento)
    {
        var porDia = fechadas.ToLookup(c => DiaComercial.DataDe(c.FechadaEm!.Value), c => new VendaFechada(c.FechadaEm!.Value, c.Total));
        return Trechos(de, ate, agrupamento).Select(t => SomarTrecho(t, porDia)).ToList();
    }

    /// <summary>
    /// RN-RL-10: segunda a domingo, só os dias da semana que o período tem (mesmo sem venda). Hoje conta como um dia, mesmo
    /// ainda aberto.
    /// </summary>
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

    /// <summary>
    /// RN-RL-03: todos os produtos vendidos, por faturamento; empate → mais unidades, depois nome. RN-RL-04: custo e margem
    /// estimados com os custos de hoje (RN-PR-07); se alguma venda do produto não tem custo, o produto fica sem (n/d).
    /// </summary>
    private static IReadOnlyList<ProdutoNoRankingDto> RankingProdutos(IReadOnlyList<Comanda> fechadas, Catalogo catalogo, decimal total) =>
        fechadas
            .SelectMany(c => c.Itens.Select(i => (Comanda: c, Item: i)))
            .GroupBy(x => x.Item.ProdutoId)
            .Select(g =>
            {
                var (descricao, categoria) = CalculoRelatorio.NomeECategoria(g.Key, catalogo.Produtos, g.MaxBy(x => x.Comanda.FechadaEm).Item.Descricao);
                var totalDoProduto = Moeda.Arredondar(g.Sum(x => x.Item.Subtotal));
                var (custo, margem, percentual) = CustoEMargem(g.Select(x => x.Item), totalDoProduto, catalogo);
                return new ProdutoNoRankingDto(
                    g.Key, descricao, categoria, g.Sum(x => x.Item.Quantidade), totalDoProduto, CalculoRelatorio.Percentual(totalDoProduto, total),
                    custo, margem, percentual);
            })
            .OrderByDescending(p => p.Total)
            .ThenByDescending(p => p.Quantidade)
            .ThenBy(p => p.Descricao, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>Soma do custo das linhas; uma linha sem custo deixa tudo sem (n/d, RN-RL-04). Margem % sobre o faturamento.</summary>
    private static (decimal? Custo, decimal? Margem, decimal? Percentual) CustoEMargem(IEnumerable<ItemComanda> linhas, decimal total, Catalogo catalogo)
    {
        var custos = linhas.Select(catalogo.CustoDaLinha).ToList();
        if (custos.Count == 0 || custos.Any(c => c is null))
        {
            return (null, null, null);
        }

        var custo = Moeda.Arredondar(custos.Sum(c => c!.Value));
        var margem = Moeda.Arredondar(total - custo);
        return (custo, margem, total == 0m ? null : CalculoRelatorio.Percentual(margem, total));
    }

    /// <summary>RN-RL-04: margem do período só com os produtos que têm custo; o resto aparece como faturamento sem custo.</summary>
    private static MargemPeriodoDto Margem(IReadOnlyList<ProdutoNoRankingDto> produtos, decimal total)
    {
        var comCusto = produtos.Where(p => p.CustoEstimado is not null).ToList();
        var faturamento = Moeda.Arredondar(comCusto.Sum(p => p.Total));
        if (comCusto.Count == 0)
        {
            // Nenhum produto vendido tem custo: margem "n/d", nunca zero.
            return new MargemPeriodoDto(0m, null, null, null, total);
        }

        var custo = Moeda.Arredondar(comCusto.Sum(p => p.CustoEstimado!.Value));
        var margem = Moeda.Arredondar(faturamento - custo);
        return new MargemPeriodoDto(
            faturamento,
            custo,
            margem,
            faturamento == 0m ? null : CalculoRelatorio.Percentual(margem, faturamento),
            Moeda.Arredondar(total - faturamento));
    }

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

    /// <summary>
    /// RN-RL-03: sabores escolhidos na venda (potes, RN-ES-09), por unidades; empate → faturamento, depois nome. Unidades pelo
    /// "quantos saem por venda" atual do produto (mudou o cadastro, o histórico acompanha). Custo pelo sabor (RN-PR-07).
    /// </summary>
    private static IReadOnlyList<SaborNoRankingDto> RankingSabores(IReadOnlyList<Comanda> fechadas, Catalogo catalogo) =>
        fechadas
            .SelectMany(c => c.Itens)
            .Where(i => i.ItemEstoqueId is not null)
            .GroupBy(i => i.ItemEstoqueId!.Value)
            .Select(g =>
            {
                var item = catalogo.Itens.GetValueOrDefault(g.Key);
                var descricao = item is null
                    ? g.First().Descricao
                    : MensagensWhatsApp.DescricaoDoItem(catalogo.Grupos.GetValueOrDefault(item.GrupoId)?.Nome, item.Nome);
                var unidades = g.Sum(i => i.Quantidade * (i.ProdutoId is { } id && catalogo.Produtos.TryGetValue(id, out var p) ? p.UnidadesPorVenda : 1));
                var total = Moeda.Arredondar(g.Sum(i => i.Subtotal));
                var (custo, margem, percentual) = CustoEMargem(g, total, catalogo);
                return new SaborNoRankingDto(g.Key, descricao, unidades, total, custo, margem, percentual);
            })
            .OrderByDescending(s => s.Unidades)
            .ThenByDescending(s => s.Total)
            .ThenBy(s => s.Descricao, StringComparer.CurrentCulture)
            .ToList();

    /// <summary>Cadastro de hoje (produtos e catálogo de estoque) para nomes e custos; guarda o custo por produto e sabor.</summary>
    private sealed class Catalogo
    {
        private readonly ILookup<Guid, ItemEstoque> _itensPorGrupo;
        private readonly Dictionary<(Guid Produto, Guid? Sabor), decimal?> _custoPorUnidade = new();

        public Catalogo(IReadOnlyList<Produto> produtos, IReadOnlyList<GrupoEstoque> grupos, IReadOnlyList<ItemEstoque> itens)
        {
            Produtos = produtos.ToDictionary(p => p.Id);
            Grupos = grupos.ToDictionary(g => g.Id);
            Itens = itens.ToDictionary(i => i.Id);
            _itensPorGrupo = itens.ToLookup(i => i.GrupoId);
        }

        public IReadOnlyDictionary<Guid, Produto> Produtos { get; }
        public IReadOnlyDictionary<Guid, GrupoEstoque> Grupos { get; }
        public IReadOnlyDictionary<Guid, ItemEstoque> Itens { get; }

        /// <summary>RN-PR-07: custo estimado da linha (quantidade × custo por unidade vendida); nulo = sem custo.</summary>
        public decimal? CustoDaLinha(ItemComanda linha)
        {
            if (linha.ProdutoId is not { } produtoId || !Produtos.TryGetValue(produtoId, out var produto))
            {
                return null;
            }

            var chave = (produtoId, linha.ItemEstoqueId);
            if (!_custoPorUnidade.TryGetValue(chave, out var porUnidade))
            {
                var sabor = linha.ItemEstoqueId is { } saborId ? Itens.GetValueOrDefault(saborId) : null;
                var doGrupo = produto.GrupoEstoqueId is { } grupoId ? _itensPorGrupo[grupoId] : [];
                porUnidade = CustoEstimado.PorUnidadeVendida(produto, doGrupo, sabor)?.Custo;
                _custoPorUnidade[chave] = porUnidade;
            }

            return porUnidade is { } valor ? Moeda.Arredondar(valor * linha.Quantidade) : null;
        }
    }
}
