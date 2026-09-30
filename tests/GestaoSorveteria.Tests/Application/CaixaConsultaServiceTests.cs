using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

public class CaixaConsultaServiceTests
{
    // 17/09/2026 12:00 em Brasília.
    private static readonly DateTime Abertura = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly CaixasEmMemoria _caixas = new();
    private readonly ComandasEmMemoria _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ClockFake _clock = new() { UtcNow = Abertura.AddHours(5) };
    private readonly CaixaConsultaService _service;

    public CaixaConsultaServiceTests()
    {
        _service = new CaixaConsultaService(_caixas, _comandas, _usuarios, _clock);
        _usuarios.Usuarios.Add(Cenario.Admin());
    }

    private Caixa NovoCaixa(decimal fundo = 100m, DateTime? abertoEm = null)
    {
        var caixa = Caixa.Abrir(_usuarios.Usuarios[0].Id, fundo, abertoEm ?? Abertura);
        _caixas.Caixas.Add(caixa);
        return caixa;
    }

    private Comanda VendaFechada(Caixa caixa, int numero, params DadosPagamento[] pagamentos)
    {
        var comanda = Comanda.Abrir(caixa, Cenario.Atendente, numero, TipoComanda.Balcao, Abertura, Abertura);
        comanda.AdicionarItemLivre("Venda avulsa", pagamentos.Sum(p => p.Valor));
        comanda.Fechar(pagamentos, Abertura.AddMinutes(numero));
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    [Fact]
    public async Task ObterAtual_CaixaAberto_CalculaEsperadoAteAgora()
    {
        var caixa = NovoCaixa(fundo: 100m);
        caixa.RegistrarSangria(30m, "Depósito", Cenario.Atendente, Abertura.AddHours(1));
        VendaFechada(caixa, 1, new DadosPagamento(FormaPagamento.Dinheiro, 12.50m, 20m));
        VendaFechada(caixa, 2, new DadosPagamento(FormaPagamento.Pix, 18.90m));

        var atual = await _service.ObterAtualAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(atual);
        Assert.Equal("Aberto", atual.Status);
        Assert.Equal(31.40m, atual.TotalVendas);
        Assert.Equal(2, atual.QuantidadeComandas);
        Assert.Equal(12.50m, atual.TotalVendasDinheiro);
        Assert.Equal(82.50m, atual.ValorEsperado); // 100 + 12,50 − 30 (RN-CX-06)
        Assert.Null(atual.ValorContado);
        Assert.Null(atual.Diferenca);
        Assert.Equal("Dona Maria", atual.AbertoPorNome);
    }

    [Fact]
    public async Task ObterAtual_SemCaixaAberto_RetornaNull()
    {
        var caixa = NovoCaixa();
        caixa.Fechar(100m, 0m, Cenario.Dona, Abertura.AddHours(8));

        Assert.Null(await _service.ObterAtualAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Obter_CaixaFechado_UsaValoresGravadosEMarcaVendaTardia()
    {
        var caixa = NovoCaixa(fundo: 50m);
        VendaFechada(caixa, 1, new DadosPagamento(FormaPagamento.Dinheiro, 10m, 10m));
        // RN-CX-08: venda feita no celular que só chegou depois do fechamento; não muda os valores gravados (RN-CX-07).
        var tardia = Comanda.Abrir(caixa, Cenario.Atendente, 2, TipoComanda.Balcao, Abertura, Abertura);
        tardia.AdicionarItemLivre("Venda avulsa", 7m);
        caixa.Fechar(55m, 10m, _usuarios.Usuarios[0].Id, Abertura.AddHours(8));
        tardia.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 7m, 7m)], Abertura.AddHours(7));
        tardia.MarcarRecebidaAposFechamentoCaixa();
        _comandas.Comandas.Add(tardia);

        var detalhe = await _service.ObterAsync(caixa.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Fechado", detalhe.Resumo.Status);
        Assert.Equal(10m, detalhe.Resumo.TotalVendasDinheiro);
        Assert.Equal(60m, detalhe.Resumo.ValorEsperado);
        Assert.Equal(55m, detalhe.Resumo.ValorContado);
        Assert.Equal(-5m, detalhe.Resumo.Diferenca);
        Assert.Equal(17m, detalhe.Resumo.TotalVendas);
        Assert.Equal(1, detalhe.Resumo.VendasRecebidasAposFechamento);
        Assert.Equal(7m, detalhe.Resumo.DinheiroRecebidoAposFechamento);
        // A linha "Dinheiro" = vendas em dinheiro do fechamento + dinheiro que chegou depois.
        var dinheiro = Assert.Single(detalhe.PorFormaPagamento);
        Assert.Equal(detalhe.Resumo.TotalVendasDinheiro + detalhe.Resumo.DinheiroRecebidoAposFechamento, dinheiro.Total);
        Assert.Equal("Dona Maria", detalhe.Resumo.FechadoPorNome);
    }

    [Fact]
    public async Task Obter_VendasDivididas_SomaPorFormaDePagamento()
    {
        var caixa = NovoCaixa();
        caixa.RegistrarSuprimento(20m, "Troco extra", Cenario.Atendente, Abertura.AddMinutes(5));
        VendaFechada(caixa, 1,
            new DadosPagamento(FormaPagamento.Dinheiro, 5.55m, 10m),
            new DadosPagamento(FormaPagamento.Pix, 4.45m));
        VendaFechada(caixa, 2, new DadosPagamento(FormaPagamento.Pix, 0.10m));
        var cancelada = Comanda.Abrir(caixa, Cenario.Atendente, 3, TipoComanda.Balcao, Abertura, Abertura);
        cancelada.Cancelar("Desistiu", Abertura.AddMinutes(9));
        _comandas.Comandas.Add(cancelada);

        var detalhe = await _service.ObterAsync(caixa.Id, TestContext.Current.CancellationToken);

        Assert.Collection(detalhe.PorFormaPagamento,
            dinheiro =>
            {
                Assert.Equal("Dinheiro", dinheiro.Forma);
                Assert.Equal(5.55m, dinheiro.Total);
                Assert.Equal(1, dinheiro.Quantidade);
            },
            pix =>
            {
                Assert.Equal("Pix", pix.Forma);
                Assert.Equal(4.55m, pix.Total);
                Assert.Equal(2, pix.Quantidade);
            });
        Assert.Equal(2, detalhe.Resumo.QuantidadeComandas);
        Assert.Equal(10.10m, detalhe.Resumo.TotalVendas);
        var movimento = Assert.Single(detalhe.Movimentos);
        Assert.Equal("Suprimento", movimento.Tipo);
    }

    [Fact]
    public async Task Obter_IdInexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(
            () => _service.ObterAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Listar_PeriodoPorDiaComercial_IncluiSoCaixasDoPeriodo()
    {
        // 18/09 00:30 em Brasília = 18/09 03:30 UTC → dia comercial 18.
        var dia18 = NovoCaixa(abertoEm: new DateTime(2026, 9, 18, 3, 30, 0, DateTimeKind.Utc));
        dia18.Fechar(100m, 0m, Cenario.Dona, new DateTime(2026, 9, 18, 4, 0, 0, DateTimeKind.Utc));
        // 17/09 23:30 em Brasília = 18/09 02:30 UTC → dia comercial 17.
        var dia17 = NovoCaixa(abertoEm: new DateTime(2026, 9, 18, 2, 30, 0, DateTimeKind.Utc));
        dia17.Fechar(100m, 0m, Cenario.Dona, new DateTime(2026, 9, 18, 2, 45, 0, DateTimeKind.Utc));

        var lista = await _service.ListarAsync(new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 18), TestContext.Current.CancellationToken);

        Assert.Equal(dia18.Id, Assert.Single(lista).Id);
    }

    [Fact]
    public async Task Listar_SemDatas_UltimosTrintaDiasMaisRecentePrimeiro()
    {
        var antigo = NovoCaixa(abertoEm: _clock.UtcNow.AddDays(-40));
        antigo.Fechar(100m, 0m, Cenario.Dona, _clock.UtcNow.AddDays(-40).AddHours(1));
        var ontem = NovoCaixa(abertoEm: _clock.UtcNow.AddDays(-1));
        ontem.Fechar(100m, 0m, Cenario.Dona, _clock.UtcNow.AddDays(-1).AddHours(1));
        var hoje = NovoCaixa(abertoEm: _clock.UtcNow.AddHours(-1));

        var lista = await _service.ListarAsync(null, null, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { hoje.Id, ontem.Id }, lista.Select(c => c.Id));
    }

    [Theory]
    [InlineData(2026, 9, 18, 2026, 9, 17)]
    [InlineData(2026, 1, 1, 2026, 4, 30)]
    public async Task Listar_PeriodoInvalido_Lanca(int a1, int m1, int d1, int a2, int m2, int d2)
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.ListarAsync(
            new DateOnly(a1, m1, d1), new DateOnly(a2, m2, d2), TestContext.Current.CancellationToken));
    }
}
