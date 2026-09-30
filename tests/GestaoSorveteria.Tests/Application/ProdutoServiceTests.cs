using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Tests.Application;

public class ProdutoServiceTests
{
    private static readonly DateTime Ontem = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Hoje = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly ClockFake _clock = new();
    private readonly ProdutoService _service;

    public ProdutoServiceTests()
    {
        _service = new ProdutoService(_produtos, _clock);
    }

    [Fact]
    public async Task Listar_SemDesde_RetornaCatalogoInteiroInclusiveInativos()
    {
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem));
        var inativo = Produto.Criar("Antigo", "Picolés", 4m, false, 2, Ontem);
        inativo.Desativar(Ontem);
        _produtos.Produtos.Add(inativo);

        var catalogo = await _service.ListarAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(2, catalogo.Produtos.Count);
        Assert.Contains(catalogo.Produtos, p => p.Nome == "Antigo" && !p.Ativo);
        Assert.Equal(_clock.UtcNow, catalogo.GeradoEmUtc);
    }

    [Fact]
    public async Task Listar_ComDesde_RetornaSoOQueMudouDepois()
    {
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem));
        _produtos.Produtos.Add(Produto.Criar("Casquinha", "Sorvetes", 6m, false, 1, Hoje));
        var reajustado = Produto.Criar("Milk-shake", "Milk-shakes", 15m, false, 1, Ontem);
        reajustado.Atualizar("Milk-shake", "Milk-shakes", 17m, false, 1, Hoje);
        _produtos.Produtos.Add(reajustado);

        var catalogo = await _service.ListarAsync(Ontem.AddHours(1), TestContext.Current.CancellationToken);

        Assert.Equal(["Milk-shake", "Casquinha"], catalogo.Produtos.Select(p => p.Nome));
        Assert.Equal(17m, catalogo.Produtos.Single(p => p.Nome == "Milk-shake").Preco);
    }

    [Fact]
    public async Task Listar_ComDesde_ProdutoDesativadoDepoisVemParaOAppEsconder()
    {
        // RN-PR-03: o app precisa saber que o produto foi desativado para tirar o botão da tela.
        var produto = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem);
        produto.Desativar(Hoje);
        _produtos.Produtos.Add(produto);

        var catalogo = await _service.ListarAsync(Ontem.AddHours(1), TestContext.Current.CancellationToken);

        var dto = Assert.Single(catalogo.Produtos);
        Assert.False(dto.Ativo);
        Assert.Equal(Hoje, dto.AtualizadoEmUtc);
    }

    [Fact]
    public async Task Listar_ComDesdeIgualAoInstanteDaAlteracao_IncluiOProduto()
    {
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Hoje));

        var catalogo = await _service.ListarAsync(Hoje, TestContext.Current.CancellationToken);

        Assert.Single(catalogo.Produtos);
    }

    [Fact]
    public async Task Listar_DesdeSemFuso_TrataComoUtc()
    {
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Hoje));
        var semFuso = DateTime.SpecifyKind(Hoje + ProdutoService.MargemSeguranca + TimeSpan.FromMinutes(1), DateTimeKind.Unspecified);

        var catalogo = await _service.ListarAsync(semFuso, TestContext.Current.CancellationToken);

        Assert.Empty(catalogo.Produtos);
    }

    [Fact]
    public async Task Listar_ComDesde_AplicaMargemDeSeguranca()
    {
        // Alteração gravada "no passado" (relógio) mas confirmada depois do SELECT anterior: a margem garante que o app a receba.
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Hoje));

        var dentroDaMargem = await _service.ListarAsync(Hoje.AddMinutes(3), TestContext.Current.CancellationToken);
        var foraDaMargem = await _service.ListarAsync(Hoje + ProdutoService.MargemSeguranca + TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Single(dentroDaMargem.Produtos);
        Assert.Empty(foraDaMargem.Produtos);
    }

    [Fact]
    public async Task Listar_ProdutoNuncaAlterado_AtualizadoEmVemDaCriacao()
    {
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem));

        var catalogo = await _service.ListarAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(Ontem, catalogo.Produtos.Single().AtualizadoEmUtc);
    }
}
