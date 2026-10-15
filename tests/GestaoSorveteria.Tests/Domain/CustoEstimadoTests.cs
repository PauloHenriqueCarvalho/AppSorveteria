using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>RN-PR-07: custo estimado de uma unidade vendida, pelo catálogo de estoque ou informado no produto.</summary>
public class CustoEstimadoTests
{
    private static readonly Guid Fabrica = Guid.NewGuid();
    private readonly GrupoEstoque _leite = GrupoEstoque.Criar("Picolé ao leite", ModoControle.Contagem, 1);
    private readonly GrupoEstoque _potes = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 2);

    private ItemEstoque Caixa(string nome, decimal custo) =>
        ItemEstoque.Criar(_leite.Id, Fabrica, nome, UnidadeCompra.Caixa, 28, custo, 1);

    private ItemEstoque Pote(string nome, decimal custo) =>
        ItemEstoque.Criar(_potes.Id, Fabrica, nome, UnidadeCompra.Unidade, 1, custo, 1);

    private static Produto NovoProduto(string nome, decimal preco, Guid? grupo = null, int unidades = 1)
    {
        var produto = Produto.Criar(nome, "Teste", preco, false, 1, Cenario.Agora);
        if (grupo is not null)
        {
            produto.LigarAoEstoque(grupo, unidades, Cenario.Agora);
        }

        return produto;
    }

    [Fact]
    public void PorUnidadeVendida_ProdutoLigadoSemSabor_MediaDosItensAtivosDoGrupo()
    {
        var picole = NovoProduto("Picolé ao leite", 5m, _leite.Id);
        var desativado = Caixa("Uva", 56m);
        desativado.Desativar();

        var custo = CustoEstimado.PorUnidadeVendida(picole, [Caixa("Coco", 25.20m), Caixa("Milho", 28m), desativado]);

        // (0,90 + 1,00) ÷ 2 = 0,95; o desativado (2,00) não entra.
        Assert.Equal((0.95m, CustoEstimado.Origem.Estoque), custo);
    }

    [Fact]
    public void PorUnidadeVendida_PoteComSabor_CustoDoSaborVezesUnidadesPorVenda()
    {
        var doisPotes = NovoProduto("2 potes de 1L", 50m, _potes.Id, unidades: 2);
        var flocos = Pote("Flocos", 14m);

        var custo = CustoEstimado.PorUnidadeVendida(doisPotes, [flocos, Pote("Chocolate", 16m)], flocos);

        Assert.Equal((28m, CustoEstimado.Origem.Estoque), custo);
    }

    [Fact]
    public void PorUnidadeVendida_SaborSemCusto_UsaAMediaDoGrupo()
    {
        var pote = NovoProduto("Pote 1L", 28m, _potes.Id);
        var semCusto = Pote("Novo sabor", 0m);

        var custo = CustoEstimado.PorUnidadeVendida(pote, [semCusto, Pote("Flocos", 14m), Pote("Chocolate", 15m)], semCusto);

        Assert.Equal(14.50m, custo?.Custo);
    }

    [Fact]
    public void PorUnidadeVendida_GrupoSemCusto_UsaOCustoInformado()
    {
        var picole = NovoProduto("Picolé ao leite", 5m, _leite.Id);
        picole.InformarCusto(1.10m);

        var custo = CustoEstimado.PorUnidadeVendida(picole, [Caixa("Coco", 0m)]);

        Assert.Equal((1.10m, CustoEstimado.Origem.Informado), custo);
    }

    [Fact]
    public void PorUnidadeVendida_SemVinculoNemCustoInformado_SemCusto()
    {
        Assert.Null(CustoEstimado.PorUnidadeVendida(NovoProduto("Milk-shake", 18m), [Caixa("Coco", 25.20m)]));
    }

    [Fact]
    public void PorUnidadeVendida_MediaComMuitasCasas_ArredondaAoCentavo()
    {
        var picole = NovoProduto("Picolé ao leite", 5m, _leite.Id);

        // 25,00 ÷ 28 = 0,892857… → 0,89
        Assert.Equal(0.89m, CustoEstimado.PorUnidadeVendida(picole, [Caixa("Coco", 25m)])?.Custo);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.234")]
    public void InformarCusto_Invalido_Lanca(string custo)
    {
        Assert.Throws<DomainException>(() => NovoProduto("Milk-shake", 18m).InformarCusto(decimal.Parse(custo, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void InformarCusto_Nulo_ApagaENaoMarcaAlteracaoParaOApp()
    {
        var produto = NovoProduto("Milk-shake", 18m);
        var atualizadoEm = produto.AtualizadoEm;
        produto.InformarCusto(4.50m);

        produto.InformarCusto(null);

        Assert.Null(produto.CustoInformado);
        Assert.Equal(atualizadoEm, produto.AtualizadoEm);
    }
}
