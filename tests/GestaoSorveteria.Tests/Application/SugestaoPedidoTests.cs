using GestaoSorveteria.Application.Pedidos;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Contas do pedido sugerido (RN-PF-02, RN-ES-07).</summary>
public class SugestaoPedidoTests
{
    private static readonly DateTime Domingo = new(2026, 10, 4, 23, 0, 0, DateTimeKind.Utc);

    private static SugestaoPedido.Contado Contado(int semanas, int unidades) => new(Domingo.AddDays(7 * semanas), unidades);

    [Fact]
    public void SaidaSemanalMedia_ContagensComEntregaNoMeio_SomaORecebido()
    {
        // 70 → (recebeu 56) → 84: saiu 42 na semana. Depois 84 → 42: saiu 42.
        var media = SugestaoPedido.SaidaSemanalMedia(
            [Contado(0, 70), Contado(1, 84), Contado(2, 42)],
            (de, ate) => de == Domingo ? 56 : 0);

        Assert.Equal(42m, media);
    }

    [Fact]
    public void SaidaSemanalMedia_IntervaloNegativo_FicaDeFora()
    {
        // 30 → 40 sem entrega: contou mais do que devia ("conferir"), não entra na média.
        var media = SugestaoPedido.SaidaSemanalMedia([Contado(0, 30), Contado(1, 40), Contado(2, 26)], (_, _) => 0);

        Assert.Equal(14m, media);
    }

    [Fact]
    public void SaidaSemanalMedia_SoAsQuatroUltimasSemanasEIntervaloDeOutroTamanhoViraPorSemana()
    {
        var contagens = new[] { Contado(0, 1000), Contado(1, 990), Contado(2, 980), Contado(3, 970), Contado(4, 960), Contado(5, 950) };
        Assert.Equal(10m, SugestaoPedido.SaidaSemanalMedia(contagens, (_, _) => 0));

        // 14 dias com saída de 28 = 14 por semana.
        var quinzena = new[] { new SugestaoPedido.Contado(Domingo, 50), new SugestaoPedido.Contado(Domingo.AddDays(14), 22) };
        Assert.Equal(14m, SugestaoPedido.SaidaSemanalMedia(quinzena, (_, _) => 0));
    }

    [Fact]
    public void SaidaSemanalMedia_SemHistorico_Nulo()
    {
        Assert.Null(SugestaoPedido.SaidaSemanalMedia([Contado(0, 30)], (_, _) => 0));
    }

    [Fact]
    public void DiasACobrir_PedidoToDaSegundaEntregaEmDoisDias_NoveDias()
    {
        Assert.Equal(9, SugestaoPedido.DiasACobrir(DayOfWeek.Monday, [DayOfWeek.Monday], 2));
        Assert.Equal(3, SugestaoPedido.DiasACobrir(DayOfWeek.Monday, [DayOfWeek.Monday, DayOfWeek.Thursday], 0));
        Assert.Equal(2, SugestaoPedido.DiasACobrir(DayOfWeek.Sunday, [DayOfWeek.Monday], 1));
    }

    [Theory]
    [InlineData(42.0, 9, 20, null, 34.0)]   // 42/semana × 9 dias − 20 = 34
    [InlineData(null, 9, 20, 56, 36.0)]     // sem histórico: alvo 56 − 20
    [InlineData(null, 9, 20, null, 0.0)]    // sem histórico e sem alvo
    [InlineData(7.0, 7, 30, null, 0.0)]     // tem de sobra: 0, nunca negativo
    public void NecessidadeEmUnidades_CasosDaRegra(double? media, int dias, int contado, int? alvo, double esperado)
    {
        Assert.Equal((decimal)esperado, SugestaoPedido.NecessidadeEmUnidades((decimal?)media, dias, contado, alvo));
    }

    [Theory]
    [InlineData(34.0, 28, 1.0, 2.0)]   // 1,2 caixa → 2 caixas
    [InlineData(34.0, 28, 0.5, 1.5)]   // aceita meia caixa → 1,5
    [InlineData(28.0, 28, 1.0, 1.0)]
    [InlineData(0.0, 28, 1.0, 0.0)]
    [InlineData(1.0, 1, 1.0, 1.0)]     // balde
    public void EmEmbalagens_ArredondaParaCimaNoPasso(double unidades, int porEmbalagem, double passo, double esperado)
    {
        Assert.Equal((decimal)esperado, SugestaoPedido.EmEmbalagens((decimal)unidades, porEmbalagem, (decimal)passo));
    }
}
