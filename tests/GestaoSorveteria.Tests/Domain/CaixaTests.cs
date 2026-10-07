using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Tests.Domain;

public class CaixaTests
{
    [Fact]
    public void Abrir_ComFundoDeTroco_NasceAberto()
    {
        var caixa = Caixa.Abrir(Cenario.Atendente, 100m, Cenario.Agora);

        Assert.Equal(StatusCaixa.Aberto, caixa.Status);
        Assert.True(caixa.EstaAberto);
        Assert.Equal(100m, caixa.FundoTroco);
        Assert.Equal(Cenario.Atendente, caixa.AbertoPorUsuarioId);
        Assert.Null(caixa.FechadoEm);
        Assert.Empty(caixa.Movimentos);
    }

    [Fact]
    public void Abrir_FundoNegativo_Lanca()
    {
        Assert.Throws<DomainException>(() => Caixa.Abrir(Cenario.Atendente, -1m, Cenario.Agora));
    }

    [Fact]
    public void Abrir_FundoZero_Permitido()
    {
        var caixa = Caixa.Abrir(Cenario.Atendente, 0m, Cenario.Agora);

        Assert.Equal(0m, caixa.FundoTroco);
    }

    [Fact]
    public void SangriaESuprimento_SomamSeparados()
    {
        var caixa = Cenario.CaixaAberto();

        caixa.RegistrarSangria(80m, "levar ao banco", Cenario.Atendente, Cenario.Agora);
        caixa.RegistrarSuprimento(50m, "troco extra", Cenario.Dona, Cenario.Agora);
        caixa.RegistrarSangria(20m, "pagar entregador", Cenario.Atendente, Cenario.Agora);

        Assert.Equal(3, caixa.Movimentos.Count);
        Assert.Equal(100m, caixa.TotalSangrias);
        Assert.Equal(50m, caixa.TotalSuprimentos);
    }

    [Fact]
    public void Sangria_ValorZero_Lanca()
    {
        var caixa = Cenario.CaixaAberto();

        Assert.Throws<DomainException>(() => caixa.RegistrarSangria(0m, "motivo", Cenario.Atendente, Cenario.Agora));
    }

    [Fact]
    public void Sangria_SemMotivo_Lanca()
    {
        var caixa = Cenario.CaixaAberto();

        Assert.Throws<DomainException>(() => caixa.RegistrarSangria(10m, "", Cenario.Atendente, Cenario.Agora));
    }

    [Fact]
    public void Movimento_ComMesmoId_EhIdempotente()
    {
        var caixa = Cenario.CaixaAberto();
        var id = Guid.NewGuid();

        var primeiro = caixa.RegistrarSangria(10m, "motivo", Cenario.Atendente, Cenario.Agora, id);
        var segundo = caixa.RegistrarSangria(10m, "motivo", Cenario.Atendente, Cenario.Agora, id);

        Assert.Same(primeiro, segundo);
        Assert.Single(caixa.Movimentos);
        Assert.Equal(10m, caixa.TotalSangrias);
    }

    [Fact]
    public void Fechar_CalculaEsperadoEDiferenca()
    {
        // esperado = fundo 100 + vendas em dinheiro 250,50 + suprimento 50 − sangria 80 = 320,50
        var caixa = Cenario.CaixaAberto(100m);
        caixa.RegistrarSuprimento(50m, "troco extra", Cenario.Dona, Cenario.Agora);
        caixa.RegistrarSangria(80m, "levar ao banco", Cenario.Atendente, Cenario.Agora);

        Assert.Equal(320.50m, caixa.CalcularEsperado(250.50m));

        caixa.Fechar(valorContado: 300m, totalVendasDinheiro: 250.50m, Cenario.Atendente, Cenario.Agora.AddHours(8), "faltou troco");

        Assert.Equal(StatusCaixa.Fechado, caixa.Status);
        Assert.Equal(250.50m, caixa.TotalVendasDinheiro);
        Assert.Equal(320.50m, caixa.ValorEsperado);
        Assert.Equal(300m, caixa.ValorContado);
        Assert.Equal(-20.50m, caixa.Diferenca);
        Assert.Equal("faltou troco", caixa.Observacao);
        Assert.Equal(Cenario.Atendente, caixa.FechadoPorUsuarioId);
        Assert.Equal(Cenario.Agora.AddHours(8), caixa.FechadoEm);
    }

