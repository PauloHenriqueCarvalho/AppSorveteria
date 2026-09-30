using GestaoSorveteria.Application.Comandas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

public class ComandaConsultaServiceTests
{
    // 17/09/2026 12:00 em Brasília.
    private static readonly DateTime Meio17 = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly ComandasEmMemoria _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ClockFake _clock = new() { UtcNow = Meio17 };
    private readonly Caixa _caixa;
    private readonly ComandaConsultaService _service;

    public ComandaConsultaServiceTests()
    {
        _service = new ComandaConsultaService(_comandas, _usuarios, _clock);
        _usuarios.Usuarios.Add(Cenario.Admin());
        _caixa = Caixa.Abrir(_usuarios.Usuarios[0].Id, 100m, Meio17.AddDays(-2));
    }

    private Comanda Venda(int numero, DateTime fechadaEm, params DadosPagamento[] pagamentos)
    {
        var comanda = Comanda.Abrir(_caixa, _usuarios.Usuarios[0].Id, numero, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        comanda.AdicionarItemLivre("Self-service", pagamentos.Sum(p => p.Valor));
        comanda.Fechar(pagamentos, fechadaEm);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    [Fact]
    public async Task Listar_SemDatas_TrazVendasDeHojeMaisRecentePrimeiro()
    {
        Venda(1, Meio17.AddHours(-1), new DadosPagamento(FormaPagamento.Pix, 10m));
        var ultima = Venda(2, Meio17.AddMinutes(-5),
            new DadosPagamento(FormaPagamento.Pix, 3m),
            new DadosPagamento(FormaPagamento.Dinheiro, 7.25m, 10m),
            new DadosPagamento(FormaPagamento.Pix, 1m));
        Venda(3, Meio17.AddDays(-1), new DadosPagamento(FormaPagamento.Pix, 4m)); // ontem

        var pagina = await _service.ListarAsync(null, null, null, null, null, TestContext.Current.CancellationToken);

        Assert.Equal(2, pagina.TotalItens);
        Assert.Equal(new[] { 2, 1 }, pagina.Itens.Select(c => c.Numero));
        var primeira = pagina.Itens[0];
        Assert.Equal(ultima.Id, primeira.Id);
        Assert.Equal(11.25m, primeira.Total);
        Assert.Equal(new[] { "Dinheiro", "Pix" }, primeira.FormasPagamento);
        Assert.Equal("Dona Maria", primeira.AtendenteNome);
    }

    [Fact]
    public async Task Listar_VendaFechadaDepoisDaMeiaNoite_ContaNoDiaDoFechamento()
    {
        // Criada 17/09 23:58 e fechada 18/09 00:01 em Brasília (03:01 UTC).
        Venda(1, new DateTime(2026, 9, 18, 3, 1, 0, DateTimeKind.Utc), new DadosPagamento(FormaPagamento.Pix, 5m));

        var dia17 = await _service.ListarAsync(new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 17), null, null, null, TestContext.Current.CancellationToken);
        var dia18 = await _service.ListarAsync(new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 18), null, null, null, TestContext.Current.CancellationToken);

        Assert.Empty(dia17.Itens);
        Assert.Single(dia18.Itens);
    }

    [Fact]
    public async Task Listar_FiltroCancelada_TrazSoCanceladas()
    {
        Venda(1, Meio17.AddMinutes(-30), new DadosPagamento(FormaPagamento.Pix, 5m));
        var cancelada = Comanda.Abrir(_caixa, _usuarios.Usuarios[0].Id, 2, TipoComanda.Delivery, Meio17.AddMinutes(-20), Meio17);
        cancelada.Cancelar("Cliente desistiu", Meio17.AddMinutes(-10));
        _comandas.Comandas.Add(cancelada);

        var pagina = await _service.ListarAsync(null, null, "cancelada", null, null, TestContext.Current.CancellationToken);

        var item = Assert.Single(pagina.Itens);
        Assert.Equal("Cancelada", item.Status);
        Assert.Equal("Delivery", item.Tipo);
        Assert.Empty(item.FormasPagamento);
    }

    [Fact]
    public async Task Listar_Paginado_DevolveTotalSemPaginacao()
    {
        for (var i = 1; i <= 5; i++)
        {
            Venda(i, Meio17.AddMinutes(-60 + i), new DadosPagamento(FormaPagamento.Pix, 1m));
        }

        var pagina2 = await _service.ListarAsync(null, null, null, 2, 2, TestContext.Current.CancellationToken);

        Assert.Equal(5, pagina2.TotalItens);
        Assert.Equal(2, pagina2.Pagina);
        Assert.Equal(new[] { 3, 2 }, pagina2.Itens.Select(c => c.Numero));
    }

    [Theory]
    [InlineData("Paga")]
    [InlineData("2")]
    public async Task Listar_StatusInvalido_Lanca(string status)
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _service.ListarAsync(null, null, status, null, null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Listar_PaginacaoInvalida_Lanca(int pagina, int tamanho)
    {
        await Assert.ThrowsAsync<DomainException>(
            () => _service.ListarAsync(null, null, null, pagina, tamanho, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Obter_VendaComTroco_MostraItensEPagamentosComoGravados()
    {
        var venda = Venda(7, Meio17, new DadosPagamento(FormaPagamento.Dinheiro, 12.50m, 20m));

        var detalhe = await _service.ObterAsync(venda.Id, TestContext.Current.CancellationToken);

        var item = Assert.Single(detalhe.Itens);
        Assert.Null(item.ProdutoId); // item livre
        Assert.Equal(12.50m, item.Subtotal);
        var pagamento = Assert.Single(detalhe.Pagamentos);
        Assert.Equal(20m, pagamento.ValorRecebido);
        Assert.Equal(7.50m, pagamento.Troco);
        Assert.Equal(12.50m, detalhe.Total);
    }

    [Fact]
    public async Task Obter_IdInexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(
            () => _service.ObterAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }
}
