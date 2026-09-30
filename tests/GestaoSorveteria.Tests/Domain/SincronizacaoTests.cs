using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using static GestaoSorveteria.Tests.Domain.Cenario;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Remontagem no servidor do que o app fechou offline (RN-SY-06, RN-CX-08, RN-CX-10).</summary>
public class SincronizacaoTests
{
    private static readonly DateTime Criada = Agora.AddMinutes(5);
    private static readonly DateTime Fechada = Agora.AddMinutes(10);
    private static readonly DateTime Recebida = Agora.AddHours(2);

    private static Comanda RemontarFechada(Caixa caixa, decimal total, DadosPagamento[] pagamentos, params DadosItemRecebido[] itens) =>
        Comanda.Remontar(caixa, Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Fechada, Criada, Recebida, null,
            itens, total, pagamentos, fechadaEmUtc: Fechada);

    private static Comanda RemontarCancelada(Caixa caixa, DadosPagamento[]? pagamentos = null, DateTime? fechadaEm = null) =>
        Comanda.Remontar(caixa, Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Cancelada, Criada, Recebida, null,
            [], 0m, pagamentos, fechadaEm, canceladaEmUtc: Fechada, motivoCancelamento: "cliente desistiu");

    private static DadosItemRecebido Item(Guid? produtoId, string descricao, int quantidade, decimal preco) =>
        new(Guid.NewGuid(), produtoId, descricao, quantidade, preco, Moeda.Arredondar(quantidade * preco));

    private static DadosItemRecebido Avulsa(decimal valor) => Item(null, "Venda avulsa", 1, valor);

    private static DadosPagamento[] Pix(decimal valor) => [new DadosPagamento(FormaPagamento.Pix, valor)];

    [Fact]
    public void Remontar_ProdutoMudouDePrecoEFoiDesativado_UsaPrecoDaVenda()
    {
        var picole = Picole(preco: 5m);
        // Depois da venda offline, a dona reajustou o preço e desativou o produto.
        picole.Atualizar(picole.Nome, picole.Categoria, 6m, false, picole.Ordem, Agora.AddHours(1));
        picole.Desativar(Agora.AddHours(1));

        var comanda = RemontarFechada(CaixaAberto(), 10m, Pix(10m), Item(picole.Id, "Picolé de morango", 2, 5m));

        var item = Assert.Single(comanda.Itens);
        Assert.Equal(5m, item.PrecoUnitario); // RN-SY-06 / RN-PR-04
        Assert.Equal(picole.Id, item.ProdutoId);
        Assert.Equal(10m, comanda.Total);
        Assert.True(comanda.EstaFechada);
        Assert.Equal(Recebida, comanda.RecebidaEm);
        Assert.False(comanda.RecebidaAposFechamentoCaixa);
    }

    [Fact]
    public void Remontar_ItemLivre_ProdutoIdNulo()
    {
        var comanda = RemontarFechada(CaixaAberto(), 23.45m, Pix(23.45m), Item(null, "Self-service", 1, 23.45m));

        Assert.True(Assert.Single(comanda.Itens).EhItemLivre);
    }