    [Fact]
    public void Fechar_SemVendasNemMovimentos_EsperadoEhOFundo()
    {
        var caixa = Cenario.CaixaAberto(50m);

        caixa.Fechar(50m, 0m, Cenario.Atendente, Cenario.Agora);

        Assert.Equal(50m, caixa.ValorEsperado);
        Assert.Equal(0m, caixa.Diferenca);
    }

    [Fact]
    public void Fechar_DuasVezes_Lanca()
    {
        var caixa = Cenario.CaixaAberto();
        caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora);

        Assert.Throws<DomainException>(() => caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora));
    }

    [Fact]
    public void Fechar_DataAnteriorAAbertura_Lanca()
    {
        var caixa = Cenario.CaixaAberto();

        Assert.Throws<DomainException>(() => caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora.AddMinutes(-1)));
    }

    [Fact]
    public void FecharForcado_ContadoZeroEDiferencaNegativa()
    {
        // RN-CX-09
        var caixa = Cenario.CaixaAberto(fundoTroco: 100m);
        caixa.RegistrarSuprimento(20m, "troco extra", Cenario.Atendente, Cenario.Agora.AddHours(1));

        caixa.FecharForcado(totalVendasDinheiro: 55.50m, Cenario.Dona, Cenario.Agora.AddHours(12), "Atendente esqueceu de fechar");

        Assert.Equal(StatusCaixa.Fechado, caixa.Status);
        Assert.True(caixa.FechamentoForcado);
        Assert.Equal(0m, caixa.ValorContado);
        Assert.Equal(55.50m, caixa.TotalVendasDinheiro);
        Assert.Equal(175.50m, caixa.ValorEsperado); // 100 + 55,50 + 20 (RN-CX-06)
        Assert.Equal(-175.50m, caixa.Diferenca);
        Assert.Equal(Cenario.Dona, caixa.FechadoPorUsuarioId);
        Assert.Equal("Fechamento forçado pelo painel: Atendente esqueceu de fechar", caixa.Observacao);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ok")]
    public void FecharForcado_SemMotivo_Lanca(string motivo)
    {
        var caixa = Cenario.CaixaAberto();

        Assert.Throws<DomainException>(() => caixa.FecharForcado(0m, Cenario.Dona, Cenario.Agora, motivo));
        Assert.True(caixa.EstaAberto);
        Assert.False(caixa.FechamentoForcado);
    }

    [Fact]
    public void FecharForcado_CaixaJaFechado_Lanca()
    {
        var caixa = Cenario.CaixaAberto();
        caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora);

        // RN-CX-07: fechado é imutável, inclusive para o fechamento forçado.
        Assert.Throws<DomainException>(() => caixa.FecharForcado(0m, Cenario.Dona, Cenario.Agora, "Teste de fechamento"));
        Assert.Equal(100m, caixa.ValorContado);
        Assert.False(caixa.FechamentoForcado);
    }

    [Fact]
    public void Fechar_Normal_NaoMarcaFechamentoForcado()
    {
        var caixa = Cenario.CaixaAberto();
        caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora);

        Assert.False(caixa.FechamentoForcado);
    }

    [Fact]
    public void Movimento_DepoisDeFechado_Lanca()
    {
        var caixa = Cenario.CaixaAberto();
        caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora);

        Assert.Throws<DomainException>(() => caixa.RegistrarSangria(10m, "motivo", Cenario.Atendente, Cenario.Agora));
        Assert.Throws<DomainException>(() => caixa.RegistrarSuprimento(10m, "motivo", Cenario.Atendente, Cenario.Agora));
    }
}
