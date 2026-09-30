using GestaoSorveteria.Application.Common;

namespace GestaoSorveteria.Tests.Application;

public class DiaComercialTests
{
    [Fact]
    public void DataDe_MadrugadaUtc_AindaEhODiaAnteriorNoBrasil()
    {
        // 01:30 UTC do dia 18 = 22:30 do dia 17 em Brasília (UTC−3)
        var utc = new DateTime(2026, 9, 18, 1, 30, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 9, 17), DiaComercial.DataDe(utc));
    }

    [Fact]
    public void DataDe_TardeUtc_EhOMesmoDia()
    {
        var utc = new DateTime(2026, 9, 17, 18, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 9, 17), DiaComercial.DataDe(utc));
    }

    [Fact]
    public void IntervaloUtc_DeUmDia_ComecaAs3hUtc()
    {
        var (inicio, fim) = DiaComercial.IntervaloUtc(new DateOnly(2026, 9, 17));

        Assert.Equal(new DateTime(2026, 9, 17, 3, 0, 0, DateTimeKind.Utc), inicio);
        Assert.Equal(new DateTime(2026, 9, 18, 3, 0, 0, DateTimeKind.Utc), fim);
        Assert.Equal(DateTimeKind.Utc, inicio.Kind);
    }

    [Fact]
    public void IntervaloUtc_DeVariosDias_CobreAteOFimDoUltimo()
    {
        var (inicio, fim) = DiaComercial.IntervaloUtc(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

        Assert.Equal(new DateTime(2026, 9, 1, 3, 0, 0, DateTimeKind.Utc), inicio);
        Assert.Equal(new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc), fim);
    }

    [Fact]
    public void IntervaloUtc_UltimoDiaAntesDoPrimeiro_Lanca()
    {
        Assert.Throws<ArgumentException>(() =>
            DiaComercial.IntervaloUtc(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1)));
    }
}
