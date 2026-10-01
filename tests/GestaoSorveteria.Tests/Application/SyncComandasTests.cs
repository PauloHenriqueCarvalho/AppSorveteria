using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>POST /api/sync/comandas (RN-SY-02/06, RN-CX-07/08, RN-CM-07, RN-PG-04).</summary>
public class SyncComandasTests
{
    private static readonly DateTime Abertura = new(2026, 9, 17, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Venda = Abertura.AddHours(1);

    private readonly CaixaRepositoryFake _caixas = new();
    private readonly ComandaRepositoryFake _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Abertura.AddHours(3) };
    private readonly SyncService _service;
    private readonly Guid _atendente;
    private readonly Produto _picole;
    private readonly Guid _caixaId = Guid.NewGuid();
    private int _numero;

    public SyncComandasTests()
    {
        var atendente = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Abertura);
        var dona = Usuario.Criar("Dona Maria", "maria", "hash", PerfilUsuario.Admin, Abertura);
        _usuarios.Usuarios.AddRange([atendente, dona]);
        _atendente = atendente.Id;
        _picole = Produto.Criar("Picolé de morango", "Picolés", 5m, false, 1, Abertura);
        _produtos.Produtos.Add(_picole);
        _service = new SyncService(_caixas, _comandas, _usuarios, _produtos, _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task AbrirCaixa(decimal fundo = 100m) =>
        await _service.ReceberCaixasAsync(new SyncCaixasRequest([new CaixaSyncDto(_caixaId, _atendente, Abertura, fundo, [])]), Ct);

    private ComandaSyncDto Fechada(decimal total, PagamentoSyncDto[] pagamentos, params ItemComandaSyncDto[] itens) =>
        new(Guid.NewGuid(), _caixaId, _atendente, ++_numero, "Balcao", "Fechada", total, Venda, itens, pagamentos, FechadaEm: Venda.AddMinutes(2));

    private ComandaSyncDto Avulsa(decimal valor, string forma = "Pix", decimal? recebido = null) =>
        Fechada(valor, [Pagamento(forma, valor, recebido)], Item(null, "Venda avulsa", 1, valor));

    private static ItemComandaSyncDto Item(Guid? produtoId, string descricao, int quantidade, decimal preco) =>
        new(Guid.NewGuid(), produtoId, descricao, quantidade, preco, quantidade * preco);

    private static PagamentoSyncDto Pagamento(string forma, decimal valor, decimal? recebido = null)
    {
        var entregue = recebido ?? valor;
        return new PagamentoSyncDto(Guid.NewGuid(), forma, valor, entregue, entregue - valor);
    }

    private Task<SyncResponse> Enviar(params ComandaSyncDto[] comandas) => _service.ReceberComandasAsync(new SyncComandasRequest(comandas), Ct);

    [Fact]
    public async Task ReceberComandas_FechadaComProdutoQueMudouDePreco_AceitaComPrecoDaVenda()
    {
        await AbrirCaixa();
        _picole.Atualizar(_picole.Nome, _picole.Categoria, 6m, false, _picole.Ordem, Abertura.AddHours(2));

        var resposta = await Enviar(Fechada(10m, [Pagamento("Pix", 10m)], Item(_picole.Id, "Picolé de morango", 2, 5m)));

        Assert.Equal(StatusSync.Aceita, Assert.Single(resposta.Resultados).Status);
        var comanda = Assert.Single(_comandas.Comandas);
        Assert.Equal(5m, Assert.Single(comanda.Itens).PrecoUnitario); // RN-SY-06
        Assert.Equal(_clock.UtcNow, comanda.RecebidaEm); // RN-CM-12: hora do servidor
        Assert.Equal(Venda, comanda.CriadaEm);
        Assert.False(comanda.RecebidaAposFechamentoCaixa);
    }

    [Fact]
    public async Task ReceberComandas_MesmoLoteDuasVezes_GravaCadaVendaUmaVez()
    {
        await AbrirCaixa();
        var lote = new[] { Avulsa(10m), Avulsa(20m, "Dinheiro", 50m) };

        await Enviar(lote);
        var segunda = await Enviar(lote);

        Assert.All(segunda.Resultados, r => Assert.Equal(StatusSync.JaRecebida, r.Status));
        Assert.Equal(2, _comandas.Comandas.Count);
    }

    [Fact]
    public async Task ReceberComandas_LoteComUmaInvalida_AsOutrasSaoAceitas()
    {
        await AbrirCaixa();
        var valida1 = Avulsa(10m);
        var pagamentoMenor = Fechada(20m, [Pagamento("Pix", 19.99m)], Item(null, "Venda avulsa", 1, 20m));
        var valida2 = Avulsa(15m);

        var resposta = await Enviar(valida1, pagamentoMenor, valida2);

        Assert.Equal([StatusSync.Aceita, StatusSync.Rejeitada, StatusSync.Aceita], resposta.Resultados.Select(r => r.Status));
        Assert.Contains("pagamentos somam", resposta.Resultados[1].Motivo);
        Assert.Equal(2, _comandas.Comandas.Count);
        Assert.Equal(1, _uow.Descartes);
    }

    [Fact]
    public async Task ReceberComandas_TrocoErrado_Rejeita()
    {
        await AbrirCaixa();
        var dto = Fechada(18m, [new PagamentoSyncDto(Guid.NewGuid(), "Dinheiro", 18m, 20m, 3m)], Item(null, "Venda avulsa", 1, 18m));

        var resposta = await Enviar(dto);

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("troco", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_CaixaJaFechado_AceitaMarcaENaoMudaOsValoresDoCaixa()
    {
        await AbrirCaixa(fundo: 100m);
        await Enviar(Avulsa(50m, "Dinheiro"));
        await _service.ReceberCaixasAsync(new SyncCaixasRequest([
            new CaixaSyncDto(_caixaId, _atendente, Abertura, 100m, [], new FechamentoCaixaSyncDto(_atendente, Abertura.AddHours(2), 150m, 50m, 150m, 0m)),
        ]), Ct);

        var resposta = await Enviar(Avulsa(30m, "Dinheiro"));

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        Assert.True(_comandas.Comandas[^1].RecebidaAposFechamentoCaixa); // RN-CX-08
        var caixa = _caixas.Caixas[0];
        Assert.Equal(50m, caixa.TotalVendasDinheiro); // RN-CX-07: a venda tardia não entra
        Assert.Equal(150m, caixa.ValorEsperado);
        Assert.Equal(0m, caixa.Diferenca);
    }

    [Fact]
    public async Task FluxoCompleto_CaixaComandasEFechamento_TotaisDoCaixaBatem()
    {
        // Critério de pronto do Sprint 1: lote enviado 2× grava uma vez e o caixa bate.
        await AbrirCaixa(fundo: 100m);
        var vendas = new[] { Avulsa(12.50m, "Dinheiro", 20m), Avulsa(30m, "Pix"), Avulsa(7.50m, "Dinheiro", 10m) };
        await Enviar(vendas);
        await Enviar(vendas);
        var fechamento = new CaixaSyncDto(_caixaId, _atendente, Abertura, 100m, [],
            new FechamentoCaixaSyncDto(_atendente, Abertura.AddHours(8), ValorContado: 120m, TotalVendasDinheiro: 20m, ValorEsperado: 120m, Diferenca: 0m));

        var resposta = await _service.ReceberCaixasAsync(new SyncCaixasRequest([fechamento]), Ct);

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        Assert.Equal(3, _comandas.Comandas.Count);
        var caixa = _caixas.Caixas[0];
        Assert.Equal(20m, caixa.TotalVendasDinheiro); // só dinheiro (RN-PG-05), pelo Valor e não pelo recebido
        Assert.Equal(120m, caixa.ValorEsperado);
        Assert.Equal(0m, caixa.Diferenca);
        Assert.False(caixa.DivergenciaSincronizacao);
    }

    [Fact]
    public async Task ReceberComandas_CaixaAindaNaoRecebido_Rejeita()
    {
        var resposta = await Enviar(Avulsa(10m));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("RN-SY-03", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_NumeroRepetidoNoCaixa_RejeitaSemTravarAFila()
    {
        await AbrirCaixa();
        var primeira = Avulsa(10m);
        await Enviar(primeira);

        var resposta = await Enviar(Avulsa(10m) with { Numero = primeira.Numero });

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("RN-CM-02", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_IdDeItemJaUsadoEmOutraComanda_Rejeita()
    {
        await AbrirCaixa();
        var primeira = Avulsa(10m);
        await Enviar(primeira);

        var copia = Avulsa(10m) with { Itens = primeira.Itens };
        var resposta = await Enviar(copia);

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
    }

    [Fact]
    public async Task ReceberComandas_ProdutoInexistente_Rejeita()
    {
        await AbrirCaixa();

        var resposta = await Enviar(Fechada(5m, [Pagamento("Pix", 5m)], Item(Guid.NewGuid(), "Sumido", 1, 5m)));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("Produto não encontrado", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_EstornadaNoServidorEReenviadaFechada_JaRecebida()
    {
        await AbrirCaixa();
        var dto = Avulsa(10m);
        await Enviar(dto);
        _comandas.Comandas[0].Estornar("cliente devolveu", Abertura.AddHours(2)); // RN-CM-09 pelo painel

        var resposta = await Enviar(dto);

        Assert.Equal(StatusSync.JaRecebida, resposta.Resultados[0].Status);
        Assert.Equal(StatusComanda.Cancelada, _comandas.Comandas[0].Status);
    }

    [Fact]
    public async Task ReceberComandas_ReenvioComOutroTotal_Rejeita()
    {
        await AbrirCaixa();
        var dto = Avulsa(10m);
        await Enviar(dto);

        var resposta = await Enviar(dto with { Total = 11m });

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Equal(10m, _comandas.Comandas[0].Total);
    }

    [Fact]
    public async Task ReceberComandas_Cancelada_Aceita()
    {
        await AbrirCaixa();
        var dto = new ComandaSyncDto(Guid.NewGuid(), _caixaId, _atendente, 1, "Delivery", "Cancelada", 0m, Venda, [], [],
            CanceladaEm: Venda.AddMinutes(1), MotivoCancelamento: "cliente desistiu");

        var resposta = await Enviar(dto);

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        Assert.Equal(StatusComanda.Cancelada, _comandas.Comandas[0].Status);
    }

    [Theory]
    [InlineData("Aberta")]
    [InlineData("2")]
    [InlineData("Paga")]
    public async Task ReceberComandas_StatusInvalido_Rejeita(string status)
    {
        await AbrirCaixa();

        var resposta = await Enviar(Avulsa(10m) with { Status = status });

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Empty(_comandas.Comandas);
    }

    [Fact]
    public async Task ReceberComandas_ItensOuPagamentosNulos_NaoDerrubaOLote()
    {
        await AbrirCaixa();

        var resposta = await _service.ReceberComandasAsync(new SyncComandasRequest([
            null!,
            Avulsa(10m) with { Itens = null!, Pagamentos = null! },
            Avulsa(10m) with { Pagamentos = [null!] },
            Avulsa(5m),
        ]), Ct);

        Assert.Equal([StatusSync.Rejeitada, StatusSync.Rejeitada, StatusSync.Rejeitada, StatusSync.Aceita], resposta.Resultados.Select(r => r.Status));
    }
    [Fact]
    public async Task ReceberComandas_MesmaComandaDuasVezesNoLote_SegundaJaRecebida()
    {
        await AbrirCaixa();
        var dto = Avulsa(10m);

        var resposta = await Enviar(dto, dto);

        Assert.Equal([StatusSync.Aceita, StatusSync.JaRecebida], resposta.Resultados.Select(r => r.Status));
        Assert.Single(_comandas.Comandas);
    }

    [Fact]
    public async Task ReceberComandas_UsuarioInexistente_Rejeita()
    {
        await AbrirCaixa();

        var resposta = await Enviar(Avulsa(10m) with { UsuarioId = Guid.NewGuid() });

        Assert.Contains("Usuário não encontrado", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_VendaDepoisDeFechamentoForcado_AceitaEMarca()
    {
        await AbrirCaixa();
        _caixas.Caixas[0].Fechar(0m, 0m, _atendente, Abertura.AddMinutes(30), "fechamento forçado"); // RN-CX-09

        var resposta = await Enviar(Avulsa(10m, "Dinheiro"));

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        Assert.True(_comandas.Comandas[0].RecebidaAposFechamentoCaixa);
        Assert.Equal(0m, _caixas.Caixas[0].TotalVendasDinheiro);
    }

    [Fact]
    public async Task ReceberComandas_CanceladaComPagamento_Rejeita()
    {
        await AbrirCaixa();
        var dto = new ComandaSyncDto(Guid.NewGuid(), _caixaId, _atendente, 1, "Balcao", "Cancelada", 0m, Venda, [], [Pagamento("Pix", 10m)],
            CanceladaEm: Venda.AddMinutes(1));

        var resposta = await Enviar(dto);

        Assert.Contains("RN-CM-09", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_PagamentoSemId_Rejeita()
    {
        await AbrirCaixa();
        var semId = new PagamentoSyncDto(Guid.Empty, "Pix", 10m, 10m, 0m);

        var resposta = await Enviar(Fechada(10m, [semId], Item(null, "Venda avulsa", 1, 10m)));

        Assert.Contains("Pagamento sem Id", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberComandas_ValorAcimaDoMaximo_RejeitaSemDerrubarOLote()
    {
        await AbrirCaixa();
        var enorme = 10_000_000_000m; // digitou demais: não cabe em numeric(12,2)

        var resposta = await Enviar(Avulsa(10m), Fechada(enorme, [Pagamento("Dinheiro", enorme)], Item(null, "Venda avulsa", 1, enorme)));

        Assert.Equal([StatusSync.Aceita, StatusSync.Rejeitada], resposta.Resultados.Select(r => r.Status));
    }

    [Fact]
    public async Task ReceberComandas_ReenvioComOutroNumero_Rejeita()
    {
        await AbrirCaixa();
        var dto = Avulsa(10m);
        await Enviar(dto);

        var resposta = await Enviar(dto with { Numero = dto.Numero + 10 });

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
    }

    [Fact]
    public async Task ReceberComandas_CamposDeTextoNulos_Rejeita()
    {
        await AbrirCaixa();

        var resposta = await Enviar(Avulsa(10m) with { Tipo = null }, Avulsa(10m) with { Status = null });

        Assert.All(resposta.Resultados, r => Assert.Equal(StatusSync.Rejeitada, r.Status));
    }
}
