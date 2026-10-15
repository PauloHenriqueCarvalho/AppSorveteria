using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

public class RelatorioServiceTests
{
    // 17/09/2026 12:00 em Brasília.
    private static readonly DateTime Meio17 = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly CaixasEmMemoria _caixas = new();
    private readonly ComandasEmMemoria _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ClockFake _clock = new() { UtcNow = Meio17 };
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly Caixa _caixa;
    private readonly RelatorioService _service;

    public RelatorioServiceTests()
    {
        _usuarios.Usuarios.Add(Cenario.Admin());
        var consultaCaixas = new CaixaConsultaService(_caixas, _comandas, _usuarios, _clock);
        _service = new RelatorioService(_comandas, _produtos, consultaCaixas, _clock);
        _caixa = Caixa.Abrir(_usuarios.Usuarios[0].Id, 100m, Meio17.AddHours(-4));
        _caixas.Caixas.Add(_caixa);
    }

    private Comanda Venda(int numero, DateTime fechadaEm, params DadosPagamento[] pagamentos)
    {
        var comanda = Comanda.Abrir(_caixa, Cenario.Atendente, numero, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        comanda.AdicionarItemLivre("Venda avulsa", pagamentos.Sum(p => p.Valor));
        comanda.Fechar(pagamentos, fechadaEm);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    [Fact]
    public async Task ResumoDoDia_Hoje_SomaVendasTicketMedioEFormas()
    {
        Venda(1, Meio17.AddHours(-2), new DadosPagamento(FormaPagamento.Dinheiro, 10m, 10m));
        Venda(2, Meio17.AddHours(-1), new DadosPagamento(FormaPagamento.Pix, 5m));
        Venda(3, Meio17.AddMinutes(-1), new DadosPagamento(FormaPagamento.Pix, 5m));
        Venda(4, Meio17.AddDays(-1), new DadosPagamento(FormaPagamento.Pix, 99m)); // ontem
        var cancelada = Comanda.Abrir(_caixa, Cenario.Atendente, 5, TipoComanda.Balcao, Meio17, Meio17);
        cancelada.Cancelar(null, Meio17.AddMinutes(-3));
        _comandas.Comandas.Add(cancelada);

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(new DateOnly(2026, 9, 17), resumo.Data);
        Assert.Equal(20m, resumo.TotalVendido);
        Assert.Equal(3, resumo.QuantidadeComandas);
        Assert.Equal(6.67m, resumo.TicketMedio); // 20 ÷ 3 = 6,666… → 6,67
        Assert.Equal(1, resumo.QuantidadeCanceladas);
        Assert.Collection(resumo.PorFormaPagamento,
            d => Assert.Equal(("Dinheiro", 10m, 1), (d.Forma, d.Total, d.Quantidade)),
            p => Assert.Equal(("Pix", 10m, 2), (p.Forma, p.Total, p.Quantidade)));
        Assert.NotNull(resumo.CaixaAtual);
        Assert.Equal(110m, resumo.CaixaAtual.ValorEsperado); // fundo 100 + 10 em dinheiro (RN-CX-06)
    }

    [Fact]
    public async Task ResumoDoDia_VendaEstornada_FicaForaDoTotalEApareceComoEstorno()
    {
        Venda(1, Meio17.AddHours(-2), new DadosPagamento(FormaPagamento.Dinheiro, 10m, 10m));
        var estornada = Venda(2, Meio17.AddHours(-1), new DadosPagamento(FormaPagamento.Pix, 7.50m));
        estornada.Estornar("Cobrado duas vezes", _usuarios.Usuarios[0].Id, Meio17);

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        // RN-RL-01: faturamento só com as fechadas; o estorno à parte, no dia da venda.
        Assert.Equal(10m, resumo.TotalVendido);
        Assert.Equal(1, resumo.QuantidadeComandas);
        Assert.Equal(10m, resumo.TicketMedio);
        Assert.Equal("Dinheiro", Assert.Single(resumo.PorFormaPagamento).Forma);
        Assert.Equal(7.50m, resumo.TotalEstornado);
        Assert.Equal(1, resumo.QuantidadeEstornos);
        Assert.Equal(0, resumo.QuantidadeCanceladas);
    }

    [Fact]
    public async Task ResumoDoDia_SemVendas_ZeraSemDividirPorZero()
    {
        var resumo = await _service.ResumoDoDiaAsync(new DateOnly(2026, 9, 10), TestContext.Current.CancellationToken);

        Assert.Equal(0m, resumo.TotalVendido);
        Assert.Equal(0, resumo.QuantidadeComandas);
        Assert.Equal(0m, resumo.TicketMedio);
        Assert.Empty(resumo.PorFormaPagamento);
    }

    [Fact]
    public async Task ResumoDoDia_TicketMedioNoMeioCentavo_ArredondaParaCima()
    {
        // 0,05 ÷ 2 = 0,025 → 0,03 (MidpointRounding.AwayFromZero, RN-TD-02).
        Venda(1, Meio17.AddMinutes(-2), new DadosPagamento(FormaPagamento.Pix, 0.02m));
        Venda(2, Meio17.AddMinutes(-1), new DadosPagamento(FormaPagamento.Pix, 0.03m));

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(0.03m, resumo.TicketMedio);
    }

    [Theory]
    [InlineData(9999, 12, 31)]
    [InlineData(1, 1, 1)]
    [InlineData(2019, 12, 31)]
    public async Task ResumoDoDia_DataForaDaFaixa_Lanca(int ano, int mes, int dia)
    {
        var erro = await Assert.ThrowsAsync<DomainException>(
            () => _service.ResumoDoDiaAsync(new DateOnly(ano, mes, dia), TestContext.Current.CancellationToken));
        Assert.Contains("01/01/2020", erro.Message);
    }

    [Fact]
    public async Task ResumoDoDia_SemCaixaAberto_CaixaAtualNulo()
    {
        _caixa.Fechar(100m, 0m, Cenario.Dona, Meio17);

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        Assert.Null(resumo.CaixaAtual);
    }

    // ------------------------------------------------------------ dashboard: por hora, comparativo, mais vendidos

    private Comanda VendaCom(Caixa caixa, int numero, DateTime fechadaEm, Action<Comanda> itens)
    {
        var comanda = Comanda.Abrir(caixa, Cenario.Atendente, numero, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        itens(comanda);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, comanda.Total)], fechadaEm);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    /// <summary>Caixa já fechado da semana anterior, para as vendas do comparativo.</summary>
    private Caixa CaixaDaSemanaPassada(Action<Caixa> vendas)
    {
        var caixa = Caixa.Abrir(_usuarios.Usuarios[0].Id, 0m, Meio17.AddDays(-7).AddHours(-4));
        _caixas.Caixas.Add(caixa);
        vendas(caixa);
        caixa.Fechar(0m, 0m, Cenario.Dona, Meio17.AddDays(-7).AddHours(8));
        return caixa;
    }

    [Fact]
    public async Task ResumoDoDia_PorHora_AgrupaPelaHoraLocalDoFechamento()
    {
        // Meio17 = 12:00 em Brasília (15:00 UTC).
        Venda(1, Meio17.AddHours(-2).AddMinutes(-30), new DadosPagamento(FormaPagamento.Pix, 10m)); // 09:30
        Venda(2, Meio17.AddHours(-2).AddMinutes(-10), new DadosPagamento(FormaPagamento.Pix, 5m)); // 09:50
        Venda(3, Meio17.AddMinutes(-50), new DadosPagamento(FormaPagamento.Pix, 7.25m)); // 11:10

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        // RN-RL-06: só as horas com venda, pela hora local.
        Assert.Collection(resumo.PorHora,
            h => Assert.Equal((9, 15m, 2), (h.Hora, h.Total, h.Quantidade)),
            h => Assert.Equal((11, 7.25m, 1), (h.Hora, h.Total, h.Quantidade)));
    }

    [Fact]
    public async Task ResumoDoDia_Hoje_ComparaComSemanaPassadaAteOMesmoHorario()
    {
        Venda(1, Meio17.AddHours(-2), new DadosPagamento(FormaPagamento.Pix, 10m)); // 10:00
        Venda(2, Meio17.AddHours(-1), new DadosPagamento(FormaPagamento.Pix, 20m)); // 11:00
        CaixaDaSemanaPassada(caixa =>
        {
            VendaCom(caixa, 1, Meio17.AddDays(-7).AddHours(-2), c => c.AdicionarItemLivre("Venda avulsa", 20m)); // 10:00 de 10/09
            VendaCom(caixa, 2, Meio17.AddDays(-7).AddHours(3), c => c.AdicionarItemLivre("Venda avulsa", 50m)); // 15:00: depois do horário de agora
        });

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        // RN-RL-07: hoje 30 em 2 vendas (ticket 15) × semana passada até 12:00, 20 em 1 venda (ticket 20).
        var antes = resumo.SemanaPassada;
        Assert.Equal(new DateOnly(2026, 9, 10), antes.Data);
        Assert.True(antes.AteMesmoHorario);
        Assert.Equal((20m, 1, 20m), (antes.TotalVendido, antes.QuantidadeComandas, antes.TicketMedio));
        Assert.Equal(50.0m, antes.VariacaoTotalPercentual);
        Assert.Equal(100.0m, antes.VariacaoQuantidadePercentual);
        Assert.Equal(-25.0m, antes.VariacaoTicketPercentual);
        Assert.Equal((10, 20m, 1), (Assert.Single(antes.PorHora).Hora, antes.PorHora[0].Total, antes.PorHora[0].Quantidade));
    }

    [Fact]
    public async Task ResumoDoDia_DiaPassadoSemVendasNaSemanaAnterior_DiaInteiroESemVariacao()
    {
        Venda(1, Meio17.AddDays(-1), new DadosPagamento(FormaPagamento.Pix, 10m)); // 16/09

        var resumo = await _service.ResumoDoDiaAsync(new DateOnly(2026, 9, 16), TestContext.Current.CancellationToken);

        // RN-RL-07: dia que já passou compara com o dia inteiro; base zero não vira 100%.
        Assert.False(resumo.SemanaPassada.AteMesmoHorario);
        Assert.Equal(new DateOnly(2026, 9, 9), resumo.SemanaPassada.Data);
        Assert.Equal(0m, resumo.SemanaPassada.TotalVendido);
        Assert.Null(resumo.SemanaPassada.VariacaoTotalPercentual);
        Assert.Null(resumo.SemanaPassada.VariacaoQuantidadePercentual);
        Assert.Null(resumo.SemanaPassada.VariacaoTicketPercentual);
    }

    [Fact]
    public async Task ResumoDoDia_MaisVendidos_PorFaturamentoComItensLivresJuntosESemEstornadas()
    {
        var picole = Cenario.Picole(5m);
        var shake = Cenario.MilkShake(18.90m);
        VendaCom(_caixa, 1, Meio17.AddHours(-3), c => c.AdicionarProduto(picole, 3)); // 15,00
        VendaCom(_caixa, 2, Meio17.AddHours(-2), c =>
        {
            c.AdicionarProduto(shake, 1); // 18,90
            c.AdicionarItemLivre("Self-service", 12.40m);
        });
        VendaCom(_caixa, 3, Meio17.AddHours(-1), c => c.AdicionarItemLivre("Venda avulsa", 3m));
        var estornada = VendaCom(_caixa, 4, Meio17.AddMinutes(-30), c => c.AdicionarProduto(shake, 5));
        estornada.Estornar("Lançado errado", _usuarios.Usuarios[0].Id, Meio17);

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        // RN-RL-03: 18,90 (shake) > 15,40 (self-service + avulsa) > 15,00 (picolé); o estorno não entra.
        Assert.Collection(resumo.MaisVendidos,
            p => Assert.Equal((shake.Id, "Milk-shake 500ml", 1, 18.90m), (p.ProdutoId!.Value, p.Descricao, p.Quantidade, p.Total)),
            p => Assert.Equal(((Guid?)null, "Self-service / venda avulsa", 2, 15.40m), (p.ProdutoId, p.Descricao, p.Quantidade, p.Total)),
            p => Assert.Equal((picole.Id, "Picolé de morango", 3, 15.00m), (p.ProdutoId!.Value, p.Descricao, p.Quantidade, p.Total)));
    }

    [Fact]
    public async Task ResumoDoDia_MaisVendidos_PoteComSaborAparecePeloNomeDoCadastro()
    {
        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var grupo = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 1);
        var flocos = ItemEstoque.Criar(grupo.Id, fabrica.Id, "Flocos", UnidadeCompra.Unidade, 1, 14m, 1);
        var chocolate = ItemEstoque.Criar(grupo.Id, fabrica.Id, "Chocolate", UnidadeCompra.Unidade, 1, 14m, 2);
        var pote = Produto.Criar("Pote 1L", "Potes", 28m, false, 4, Meio17.AddDays(-30));
        pote.LigarAoEstoque(grupo.Id, 1, Meio17.AddDays(-30));
        _produtos.Produtos.Add(pote);
        VendaCom(_caixa, 1, Meio17.AddHours(-2), c => c.AdicionarProduto(pote, 1, sabor: flocos));
        VendaCom(_caixa, 2, Meio17.AddHours(-1), c => c.AdicionarProduto(pote, 2, sabor: chocolate));

        var resumo = await _service.ResumoDoDiaAsync(null, TestContext.Current.CancellationToken);

        // RN-RL-03: a linha diz "Pote 1L · Chocolate", mas o ranking é do produto.
        var maisVendido = Assert.Single(resumo.MaisVendidos);
        Assert.Equal(("Pote 1L", 3, 84m), (maisVendido.Descricao, maisVendido.Quantidade, maisVendido.Total));
    }
}
