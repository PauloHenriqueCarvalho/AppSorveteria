using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Tests.Domain;

public class MoedaTests
{
    [Theory]
    [InlineData(2.345, 2.35)]   // arredondamento comercial (banker's daria 2,34)
    [InlineData(2.344, 2.34)]
    [InlineData(0.005, 0.01)]
    [InlineData(-1.005, -1.01)]
    [InlineData(10, 10)]
    public void Arredondar_DuasCasasAwayFromZero(double valor, double esperado)
    {
        Assert.Equal((decimal)esperado, Moeda.Arredondar((decimal)valor));
    }

    [Fact]
    public void Dinheiro_AcimaDoMaximo_Lanca()
    {
        // RN-TD-02: acima disso não cabe na coluna numeric(12,2) — o banco daria erro 500 e travaria a sincronização.
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<GestaoSorveteria.Domain.Common.DomainException>(() => comanda.AdicionarItemLivre("Venda avulsa", Moeda.ValorMaximo + 0.01m));
        comanda.AdicionarItemLivre("Venda avulsa", Moeda.ValorMaximo);
        Assert.Equal(Moeda.ValorMaximo, comanda.Total);
    }

    [Fact]
    public void TemNoMaximoDuasCasas()
    {
        Assert.True(Moeda.TemNoMaximoDuasCasas(12.50m));
        Assert.True(Moeda.TemNoMaximoDuasCasas(12m));
        Assert.False(Moeda.TemNoMaximoDuasCasas(12.505m));
    }
}
