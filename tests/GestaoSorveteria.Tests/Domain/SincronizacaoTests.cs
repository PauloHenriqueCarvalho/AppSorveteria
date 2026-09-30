using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using static GestaoSorveteria.Tests.Domain.Cenario;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Remontagem no servidor do que o app fechou offline (RN-SY-06, RN-CX-08, RN-CX-10).</summary>
public class SincronizacaoTests
{
    private static readonly DateTime Recebida = Agora.AddHours(2);

    private static Comanda Remontar(Caixa caixa, decimal total, params DadosItemRecebido[] itens) =>
        Comanda.Remontar(caixa, Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, Agora.AddMinutes(5), Recebida, null, itens, total);

    private static DadosItemRecebido Item(Guid? produtoId, string descricao, int quantidade, decimal preco) =>
        new(Guid.NewGuid(), produtoId, descricao, quantidade, preco, Moeda.Arredondar(quantidade * preco));

    [Fact]
    public void Remontar_ProdutoMudouDePrecoEFoiDesativado_UsaPrecoDaVenda()
    {
        var picole = Picole(preco: 5m);
        // Depois da venda offline, a dona reajustou o preço e desativou o produto.
        picole.Atualizar(picole.Nome, picole.Categoria, 6m, false, picole.Ordem, Agora.AddHours(1));
        picole.Desativar(Agora.AddHours(1));

        var comanda = Remontar(CaixaAberto(), 10m, Item(picole.Id, "Picolé de morango", 2, 5m));

        var item = Assert.Single(comanda.Itens);
        Assert.Equal(5m, item.PrecoUnitario); // RN-SY-06 / RN-PR-04
        Assert.Equal(picole.Id, item.ProdutoId);
        Assert.Equal(10m, comanda.Total);
        Assert.Equal(Recebida, comanda.RecebidaEm);
        Assert.False(comanda.RecebidaAposFechamentoCaixa);
    }

    [Fact]
    public void Remontar_ItemLivre_ProdutoIdNulo()
    {
        var comanda = Remontar(CaixaAberto(), 23.45m, Item(null, "Self-service", 1, 23.45m));

        Assert.True(Assert.Single(comanda.Itens).EhItemLivre);
    }

    [Fact]
    public void Remontar_TotalEnviadoDiferenteDaSomaDosItens_Lanca()
    {
        var ex = Assert.Throws<DomainException>(() => Remontar(CaixaAberto(), 11m, Item(null, "Venda avulsa", 1, 10m)));
        Assert.Contains("total", ex.Message);
    }

    [Fact]
    public void Remontar_SubtotalEnviadoErrado_Lanca()
    {
        var item = new DadosItemRecebido(Guid.NewGuid(), null, "Venda avulsa", 3, 3.33m, 10m);

        Assert.Throws<DomainException>(() => Remontar(CaixaAberto(), 10m, item));
    }

    [Fact]
    public void Remontar_ProdutoIdVazio_Lanca()
    {
        Assert.Throws<DomainException>(() => Remontar(CaixaAberto(), 5m, Item(Guid.Empty, "Picolé", 1, 5m)));
    }

    [Fact]
    public void Remontar_PagamentosDiferentesDoTotal_FecharLanca()
    {
        var comanda = Remontar(CaixaAberto(), 20m, Item(null, "Venda avulsa", 1, 20m));

        // RN-CM-07: a soma dos pagamentos precisa ser exatamente o total → no lote, vira "rejeitada".
        Assert.Throws<DomainException>(() => comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 19.99m)], Recebida));
        Assert.True(comanda.EstaAberta);
    }

    [Fact]
    public void Fechar_TrocoInformadoDiferenteDoCalculado_Lanca()
    {
        var comanda = Remontar(CaixaAberto(), 18m, Item(null, "Venda avulsa", 1, 18m));

        var ex = Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 18m, ValorRecebido: 20m, TrocoInformado: 3m)], Recebida));
        Assert.Contains("troco", ex.Message);
    }

    [Fact]
    public void Fechar_TrocoInformadoCorreto_Fecha()
    {
        var comanda = Remontar(CaixaAberto(), 18m, Item(null, "Venda avulsa", 1, 18m));

        comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 18m, ValorRecebido: 20m, TrocoInformado: 2m)], Recebida);

        Assert.Equal(2m, Assert.Single(comanda.Pagamentos).Troco);
    }

    [Fact]
    public void Remontar_CaixaJaFechado_AceitaMarcaENaoMudaOsValoresDoCaixa()
    {
        var caixa = CaixaAberto(fundoTroco: 100m);
        caixa.Fechar(valorContado: 150m, totalVendasDinheiro: 50m, Atendente, Agora.AddHours(1));

        var comanda = Remontar(caixa, 30m, Item(null, "Venda avulsa", 1, 30m));
        comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 30m, 30m)], Recebida);

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
    public void Remontar_CaixaFechado_PodeSerCancelada()
    {
        var caixa = CaixaAberto();
        caixa.Fechar(100m, 0m, Atendente, Agora.AddHours(1));

        var comanda = Remontar(caixa, 0m);
        comanda.Cancelar("cliente desistiu", Recebida);

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
    public void Remontar_CriadaAntesDaAberturaDoCaixa_Lanca()
    {
        var caixa = CaixaAberto();

        Assert.Throws<DomainException>(() => Comanda.Remontar(
            caixa, Guid.NewGuid(), Atendente, 1, TipoComanda.Balcao, caixa.AbertoEm.AddMinutes(-1), Recebida, null, [], 0m));
    }

    [Fact]
    public void Remontar_ItemComIdRepetido_Lanca()
    {
        var item = Item(null, "Venda avulsa", 1, 5m);

        Assert.Throws<DomainException>(() => Remontar(CaixaAberto(), 10m, item, item));
    }

    [Fact]
    public void Remontar_PagamentosMaioresQueOTotal_FecharLanca()
    {
        var comanda = Remontar(CaixaAberto(), 20m, Item(null, "Venda avulsa", 1, 20m));

        Assert.Throws<DomainException>(() => comanda.Fechar(
            [new DadosPagamento(FormaPagamento.Pix, 10m), new DadosPagamento(FormaPagamento.CartaoDebito, 10.01m)], Recebida));
    }

    [Fact]
    public void Fechar_PixComTrocoInformado_Lanca()
    {
        var comanda = Remontar(CaixaAberto(), 18m, Item(null, "Venda avulsa", 1, 18m));

        Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 18m, TrocoInformado: 1m)], Recebida));
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
