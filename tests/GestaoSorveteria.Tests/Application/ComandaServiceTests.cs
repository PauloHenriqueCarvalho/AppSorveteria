using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Comandas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Comandas;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Usuarios;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

public class ComandaServiceTests
{
    private static readonly DateTime Venda = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly CaixasEmMemoria _caixas = new();
    private readonly ComandasEmMemoria _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Venda.AddHours(2) };
    private readonly ComandaService _service;
    private readonly Usuario _dona = Cenario.Admin();
    private readonly Caixa _caixa;

    public ComandaServiceTests()
    {
        _usuarios.Usuarios.Add(_dona);
        var consulta = new ComandaConsultaService(_comandas, _usuarios, _clock);
        _service = new ComandaService(_comandas, _usuarios, _uow, _clock, consulta);
        _caixa = Caixa.Abrir(_dona.Id, 100m, Venda.AddHours(-1));
        _caixas.Caixas.Add(_caixa);
    }

    private Comanda VendaEmDinheiro(decimal valor = 10m)
    {
        var comanda = Comanda.Abrir(_caixa, Cenario.Atendente, 1, TipoComanda.Balcao, Venda, Venda);
        comanda.AdicionarItemLivre("Venda avulsa", valor);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, valor, valor)], Venda);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    [Fact]
    public async Task Estornar_Admin_VendaFechada_EstornaEConfirma()
    {
        var venda = VendaEmDinheiro();

        var detalhe = await _service.EstornarAsync(venda.Id, new EstornarComandaRequest("Cobrado duas vezes"), _dona.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Estornada", detalhe.Status);
        Assert.Equal("Cobrado duas vezes", detalhe.MotivoEstorno);
        Assert.Equal("Dona Maria", detalhe.EstornadaPorNome);
        Assert.Equal(_clock.UtcNow, detalhe.EstornadaEm);
        Assert.Equal(Venda, detalhe.FechadaEm);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Estornar_NaoAltera_EsperadoDoCaixa()
    {
        // RN-CM-09 / RN-CX-07: o dinheiro entrou na gaveta; devolução ao cliente é sangria, registrada à parte.
        var venda = VendaEmDinheiro(10m);
        var consultaCaixas = new CaixaConsultaService(_caixas, _comandas, _usuarios, _clock);
        var antes = (await consultaCaixas.ObterAtualAsync(TestContext.Current.CancellationToken))!.ValorEsperado;

        await _service.EstornarAsync(venda.Id, new EstornarComandaRequest("Cliente desistiu"), _dona.Id, TestContext.Current.CancellationToken);

        var depois = await consultaCaixas.ObterAtualAsync(TestContext.Current.CancellationToken);
        Assert.Equal(110m, antes);
        Assert.Equal(antes, depois!.ValorEsperado);
        Assert.Equal(10m, await _comandas.TotalDinheiroFechadasNoCaixaAsync(_caixa.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Estornar_Atendente_LancaAcessoNegado()
    {
        var atendente = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Venda);
        _usuarios.Usuarios.Add(atendente);
        var venda = VendaEmDinheiro();

        await Assert.ThrowsAsync<AcessoNegadoException>(() =>
            _service.EstornarAsync(venda.Id, new EstornarComandaRequest("motivo"), atendente.Id, TestContext.Current.CancellationToken));
        Assert.Equal(StatusComanda.Fechada, venda.Status);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Estornar_VendaInexistente_LancaNaoEncontrada()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.EstornarAsync(Guid.NewGuid(), new EstornarComandaRequest("motivo"), _dona.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Estornar_DuasVezes_LancaSemGravar()
    {
        var venda = VendaEmDinheiro();
        await _service.EstornarAsync(venda.Id, new EstornarComandaRequest("primeira"), _dona.Id, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.EstornarAsync(venda.Id, new EstornarComandaRequest("segunda"), _dona.Id, TestContext.Current.CancellationToken));
        Assert.Equal(1, _uow.Confirmacoes);
        Assert.Equal("primeira", venda.MotivoEstorno);
    }
}
