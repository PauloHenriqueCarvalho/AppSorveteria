using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Tests.Domain;

public class ProdutoTests
{
    [Fact]
    public void Criar_NormalizaNomeECategoria()
    {
        var produto = Produto.Criar("  Milk-Shake 500ml ", " Milk-shakes ", 18.90m, false, 2, Cenario.Agora);

        Assert.Equal("Milk-Shake 500ml", produto.Nome);
        Assert.Equal("milk-shake 500ml", produto.NomeNormalizado);
        Assert.Equal("Milk-shakes", produto.Categoria);
        Assert.Equal(18.90m, produto.Preco);
        Assert.True(produto.Ativo);
        Assert.Null(produto.AtualizadoEm);
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("   ")]
    public void Criar_NomeCurto_Lanca(string nome)
    {
        Assert.Throws<DomainException>(() => Produto.Criar(nome, "Picolés", 5m, false, 1, Cenario.Agora));
    }

    [Fact]
    public void Criar_PrecoNegativo_Lanca()
    {
        Assert.Throws<DomainException>(() => Produto.Criar("Picolé", "Picolés", -0.01m, false, 1, Cenario.Agora));
    }

    [Fact]
    public void Criar_PrecoComTresCasas_Lanca()
    {
        Assert.Throws<DomainException>(() => Produto.Criar("Picolé", "Picolés", 4.999m, false, 1, Cenario.Agora));
    }

    [Fact]
    public void Criar_ValorLivreComPrecoZero_Permitido()
    {
        var produto = Produto.Criar("Self-service", "Self-service", 0m, true, 1, Cenario.Agora);

        Assert.True(produto.PermiteValorLivre);
        Assert.Equal(0m, produto.Preco);
    }

    [Fact]
    public void Atualizar_MarcaAtualizadoEm()
    {
        var produto = Cenario.Picole(5m);
        var depois = Cenario.Agora.AddDays(1);

        produto.Atualizar("Picolé de morango", "Picolés", 6m, false, 1, depois);

        Assert.Equal(6m, produto.Preco);
        Assert.Equal(depois, produto.AtualizadoEm);
    }

    [Fact]
    public void DesativarEAtivar()
    {
        var produto = Cenario.Picole();

        produto.Desativar(Cenario.Agora);
        Assert.False(produto.Ativo);

        produto.Ativar(Cenario.Agora);
        Assert.True(produto.Ativo);
    }

    [Fact]
    public void Ativar_JaAtivo_NaoAlteraAtualizadoEm()
    {
        var produto = Cenario.Picole();

        produto.Ativar(Cenario.Agora.AddDays(1));

        Assert.True(produto.Ativo);
        Assert.Null(produto.AtualizadoEm);
    }

    [Fact]
    public void Desativar_JaInativo_NaoAlteraAtualizadoEm()
    {
        var produto = Cenario.Picole();
        produto.Desativar(Cenario.Agora);

        produto.Desativar(Cenario.Agora.AddDays(1));

        Assert.False(produto.Ativo);
        Assert.Equal(Cenario.Agora, produto.AtualizadoEm);
    }

    [Fact]
    public void Ativar_DataSemUtc_LancaMesmoJaAtivo()
    {
        var produto = Cenario.Picole();

        Assert.Throws<DomainException>(() => produto.Ativar(DateTime.SpecifyKind(Cenario.Agora, DateTimeKind.Local)));
    }

    [Fact]
    public void Desativar_DataSemUtc_LancaMesmoJaInativo()
    {
        var produto = Cenario.Picole();
        produto.Desativar(Cenario.Agora);

        Assert.Throws<DomainException>(() => produto.Desativar(DateTime.SpecifyKind(Cenario.Agora, DateTimeKind.Local)));
    }
}
