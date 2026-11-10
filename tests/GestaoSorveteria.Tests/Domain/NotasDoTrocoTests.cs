using GestaoSorveteria.Domain.Comandas;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>RN-CM-13: botões de "quanto o cliente entregou" no dinheiro do balcão.</summary>
public class NotasDoTrocoTests
{
    private static decimal[] Valores(decimal total) => NotasDoTroco.Para(total).Select(n => n.Valor).ToArray();

    [Fact]
    public void Para_TotalQuebrado_SugereExatoArredondadosENotas()
    {
        Assert.Equal(new[] { 23.50m, 25m, 30m, 50m, 100m }, Valores(23.50m));
    }

    [Fact]
    public void Para_TotalRedondo_NaoRepete()
    {
        Assert.Equal(new[] { 18m, 20m, 50m, 100m, 200m }, Valores(18m));
        Assert.Equal(new[] { 20m, 50m, 100m, 200m }, Valores(20m));
    }

    [Fact]
    public void Para_TotalAcimaDe200_SoExatoEArredondados()
    {
        Assert.Equal(new[] { 212.30m, 215m, 220m }, Valores(212.30m));
    }

    [Fact]
    public void Para_ExatoMarcadoNoTexto()
    {
        var notas = NotasDoTroco.Para(7.50m);

        Assert.StartsWith("Exato", notas[0].Texto);
        Assert.DoesNotContain(notas.Skip(1), n => n.Texto.StartsWith("Exato"));
    }

    [Fact]
    public void Para_Zero_Vazio()
    {
        Assert.Empty(NotasDoTroco.Para(0m));
    }
}
