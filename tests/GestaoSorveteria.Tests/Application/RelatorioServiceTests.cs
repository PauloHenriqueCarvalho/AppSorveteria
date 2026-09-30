using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
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
    private readonly Caixa _caixa;
    private readonly RelatorioService _service;

    public RelatorioServiceTests()
    {
        _usuarios.Usuarios.Add(Cenario.Admin());
        var consultaCaixas = new CaixaConsultaService(_caixas, _comandas, _usuarios, _clock);
        _service = new RelatorioService(_comandas, consultaCaixas, _clock);
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
}
