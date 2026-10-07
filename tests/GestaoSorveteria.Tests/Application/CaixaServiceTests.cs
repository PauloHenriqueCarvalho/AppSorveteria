using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Caixas;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

/// <summary>RN-CX-09: fechamento forçado pelo painel.</summary>
public class CaixaServiceTests
{
    private static readonly DateTime Abertura = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly CaixasEmMemoria _caixas = new();
    private readonly ComandasEmMemoria _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Abertura.AddHours(10) };
    private readonly CaixaService _service;
    private readonly Guid _dona;

    public CaixaServiceTests()
    {
        var admin = Cenario.Admin();
        _dona = admin.Id;
        _usuarios.Usuarios.Add(admin);
        var consulta = new CaixaConsultaService(_caixas, _comandas, _usuarios, _clock);
        _service = new CaixaService(_caixas, _comandas, consulta, _uow, _clock);
    }

    private Caixa CaixaAberto(decimal fundo = 100m)
    {
        var caixa = Caixa.Abrir(Cenario.Atendente, fundo, Abertura);
        _caixas.Caixas.Add(caixa);
        return caixa;
    }

    private void VendaFechada(Caixa caixa, int numero, params DadosPagamento[] pagamentos)
    {
        var comanda = Comanda.Abrir(caixa, Cenario.Atendente, numero, TipoComanda.Balcao, Abertura, Abertura);
        comanda.AdicionarItemLivre("Venda avulsa", pagamentos.Sum(p => p.Valor));
        comanda.Fechar(pagamentos, Abertura.AddMinutes(numero));
        _comandas.Comandas.Add(comanda);
    }

    [Fact]
    public async Task FecharForcado_CaixaAbertoComVendas_FechaComContadoZeroEDiferencaNegativa()
    {
        var caixa = CaixaAberto(fundo: 100m);
        caixa.RegistrarSangria(30m, "Depósito", Cenario.Atendente, Abertura.AddHours(1));
        VendaFechada(caixa, 1, new DadosPagamento(FormaPagamento.Dinheiro, 12.50m, 20m));
        VendaFechada(caixa, 2, new DadosPagamento(FormaPagamento.Pix, 18.90m));

        var detalhe = await _service.FecharForcadoAsync(caixa.Id, new FecharCaixaForcadoRequest("Atendente esqueceu de fechar"), _dona, TestContext.Current.CancellationToken);

        var resumo = detalhe.Resumo;
        Assert.Equal("Fechado", resumo.Status);
        Assert.True(resumo.FechamentoForcado);
        Assert.Equal(12.50m, resumo.TotalVendasDinheiro);  // só o dinheiro conta na gaveta
        Assert.Equal(82.50m, resumo.ValorEsperado);        // 100 + 12,50 − 30 (RN-CX-06)
        Assert.Equal(0m, resumo.ValorContado);             // RN-CX-09: ninguém contou
        Assert.Equal(-82.50m, resumo.Diferenca);
        Assert.Equal("Dona Maria", resumo.FechadoPorNome);
        Assert.Equal(_clock.UtcNow, resumo.FechadoEm);
        Assert.Contains("Atendente esqueceu de fechar", detalhe.Observacao);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task FecharForcado_CaixaJaFechado_LancaENaoMudaNada()
    {
        var caixa = CaixaAberto();
        caixa.Fechar(100m, 0m, Cenario.Atendente, Abertura.AddHours(8));

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.FecharForcadoAsync(caixa.Id, new FecharCaixaForcadoRequest("Teste"), _dona, TestContext.Current.CancellationToken));

        Assert.Equal(100m, caixa.ValorContado); // RN-CX-07
        Assert.False(caixa.FechamentoForcado);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task FecharForcado_MotivoCurto_Lanca()
    {
        var caixa = CaixaAberto();

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.FecharForcadoAsync(caixa.Id, new FecharCaixaForcadoRequest("ok"), _dona, TestContext.Current.CancellationToken));

        Assert.True(caixa.EstaAberto);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task FecharForcado_CaixaInexistente_Lanca404()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.FecharForcadoAsync(Guid.NewGuid(), new FecharCaixaForcadoRequest("Teste de caixa"), _dona, TestContext.Current.CancellationToken));
    }
}
