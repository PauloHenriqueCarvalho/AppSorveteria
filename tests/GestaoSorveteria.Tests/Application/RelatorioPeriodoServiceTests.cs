using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Tests.Domain;
using static GestaoSorveteria.Application.Relatorios.RelatorioPeriodoService;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Relatório de período (Sprint 8): trechos, comparativo, picos e rankings (RN-RL-01/03/08/09/10).</summary>
public class RelatorioPeriodoServiceTests
{
    // Quarta 14/10/2026, 15:00 em Brasília.
    private static readonly DateTime Agora = new(2026, 10, 14, 18, 0, 0, DateTimeKind.Utc);

    private readonly ComandasEmMemoria _comandas = new();
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly ClockFake _clock = new() { UtcNow = Agora };
    private readonly Caixa _caixa = Caixa.Abrir(Cenario.Atendente, 100m, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly RelatorioPeriodoService _service;
    private int _numero;

    public RelatorioPeriodoServiceTests()
    {
        _service = new RelatorioPeriodoService(_comandas, _produtos, _estoque, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Hora de Brasília (UTC−3) num dia.</summary>
    private static DateTime Em(int mes, int dia, int hora = 12) => new DateTime(2026, mes, dia, 0, 0, 0, DateTimeKind.Utc).AddHours(hora + 3);

    private Comanda Venda(DateTime fechadaEm, decimal valor) =>
        VendaCom(fechadaEm, c => c.AdicionarItemLivre("Venda avulsa", valor));

    private Comanda VendaCom(DateTime fechadaEm, Action<Comanda> itens)
    {
        var comanda = Comanda.Abrir(_caixa, Cenario.Atendente, ++_numero, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        itens(comanda);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, comanda.Total)], fechadaEm);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    [Fact]
    public async Task Obter_SemDatas_EsteMesAteHojeComparadoAoComecoDoMesPassadoAteOMesmoHorario()
    {
        Venda(Em(10, 1), 10m);
        Venda(Em(10, 14, 10), 20m);
        Venda(Em(9, 1), 5m);
        Venda(Em(9, 14, 10), 7m);
        Venda(Em(9, 14, 16), 100m); // depois das 15h: fora do comparativo
        Venda(Em(9, 20), 50m);      // depois do dia 14: fora

        var relatorio = await _service.ObterAsync(null, null, null, Ct);

        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14), "Dia"), (relatorio.De, relatorio.Ate, relatorio.Agrupamento));
        Assert.Equal((30m, 2, 15m), (relatorio.TotalVendido, relatorio.QuantidadeComandas, relatorio.TicketMedio));
        Assert.Equal(14, relatorio.PorTrecho.Count);
        Assert.Equal((new DateOnly(2026, 10, 1), 10m, 1), (relatorio.PorTrecho[0].De, relatorio.PorTrecho[0].Total, relatorio.PorTrecho[0].Quantidade));
        Assert.Equal(0m, relatorio.PorTrecho[1].Total); // dia sem venda também aparece
        var anterior = relatorio.Anterior;
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14), true), (anterior.De, anterior.Ate, anterior.AteMesmoHorario));
        Assert.Equal((12m, 2), (anterior.TotalVendido, anterior.QuantidadeComandas));
        Assert.Equal(150m, anterior.VariacaoTotalPercentual); // (30 − 12) ÷ 12
        Assert.Equal(14, anterior.PorTrecho.Count);
    }

    [Fact]
    public async Task Obter_SemanaPassada_ComparaComOsSeteDiasAntesInteiros()
    {
        Venda(Em(10, 5), 40m);
        Venda(Em(10, 4, 23), 25m); // domingo 23h: período anterior
        Venda(Em(9, 27), 99m);     // antes do anterior

        var relatorio = await _service.ObterAsync(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11), null, Ct);

        Assert.Equal(40m, relatorio.TotalVendido);
        Assert.Equal((new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 4), false),
            (relatorio.Anterior.De, relatorio.Anterior.Ate, relatorio.Anterior.AteMesmoHorario));
        Assert.Equal(25m, relatorio.Anterior.TotalVendido);
        Assert.Equal(60m, relatorio.Anterior.VariacaoTotalPercentual);
    }

    [Fact]
    public async Task Obter_VendaEstornada_FicaForaDoTotalEDoRanking()
    {
        Venda(Em(10, 2), 10m);
        Venda(Em(10, 3), 8m).Estornar("Lançado errado", Cenario.Dona, Em(10, 3, 13));

        var relatorio = await _service.ObterAsync(null, null, null, Ct);

        Assert.Equal((10m, 1), (relatorio.TotalVendido, relatorio.QuantidadeComandas));
        Assert.Equal((8m, 1), (relatorio.TotalEstornado, relatorio.QuantidadeEstornos));
        Assert.Equal(10m, Assert.Single(relatorio.Produtos).Total);
    }

    [Fact]
    public void Trechos_Semana_SegundaADomingoRecortadaNasPontas()
    {
        var trechos = Trechos(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14), Agrupamento.Semana);

        Assert.Equal(
            new[]
            {
                (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 4)),
                (new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11)),
                (new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 14)),
            },
            trechos);
    }

    [Fact]
    public void Trechos_Mes_RecortadoNasPontas()
    {
        var trechos = Trechos(new DateOnly(2026, 8, 15), new DateOnly(2026, 10, 14), Agrupamento.Mes);

        Assert.Equal(
            new[]
            {
                (new DateOnly(2026, 8, 15), new DateOnly(2026, 8, 31)),
                (new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)),
                (new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 14)),
            },
            trechos);
    }

    [Theory]
    [InlineData("2026-03-01", "2026-03-31", "2026-02-01", "2026-02-28", false)] // mês inteiro contra fevereiro inteiro
    [InlineData("2028-03-01", "2028-03-31", "2028-02-01", "2028-02-29", false)] // ano bissexto
    [InlineData("2026-09-01", "2026-09-30", "2026-08-01", "2026-08-31", false)] // setembro contra agosto inteiro
    [InlineData("2026-07-01", "2026-09-30", "2026-04-01", "2026-06-30", false)] // trimestre contra trimestre
    [InlineData("2026-01-01", "2026-01-31", "2025-12-01", "2025-12-31", false)] // virada do ano
    [InlineData("2026-10-01", "2026-10-14", "2026-09-01", "2026-09-14", true)]  // começo do mês
    [InlineData("2026-09-01", "2026-10-14", "2026-07-01", "2026-08-14", true)]  // mês e meio
    [InlineData("2026-03-01", "2026-03-30", "2026-02-01", "2026-02-28", false)] // 30/03 → fevereiro encurtado
    [InlineData("2026-10-10", "2026-10-14", "2026-10-03", "2026-10-07", true)]  // menos de 7 dias: semana anterior
    [InlineData("2026-10-14", "2026-10-14", "2026-10-07", "2026-10-07", true)]  // um dia: o mesmo dia da semana passada
    [InlineData("2026-10-05", "2026-10-18", "2026-09-21", "2026-10-04", true)]  // 14 dias quaisquer: os 14 antes
    public void PeriodoAnterior_ConformeInicioEFim_DevolveOPeriodoDeComparacao(string de, string ate, string deAntes, string ateAntes, bool cortavel)
    {
        var anterior = PeriodoAnterior(DateOnly.Parse(de), DateOnly.Parse(ate), Agrupamento.Dia);

        Assert.Equal((DateOnly.Parse(deAntes), DateOnly.Parse(ateAntes), cortavel), (anterior.De, anterior.Ate, anterior.Cortavel));
    }

    [Fact]
    public void PeriodoAnterior_TrimestrePorMes_TrechosPareadosMesAMes()
    {
        var anterior = PeriodoAnterior(new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), Agrupamento.Mes);

        Assert.Equal(
            new[]
            {
                (new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)),
                (new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)),
                (new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)),
            },
            anterior.Trechos);
    }

    [Fact]
    public void PeriodoAnterior_DezDiasPorSemana_MesmosTamanhosNaMesmaOrdem()
    {
        // Período: [05–11/10, 12–14/10]; o anterior é deslocado 10 dias, trecho a trecho.
        var anterior = PeriodoAnterior(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 14), Agrupamento.Semana);

        Assert.Equal(
            new[] { (new DateOnly(2026, 9, 25), new DateOnly(2026, 10, 1)), (new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 4)) },
            anterior.Trechos);
    }

    [Fact]
    public async Task Obter_EsteMesNoDia31ContraMesDe30_ComparaComOMesAnteriorInteiro()
    {
        _clock.UtcNow = Em(10, 31, 15);
        Venda(Em(10, 31, 10), 20m);
        Venda(Em(9, 30, 20), 30m); // 30/09 às 20h: conta, o mês anterior vai inteiro

        var relatorio = await _service.ObterAsync(null, null, null, Ct);

        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), false),
            (relatorio.Anterior.De, relatorio.Anterior.Ate, relatorio.Anterior.AteMesmoHorario));
        Assert.Equal(30m, relatorio.Anterior.TotalVendido);
        Assert.Equal(31, relatorio.PorTrecho.Count);
        Assert.Equal(30, relatorio.Anterior.PorTrecho.Count);
    }

    [Fact]
    public async Task Obter_VendasEspalhadas_SomaDosTrechosEIgualAoTotal()
    {
        Venda(Em(8, 31, 23), 7.35m);
        Venda(Em(9, 1), 12.10m);
        Venda(Em(9, 15, 21), 9.99m);
        Venda(Em(10, 14, 9), 4.56m);

        var relatorio = await _service.ObterAsync(new DateOnly(2026, 8, 20), new DateOnly(2026, 10, 14), "semana", Ct);

        Assert.Equal(relatorio.TotalVendido, relatorio.PorTrecho.Sum(t => t.Total));
        Assert.Equal(relatorio.QuantidadeComandas, relatorio.PorTrecho.Sum(t => t.Quantidade));
        Assert.Equal(relatorio.TotalVendido, relatorio.PorDiaDaSemana.Sum(d => d.Total));
        Assert.Equal(relatorio.TotalVendido, relatorio.PorHora.Sum(h => h.Total));
        Assert.Equal(34.00m, relatorio.TotalVendido);
    }

    [Fact]
    public async Task Obter_InicioNoFuturoSemDataFinal_DizQueAInicialPassouDeHoje()
    {
        var erro = await Assert.ThrowsAsync<DomainException>(() => _service.ObterAsync(new DateOnly(2026, 10, 20), null, null, Ct));

        Assert.Equal("A data inicial não pode passar de hoje.", erro.Message);
    }

    [Theory]
    [InlineData(31, "Dia")]
    [InlineData(32, "Semana")]
    [InlineData(92, "Semana")]
    [InlineData(93, "Mes")]
    public async Task Obter_SemAgrupamento_EscolhePeloTamanho(int dias, string esperado)
    {
        var ate = new DateOnly(2026, 10, 14);

        var relatorio = await _service.ObterAsync(ate.AddDays(1 - dias), ate, null, Ct);

        Assert.Equal(esperado, relatorio.Agrupamento);
    }

    [Theory]
    [InlineData("2026-06-01", "2026-10-14", "dia")]   // por dia passa de 93 dias
    [InlineData("2026-10-01", "2026-10-15", null)]    // amanhã
    [InlineData("2026-10-01", "2026-10-14", "ano")]   // agrupamento desconhecido
    [InlineData("2025-10-01", "2026-10-14", "mes")]   // mais de 366 dias
    public async Task Obter_PedidoInvalido_Lanca(string de, string ate, string? agrupar)
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.ObterAsync(DateOnly.Parse(de), DateOnly.Parse(ate), agrupar, Ct));
    }

    [Fact]
    public async Task Obter_PorDiaDaSemana_SegundaPrimeiroComMediaPelosDiasDoPeriodo()
    {
        Venda(Em(9, 7), 30m);  // segunda
        Venda(Em(9, 14), 10m); // segunda
        Venda(Em(9, 19, 20), 22m); // sábado 20h

        var relatorio = await _service.ObterAsync(new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 20), null, Ct);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 0 }, relatorio.PorDiaDaSemana.Select(d => d.DiaDaSemana));
        var segunda = relatorio.PorDiaDaSemana[0];
        Assert.Equal((2, 40m, 2, 20m), (segunda.Dias, segunda.Total, segunda.Quantidade, segunda.MediaPorDia));
        Assert.Equal((2, 0m, 0m), (relatorio.PorDiaDaSemana[1].Dias, relatorio.PorDiaDaSemana[1].Total, relatorio.PorDiaDaSemana[1].MediaPorDia));
        Assert.Equal(11m, relatorio.PorDiaDaSemana[5].MediaPorDia); // 22 ÷ 2 sábados
        Assert.Equal(new[] { (12, 40m), (20, 22m) }, relatorio.PorHora.Select(h => (h.Hora, h.Total)));
    }

    [Fact]
    public async Task Obter_Ranking_ProdutosPeloCadastroCategoriasESaboresDosPotes()
    {
        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var grupo = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 1);
        var flocos = ItemEstoque.Criar(grupo.Id, fabrica.Id, "Flocos", UnidadeCompra.Unidade, 1, 14m, 1);
        var chocolate = ItemEstoque.Criar(grupo.Id, fabrica.Id, "Chocolate", UnidadeCompra.Unidade, 1, 14m, 2);
        _estoque.Fornecedores.Add(fabrica);
        _estoque.Grupos.Add(grupo);
        _estoque.Itens.AddRange([flocos, chocolate]);
        var picole = Produto.Criar("Picolé de morango", "Picolés", 5m, false, 1, Agora.AddDays(-60));
        var pote = Produto.Criar("Pote 1L", "Potes", 28m, false, 2, Agora.AddDays(-60));
        pote.LigarAoEstoque(grupo.Id, 1, Agora.AddDays(-60));
        var doisPotes = Produto.Criar("2 potes de 1L", "Potes", 50m, false, 3, Agora.AddDays(-60));
        doisPotes.LigarAoEstoque(grupo.Id, 2, Agora.AddDays(-60));
        _produtos.Produtos.AddRange([picole, pote, doisPotes]);

        VendaCom(Em(10, 2), c => c.AdicionarProduto(picole, 3));                       // 15,00
        VendaCom(Em(10, 3), c => c.AdicionarProduto(pote, 1, sabor: flocos));          // 28,00
        VendaCom(Em(10, 4), c =>
        {
            c.AdicionarProduto(doisPotes, 1, sabor: chocolate);                         // 50,00
            c.AdicionarProduto(pote, 1, sabor: chocolate);                              // 28,00
            c.AdicionarItemLivre("Self-service", 12.40m);
        });
        VendaCom(Em(10, 5), c => c.AdicionarProduto(pote, 5, sabor: flocos)).Estornar("Errado", Cenario.Dona, Em(10, 5, 13));

        var relatorio = await _service.ObterAsync(null, null, null, Ct);

        Assert.Equal(133.40m, relatorio.TotalVendido);
        // O nome vem do cadastro, não da linha "Pote 1L · Flocos".
        Assert.Equal(
            new[]
            {
                ("Pote 1L", "Potes", 2, 56m, 42.0m),
                ("2 potes de 1L", "Potes", 1, 50m, 37.5m),
                ("Picolé de morango", "Picolés", 3, 15m, 11.2m),
                ("Self-service / venda avulsa", "Self-service / venda avulsa", 1, 12.40m, 9.3m),
            },
            relatorio.Produtos.Select(p => (p.Descricao, p.Categoria, p.Quantidade, p.Total, p.Percentual)));
        Assert.Equal(
            new[] { ("Potes", 3, 106m, 79.5m), ("Picolés", 3, 15m, 11.2m), ("Self-service / venda avulsa", 1, 12.40m, 9.3m) },
            relatorio.Categorias.Select(c => (c.Categoria, c.Quantidade, c.Total, c.Percentual)));
        // Sabores em potes: "2 potes" conta 2 unidades.
        Assert.Equal(
            new[] { ("Pote 1L Chocolate", 3, 78m), ("Pote 1L Flocos", 1, 28m) },
            relatorio.Sabores.Select(s => (s.Descricao, s.Unidades, s.Total)));
    }

    [Fact]
    public async Task Obter_SemVendas_ZeraSemDividirPorZero()
    {
        var relatorio = await _service.ObterAsync(new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11), "semana", Ct);

        Assert.Equal((0m, 0, 0m), (relatorio.TotalVendido, relatorio.QuantidadeComandas, relatorio.TicketMedio));
        Assert.Null(relatorio.Anterior.VariacaoTotalPercentual);
        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11), 0m), (relatorio.PorTrecho.Single().De, relatorio.PorTrecho.Single().Ate, relatorio.PorTrecho.Single().Total));
        Assert.Empty(relatorio.Produtos);
        Assert.Empty(relatorio.Sabores);
        Assert.All(relatorio.PorDiaDaSemana, d => Assert.Equal(0m, d.MediaPorDia));
    }
}
