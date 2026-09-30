using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>POST /api/sync/caixas (RN-SY-02, RN-CX-01/07/09/10).</summary>
public class SyncCaixasTests
{
    private static readonly DateTime Abertura = new(2026, 9, 17, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Fechamento = Abertura.AddHours(9);

    private readonly CaixaRepositoryFake _caixas = new();
    private readonly ComandaRepositoryFake _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly SyncService _service;
    private readonly Guid _atendente;
    private readonly Guid _dona;

    public SyncCaixasTests()
    {
        var atendente = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Abertura);
        var dona = Usuario.Criar("Dona Maria", "maria", "hash", PerfilUsuario.Admin, Abertura);
        _usuarios.Usuarios.AddRange([atendente, dona]);
        _atendente = atendente.Id;
        _dona = dona.Id;
        _service = new SyncService(_caixas, _comandas, _usuarios, _uow);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CaixaSyncDto Caixa(Guid? id = null, decimal fundo = 100m, FechamentoCaixaSyncDto? fechamento = null, params MovimentoCaixaSyncDto[] movimentos) =>
        new(id ?? Guid.NewGuid(), _atendente, Abertura, fundo, movimentos, fechamento);

    private MovimentoCaixaSyncDto Sangria(decimal valor, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "Sangria", valor, "depósito no banco", _atendente, Abertura.AddHours(2));

    private FechamentoCaixaSyncDto Fechar(decimal contado, decimal vendasDinheiroApp, decimal esperadoApp, Guid? por = null) =>
        new(por ?? _atendente, Fechamento, contado, vendasDinheiroApp, esperadoApp, contado - esperadoApp);

    private Task<SyncResponse> Enviar(params CaixaSyncDto[] caixas) => _service.ReceberCaixasAsync(new SyncCaixasRequest(caixas), Ct);

    [Fact]
    public async Task ReceberCaixas_CaixaAbertoNovo_AceitaComMovimentos()
    {
        var dto = Caixa(movimentos: Sangria(30m));

        var resposta = await Enviar(dto);

        Assert.Equal(StatusSync.Aceita, Assert.Single(resposta.Resultados).Status);
        var caixa = Assert.Single(_caixas.Caixas);
        Assert.Equal(dto.Id, caixa.Id);
        Assert.True(caixa.EstaAberto);
        Assert.Equal(30m, caixa.TotalSangrias);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task ReceberCaixas_MesmoLoteDuasVezes_GravaUmaVezESegundaEhJaRecebida()
    {
        var dto = Caixa(movimentos: Sangria(30m));

        await Enviar(dto);
        var segunda = await Enviar(dto);

        Assert.Equal(StatusSync.JaRecebida, Assert.Single(segunda.Resultados).Status);
        Assert.Single(_caixas.Caixas);
        Assert.Single(_caixas.Caixas[0].Movimentos);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task ReceberCaixas_ReenvioComMovimentoNovo_AcrescentaENaoApagaOAntigo()
    {
        var id = Guid.NewGuid();
        var primeira = Sangria(30m);
        await Enviar(Caixa(id, movimentos: primeira));

        // O celular reenviou sem a primeira sangria e com uma nova: nada é apagado (RN-TD-03).
        var resposta = await Enviar(Caixa(id, movimentos: Sangria(20m)));

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        Assert.Equal(50m, _caixas.Caixas[0].TotalSangrias);
    }

    [Fact]
    public async Task ReceberCaixas_FechamentoComVendasRecebidas_UsaValoresDoServidorSemDivergencia()
    {
        var id = Guid.NewGuid();
        await Enviar(Caixa(id, fundo: 100m));
        AdicionarVendaEmDinheiro(id, 50m);

        var resposta = await Enviar(Caixa(id, fundo: 100m, fechamento: Fechar(contado: 150m, vendasDinheiroApp: 50m, esperadoApp: 150m)));

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        var caixa = _caixas.Caixas[0];
        Assert.Equal(StatusCaixa.Fechado, caixa.Status);
        Assert.Equal(50m, caixa.TotalVendasDinheiro);
        Assert.Equal(150m, caixa.ValorEsperado);
        Assert.Equal(0m, caixa.Diferenca);
        Assert.False(caixa.DivergenciaSincronizacao);
    }

    [Fact]
    public async Task ReceberCaixas_FechamentoComVendaQueAindaNaoChegou_AceitaEMarcaDivergencia()
    {
        var id = Guid.NewGuid();
        await Enviar(Caixa(id, fundo: 100m));
        AdicionarVendaEmDinheiro(id, 50m);

        // O celular vendeu 80 em dinheiro; ao servidor só chegaram 50 (RN-CX-10).
        var resposta = await Enviar(Caixa(id, fundo: 100m, fechamento: Fechar(contado: 180m, vendasDinheiroApp: 80m, esperadoApp: 180m)));

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        var caixa = _caixas.Caixas[0];
        Assert.True(caixa.DivergenciaSincronizacao);
        Assert.Equal(150m, caixa.ValorEsperado);
        Assert.Equal(30m, caixa.Diferenca);
        Assert.Equal(80m, caixa.TotalVendasDinheiroApp);
    }

    [Fact]
    public async Task ReceberCaixas_CaixaJaFechadoNaPrimeiraVez_Aceita()
    {
        var resposta = await Enviar(Caixa(fundo: 100m, fechamento: Fechar(100m, 0m, 100m), movimentos: Sangria(10m)));

        Assert.Equal(StatusSync.Aceita, resposta.Resultados[0].Status);
        Assert.Equal(StatusCaixa.Fechado, _caixas.Caixas[0].Status);
        Assert.True(_caixas.Caixas[0].DivergenciaSincronizacao); // esperado do servidor = 90 (100 − 10)
    }

    [Fact]
    public async Task ReceberCaixas_MesmoFechamentoReenviado_JaRecebida()
    {
        var dto = Caixa(fechamento: Fechar(100m, 0m, 100m));
        await Enviar(dto);

        var resposta = await Enviar(dto);

        Assert.Equal(StatusSync.JaRecebida, resposta.Resultados[0].Status);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task ReceberCaixas_ReenvioDaDataComPrecisaoDoBanco_NaoEhConsideradoDiferente()
    {
        var id = Guid.NewGuid();
        var comTicks = Abertura.AddTicks(1234567); // 7 casas decimais
        await _service.ReceberCaixasAsync(new SyncCaixasRequest([new CaixaSyncDto(id, _atendente, comTicks, 100m, [])]), Ct);
        // Simula o que volta do PostgreSQL (microssegundos): recria o caixa com a data arredondada.
        _caixas.Caixas.Clear();
        _caixas.Caixas.Add(GestaoSorveteria.Domain.Caixas.Caixa.Abrir(_atendente, 100m, comTicks.AddTicks(-7), id));

        var resposta = await _service.ReceberCaixasAsync(new SyncCaixasRequest([new CaixaSyncDto(id, _atendente, comTicks, 100m, [])]), Ct);

        Assert.Equal(StatusSync.JaRecebida, resposta.Resultados[0].Status);
    }

    [Fact]
    public async Task ReceberCaixas_FechamentoDoCelularDepoisDeFechamentoForcado_Rejeita()
    {
        var id = Guid.NewGuid();
        await Enviar(Caixa(id));
        _caixas.Caixas[0].Fechar(0m, 0m, _dona, Abertura.AddHours(12), "atendente esqueceu"); // RN-CX-09 pelo painel

        var resposta = await Enviar(Caixa(id, fechamento: Fechar(contado: 250m, vendasDinheiroApp: 150m, esperadoApp: 250m)));

        var resultado = resposta.Resultados[0];
        Assert.Equal(StatusSync.Rejeitada, resultado.Status);
        Assert.Contains("250,00", resultado.Motivo);
        Assert.Contains("RN-CX-09", resultado.Motivo);
        Assert.Equal(0m, _caixas.Caixas[0].ValorContado); // RN-CX-07: não muda
    }

    [Fact]
    public async Task ReceberCaixas_MovimentoNovoEmCaixaFechado_Rejeita()
    {
        var id = Guid.NewGuid();
        await Enviar(Caixa(id, fechamento: Fechar(100m, 0m, 100m)));

        var resposta = await Enviar(Caixa(id, fechamento: Fechar(100m, 0m, 100m), movimentos: Sangria(10m)));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("RN-CX-07", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberCaixas_OutroCaixaAberto_RejeitaRN_CX_01()
    {
        await Enviar(Caixa());

        var resposta = await Enviar(Caixa());

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("RN-CX-01", resposta.Resultados[0].Motivo);
        Assert.Single(_caixas.Caixas);
    }

    [Fact]
    public async Task ReceberCaixas_AberturaDiferenteDaRecebida_Rejeita()
    {
        var id = Guid.NewGuid();
        await Enviar(Caixa(id, fundo: 100m));

        var resposta = await Enviar(Caixa(id, fundo: 120m));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Equal(100m, _caixas.Caixas[0].FundoTroco);
    }

    [Fact]
    public async Task ReceberCaixas_LoteComUmInvalido_OsOutrosSaoAceitos()
    {
        var fechadoA = Caixa(fechamento: Fechar(100m, 0m, 100m));
        var invalido = Caixa(fundo: -5m, fechamento: Fechar(0m, 0m, 0m));
        var fechadoB = Caixa(fechamento: Fechar(100m, 0m, 100m));

        var resposta = await Enviar(fechadoA, invalido, fechadoB);

        Assert.Equal([StatusSync.Aceita, StatusSync.Rejeitada, StatusSync.Aceita], resposta.Resultados.Select(r => r.Status));
        Assert.Equal([fechadoA.Id, invalido.Id, fechadoB.Id], resposta.Resultados.Select(r => r.Id));
        Assert.NotNull(resposta.Resultados[1].Motivo);
        Assert.Equal(2, _caixas.Caixas.Count);
        Assert.Equal(1, _uow.Descartes);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("Retirada")]
    [InlineData("")]
    public async Task ReceberCaixas_TipoDeMovimentoDesconhecido_Rejeita(string tipo)
    {
        var movimento = new MovimentoCaixaSyncDto(Guid.NewGuid(), tipo, 10m, "motivo", _atendente, Abertura.AddHours(1));

        var resposta = await Enviar(Caixa(movimentos: movimento));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Empty(_caixas.Caixas);
    }

    [Fact]
    public async Task ReceberCaixas_UsuarioInexistente_Rejeita()
    {
        var dto = Caixa() with { AbertoPorUsuarioId = Guid.NewGuid() };

        var resposta = await Enviar(dto);

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("Usuário não encontrado", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberCaixas_ItemNuloOuMovimentosNulos_NaoDerrubaOLote()
    {
        var semMovimentos = Caixa() with { Movimentos = null! };

        var resposta = await _service.ReceberCaixasAsync(new SyncCaixasRequest([null!, semMovimentos]), Ct);

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Equal(StatusSync.Aceita, resposta.Resultados[1].Status);
    }

    [Fact]
    public async Task ReceberCaixas_SangriaNovaValidaEFechamentoInvalido_RejeitaSemConfirmarNada()
    {
        var id = Guid.NewGuid();
        await Enviar(Caixa(id));
        var fechamentoAntesDaAbertura = Fechar(100m, 0m, 100m) with { FechadoEm = Abertura.AddMinutes(-1) };

        var resposta = await Enviar(Caixa(id, fechamento: fechamentoAntesDaAbertura, movimentos: Sangria(10m)));

        // Nada é confirmado: com o EF, DescartarAlteracoes limpa a sangria já aplicada na memória.
        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Equal(1, _uow.Confirmacoes);
        Assert.Equal(1, _uow.Descartes);
    }

    [Fact]
    public async Task ReceberCaixas_BancoFalhaNoMeioDoLote_PropagaEMantemOsAnteriores()
    {
        _uow.FalharNaConfirmacao = 2;
        var primeiro = Caixa(fechamento: Fechar(100m, 0m, 100m));
        var segundo = Caixa(fechamento: Fechar(100m, 0m, 100m));

        // Erro de infraestrutura não vira "rejeitada": a requisição falha e o app reenvia o lote (idempotente).
        await Assert.ThrowsAsync<InvalidOperationException>(() => Enviar(primeiro, segundo));
        Assert.Equal(1, _uow.Confirmacoes);

        _uow.FalharNaConfirmacao = null;
        var reenvio = await Enviar(primeiro, segundo);
        Assert.Equal(StatusSync.JaRecebida, reenvio.Resultados[0].Status);
    }

    [Fact]
    public async Task ReceberCaixas_IdDeMovimentoJaUsadoEmOutroCaixa_RejeitaSemTravarAFila()
    {
        var movimentoId = Guid.NewGuid();
        await Enviar(Caixa(fechamento: Fechar(90m, 0m, 90m), movimentos: Sangria(10m, movimentoId)));

        var resposta = await Enviar(Caixa(movimentos: Sangria(10m, movimentoId)));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Contains("outro caixa", resposta.Resultados[0].Motivo);
    }

    [Fact]
    public async Task ReceberCaixas_MovimentoRepetidoNoMesmoEnvio_Rejeita()
    {
        var movimento = Sangria(10m);

        var resposta = await Enviar(Caixa(movimentos: [movimento, movimento]));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
    }

    [Fact]
    public async Task ReceberCaixas_MovimentoReenviadoComOutroValor_Rejeita()
    {
        var id = Guid.NewGuid();
        var movimentoId = Guid.NewGuid();
        await Enviar(Caixa(id, movimentos: Sangria(10m, movimentoId)));

        var resposta = await Enviar(Caixa(id, movimentos: Sangria(99m, movimentoId)));

        Assert.Equal(StatusSync.Rejeitada, resposta.Resultados[0].Status);
        Assert.Equal(10m, _caixas.Caixas[0].TotalSangrias);
    }

    [Fact]
    public async Task ReceberCaixas_MovimentoAntesDaAberturaOuDepoisDoFechamento_Rejeita()
    {
        var antes = Sangria(10m) with { Em = Abertura.AddMinutes(-1) };
        var depois = Sangria(10m) with { Em = Fechamento.AddMinutes(1) };

        var resposta = await Enviar(
            Caixa(fechamento: Fechar(90m, 0m, 90m), movimentos: antes),
            Caixa(fechamento: Fechar(90m, 0m, 90m), movimentos: depois));

        Assert.All(resposta.Resultados, r => Assert.Equal(StatusSync.Rejeitada, r.Status));
        Assert.Empty(_caixas.Caixas);
    }

    private void AdicionarVendaEmDinheiro(Guid caixaId, decimal valor)
    {
        var caixa = _caixas.Caixas.Single(c => c.Id == caixaId);
        var comanda = Comanda.Abrir(caixa, _atendente, _comandas.Comandas.Count + 1, TipoComanda.Balcao, Abertura.AddHours(1), Abertura.AddHours(1));
        comanda.AdicionarItemLivre("Venda avulsa", valor);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, valor, valor)], Abertura.AddHours(1));
        _comandas.Comandas.Add(comanda);
    }
}