    [Fact]
    public void Remontar_TotalEnviadoDiferenteDaSomaDosItens_Lanca()
    {
        var ex = Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 11m, Pix(10m), Avulsa(10m)));
        Assert.Contains("total", ex.Message);
    }

    [Fact]
    public void Remontar_SubtotalEnviadoErrado_Lanca()
    {
        var item = new DadosItemRecebido(Guid.NewGuid(), null, "Venda avulsa", 3, 3.33m, 10m);

        var ex = Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 10m, Pix(10m), item));
        Assert.Contains("subtotal", ex.Message);
    }

    [Fact]
    public void Remontar_ProdutoIdVazio_Lanca()
    {
        Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 5m, Pix(5m), Item(Guid.Empty, "Picolé", 1, 5m)));
    }

    [Fact]
    public void Remontar_ItemComIdRepetido_Lanca()
    {
        var item = Avulsa(5m);

        Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 10m, Pix(10m), item, item));
    }

    // RN-CM-07: a soma dos pagamentos precisa ser exatamente o total → no lote, vira "rejeitada".
    [Theory]
    [InlineData(19.99)]
    [InlineData(20.01)]
    public void Remontar_PagamentosDiferentesDoTotal_Lanca(double pago)
    {
        Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 20m, Pix((decimal)pago), Avulsa(20m)));
    }

    [Fact]
    public void Remontar_TrocoInformadoDiferenteDoCalculado_Lanca()
    {
        var ex = Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 18m,
            [new DadosPagamento(FormaPagamento.Dinheiro, 18m, ValorRecebido: 20m, TrocoInformado: 3m)], Avulsa(18m)));
        Assert.Contains("troco", ex.Message);
    }

    [Fact]
    public void Remontar_TrocoInformadoCorreto_Fecha()
    {
        var comanda = RemontarFechada(CaixaAberto(), 18m,
            [new DadosPagamento(FormaPagamento.Dinheiro, 18m, ValorRecebido: 20m, TrocoInformado: 2m)], Avulsa(18m));

        Assert.Equal(2m, Assert.Single(comanda.Pagamentos).Troco);
    }

    [Fact]
    public void Remontar_PixComTrocoInformado_Lanca()
    {
        Assert.Throws<DomainException>(() => RemontarFechada(CaixaAberto(), 18m,
            [new DadosPagamento(FormaPagamento.Pix, 18m, TrocoInformado: 1m)], Avulsa(18m)));
    }

    [Fact]
    public void Remontar_StatusAberta_Lanca()
    {
        Assert.Throws<DomainException>(() => Comanda.Remontar(
            CaixaAberto(), Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Aberta, Criada, Recebida, null, [], 0m));
    }

    // Estorno é só pelo painel (RN-CM-09): o celular não manda comanda cancelada depois de paga.
    [Fact]
    public void Remontar_CanceladaComPagamento_Lanca()
    {
        var ex = Assert.Throws<DomainException>(() => RemontarCancelada(CaixaAberto(), pagamentos: Pix(10m)));
        Assert.Contains("RN-CM-09", ex.Message);
    }

    [Fact]
    public void Remontar_CanceladaComFechamento_Lanca()
    {
        var ex = Assert.Throws<DomainException>(() => RemontarCancelada(CaixaAberto(), fechadaEm: Fechada));
        Assert.Contains("RN-CM-09", ex.Message);
    }

    [Fact]
    public void Remontar_FechadaComCancelamento_Lanca()
    {
        var ex = Assert.Throws<DomainException>(() => Comanda.Remontar(
            CaixaAberto(), Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Fechada, Criada, Recebida, null,
            [Avulsa(10m)], 10m, Pix(10m), fechadaEmUtc: Fechada, canceladaEmUtc: Fechada));
        Assert.Contains("RN-CM-09", ex.Message);
    }

    [Fact]
    public void Remontar_FechadaAntesDaCriacao_Lanca()
    {
        Assert.Throws<DomainException>(() => Comanda.Remontar(
            CaixaAberto(), Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Fechada, Criada, Recebida, null,
            [Avulsa(10m)], 10m, Pix(10m), fechadaEmUtc: Criada.AddMinutes(-1)));
    }

    [Fact]
    public void Remontar_CanceladaAntesDaCriacao_Lanca()
    {
        Assert.Throws<DomainException>(() => Comanda.Remontar(
            CaixaAberto(), Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Cancelada, Criada, Recebida, null,
            [], 0m, canceladaEmUtc: Criada.AddMinutes(-1)));
    }

    [Fact]
    public void Remontar_CriadaAntesDaAberturaDoCaixa_Lanca()
    {
        var caixa = CaixaAberto();

        Assert.Throws<DomainException>(() => Comanda.Remontar(
            caixa, Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, StatusComanda.Cancelada, caixa.AbertoEm.AddMinutes(-1), Recebida, null,
            [], 0m, canceladaEmUtc: Fechada));
    }

    [Fact]
    public void Remontar_CaixaJaFechado_AceitaMarcaENaoMudaOsValoresDoCaixa()
    {
        var caixa = CaixaAberto(fundoTroco: 100m);
        caixa.Fechar(valorContado: 150m, totalVendasDinheiro: 50m, Atendente, Agora.AddHours(1));

        var comanda = RemontarFechada(caixa, 30m, [new DadosPagamento(FormaPagamento.Dinheiro, 30m, 30m)], Avulsa(30m));

        Assert.True(comanda.EstaFechada);
        Assert.True(comanda.RecebidaAposFechamentoCaixa); // RN-CX-08
        // RN-CX-07: o caixa fechado não muda com a venda tardia.
        Assert.Equal(50m, caixa.TotalVendasDinheiro);
        Assert.Equal(150m, caixa.ValorEsperado);
        Assert.Equal(150m, caixa.ValorContado);
        Assert.Equal(0m, caixa.Diferenca);
        Assert.Equal(StatusCaixa.Fechado, caixa.Status);
    }

    [Fact]
    public void Remontar_CanceladaEmCaixaFechado_AceitaEMarca()
    {
        var caixa = CaixaAberto();
        caixa.Fechar(100m, 0m, Atendente, Agora.AddHours(1));

        var comanda = RemontarCancelada(caixa);

        Assert.Equal(StatusComanda.Cancelada, comanda.Status);
        Assert.True(comanda.RecebidaAposFechamentoCaixa);
    }

    [Fact]
    public void FecharSincronizado_ValoresDoAppIguais_SemDivergencia()
    {
        var caixa = CaixaAberto(fundoTroco: 100m);

        caixa.FecharSincronizado(valorContado: 180m, totalVendasDinheiro: 80m, totalVendasDinheiroApp: 80m, valorEsperadoApp: 180m, Atendente, Agora.AddHours(8));

        Assert.False(caixa.DivergenciaSincronizacao);
        Assert.Null(caixa.TotalVendasDinheiroApp);
        Assert.Null(caixa.ValorEsperadoApp);
        Assert.Equal(0m, caixa.Diferenca);
    }

    [Fact]
    public void FecharSincronizado_AppDiverge_GravaValoresDoServidorEMarca()
    {
        var caixa = CaixaAberto(fundoTroco: 100m);

        // O celular contou 80 em vendas em dinheiro; o servidor só recebeu 50 (RN-CX-10).
        caixa.FecharSincronizado(valorContado: 180m, totalVendasDinheiro: 50m, totalVendasDinheiroApp: 80m, valorEsperadoApp: 180m, Atendente, Agora.AddHours(8));

        Assert.True(caixa.DivergenciaSincronizacao);
        Assert.Equal(50m, caixa.TotalVendasDinheiro);
        Assert.Equal(150m, caixa.ValorEsperado);
        Assert.Equal(30m, caixa.Diferenca);
        Assert.Equal(80m, caixa.TotalVendasDinheiroApp);
        Assert.Equal(180m, caixa.ValorEsperadoApp);
        Assert.Equal(StatusCaixa.Fechado, caixa.Status);
    }

    [Fact]
    public void FecharSincronizado_SoOEsperadoDiverge_Marca()
    {
        var caixa = CaixaAberto(fundoTroco: 100m);

        // Mesmas vendas, mas o celular não tinha uma sangria que o servidor tem → esperado diferente.
        caixa.RegistrarSangria(20m, "depósito", Atendente, Agora.AddHours(1));
        caixa.FecharSincronizado(valorContado: 130m, totalVendasDinheiro: 50m, totalVendasDinheiroApp: 50m, valorEsperadoApp: 150m, Atendente, Agora.AddHours(8));

        Assert.True(caixa.DivergenciaSincronizacao);
        Assert.Equal(130m, caixa.ValorEsperado);
        Assert.Equal(150m, caixa.ValorEsperadoApp);
    }

    [Fact]
    public void FecharSincronizado_CaixaJaFechado_Lanca()
    {
        var caixa = CaixaAberto();
        caixa.Fechar(100m, 0m, Dona, Agora.AddHours(1)); // ex.: fechamento forçado pelo painel (RN-CX-09)

        Assert.Throws<DomainException>(() => caixa.FecharSincronizado(100m, 0m, 0m, 100m, Atendente, Agora.AddHours(2)));
        Assert.Equal(Dona, caixa.FechadoPorUsuarioId);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(10.005, 100)]
    [InlineData(10, 100.001)]
    public void FecharSincronizado_ValoresDoAppInvalidos_Lanca(double totalApp, double esperadoApp)
    {
        var caixa = CaixaAberto();

        Assert.Throws<DomainException>(() => caixa.FecharSincronizado(100m, 0m, (decimal)totalApp, (decimal)esperadoApp, Atendente, Agora.AddHours(1)));
        Assert.True(caixa.EstaAberto);
    }
}
