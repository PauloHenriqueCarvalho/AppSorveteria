using GestaoSorveteria.Application.Comandas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Comandas;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>POST /api/comandas/{id}/estornar (RN-CM-09, RN-US-01).</summary>
public class EstornoServiceTests
{
    private static readonly DateTime Agora = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly ComandaRepositoryFake _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Agora.AddHours(1) };
    private readonly EstornoService _service;
    private readonly Usuario _dona = Usuario.Criar("Dona Maria", "maria", "hash", PerfilUsuario.Admin, Agora);
    private readonly Usuario _atendente = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Agora);
    private readonly Caixa _caixa;

    public EstornoServiceTests()
    {
        _usuarios.Usuarios.AddRange([_dona, _atendente]);
        _caixa = Caixa.Abrir(_atendente.Id, 100m, Agora);
        _service = new EstornoService(_comandas, _usuarios, _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Comanda VendaFechada()
    {
        var comanda = Comanda.Abrir(_caixa, _atendente.Id, _comandas.Comandas.Count + 1, TipoComanda.Balcao, Agora, Agora);
        comanda.AdicionarItemLivre("Venda avulsa", 20m);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 20m, 50m)], Agora);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    [Fact]
    public async Task Estornar_PelaDona_ViraEstornadaEConfirma()
    {
        var comanda = VendaFechada();

        var resposta = await _service.EstornarAsync(comanda.Id, new EstornarComandaRequest("cobrado em duplicidade"), _dona.Id, Ct);

        Assert.Equal("Estornada", resposta.Status);
        Assert.Equal(_clock.UtcNow, resposta.EstornadaEmUtc);
        Assert.Equal(_dona.Id, comanda.EstornadaPorUsuarioId);
        Assert.Equal(1, _uow.Confirmacoes);
        Assert.True(_caixa.EstaAberto); // o caixa não é tocado
        Assert.Empty(_caixa.Movimentos);
    }

    [Fact]
    public async Task Estornar_PeloAtendente_AcessoNegado()
    {
        var comanda = VendaFechada();

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _service.EstornarAsync(comanda.Id, new EstornarComandaRequest("motivo"), _atendente.Id, Ct));
        Assert.Equal(StatusComanda.Fechada, comanda.Status);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Estornar_DonaDesativada_AcessoNegado()
    {
        _dona.Desativar();
        var comanda = VendaFechada();

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _service.EstornarAsync(comanda.Id, new EstornarComandaRequest("motivo"), _dona.Id, Ct));
    }

    [Fact]
    public async Task Estornar_ComandaInexistente_NaoEncontrada()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.EstornarAsync(Guid.NewGuid(), new EstornarComandaRequest("motivo"), _dona.Id, Ct));
    }

    [Fact]
    public async Task Estornar_DuasVezes_RegraDeNegocio()
    {
        var comanda = VendaFechada();
        await _service.EstornarAsync(comanda.Id, new EstornarComandaRequest("primeiro"), _dona.Id, Ct);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.EstornarAsync(comanda.Id, new EstornarComandaRequest("segundo"), _dona.Id, Ct));
        Assert.Equal(1, _uow.Confirmacoes);
    }
}
