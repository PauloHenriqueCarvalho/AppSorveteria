using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Tests.Application;

public class ProdutoServiceTests
{
    private static readonly DateTime Ontem = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Hoje = new(2026, 9, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new();
    private readonly ProdutoService _service;

    public ProdutoServiceTests()
    {
        _service = new ProdutoService(_produtos, _estoque, _uow, _clock);
    }

    private static SalvarProdutoRequest Picole(string nome = "Picolé", decimal preco = 5m) =>
        new(nome, "Picolés", preco, PermiteValorLivre: false, Ordem: 1);

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

    // ---------- Cadastro (Admin) ----------

    [Fact]
    public async Task Criar_Valido_GravaComDataDoRelogioEConfirma()
    {
        var dto = await _service.CriarAsync(Picole(), TestContext.Current.CancellationToken);

        var gravado = Assert.Single(_produtos.Produtos);
        Assert.Equal(dto.Id, gravado.Id);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("Picolé", dto.Nome);
        Assert.Equal(5m, dto.Preco);
        Assert.True(dto.Ativo);
        Assert.Equal(_clock.UtcNow, dto.AtualizadoEmUtc);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Criar_NomeJaUsadoIgnorandoMaiusculasEEspacos_Lanca()
    {
        // RN-PR-01
        _produtos.Produtos.Add(Produto.Criar("Picolé de Uva", "Picolés", 5m, false, 1, Ontem));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarAsync(Picole("  PICOLÉ DE UVA "), TestContext.Current.CancellationToken));

        Assert.Equal("Já existe um produto com esse nome.", ex.Message);
        Assert.Single(_produtos.Produtos);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Criar_PrecoNegativo_LancaSemGravar()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarAsync(Picole(preco: -1m), TestContext.Current.CancellationToken));

        Assert.Empty(_produtos.Produtos);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Atualizar_Existente_AlteraEMarcaAtualizadoEm()
    {
        var produto = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem);
        _produtos.Produtos.Add(produto);

        var dto = await _service.AtualizarAsync(produto.Id, Picole(preco: 6.5m) with { Ordem = 3 }, TestContext.Current.CancellationToken);

        Assert.Equal(6.5m, dto.Preco);
        Assert.Equal(3, dto.Ordem);
        Assert.Equal(_clock.UtcNow, dto.AtualizadoEmUtc);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Atualizar_MantendoOProprioNome_NaoLanca()
    {
        var produto = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem);
        _produtos.Produtos.Add(produto);

        var dto = await _service.AtualizarAsync(produto.Id, Picole(preco: 7m), TestContext.Current.CancellationToken);

        Assert.Equal(7m, dto.Preco);
    }

    [Fact]
    public async Task Atualizar_ComNomeDeOutroProduto_Lanca()
    {
        // RN-PR-01
        _produtos.Produtos.Add(Produto.Criar("Casquinha", "Sorvetes", 6m, false, 1, Ontem));
        var produto = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem);
        _produtos.Produtos.Add(produto);

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.AtualizarAsync(produto.Id, Picole("casquinha"), TestContext.Current.CancellationToken));

        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Atualizar_NaoEncontrado_Lanca()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.AtualizarAsync(Guid.NewGuid(), Picole(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Desativar_Existente_ContinuaNoCatalogoComAtivoFalso()
    {
        // RN-PR-03: nunca apagado; desativado vem na sincronização com Ativo = false.
        var produto = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem);
        _produtos.Produtos.Add(produto);
        _clock.UtcNow = Hoje;

        var dto = await _service.DesativarAsync(produto.Id, TestContext.Current.CancellationToken);
        var catalogo = await _service.ListarAsync(Ontem.AddHours(1), TestContext.Current.CancellationToken);

        Assert.False(dto.Ativo);
        Assert.Single(_produtos.Produtos);
        Assert.False(Assert.Single(catalogo.Produtos).Ativo);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Ativar_Inativo_VoltaAtivoEMarcaAtualizadoEm()
    {
        var produto = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem);
        produto.Desativar(Ontem);
        _produtos.Produtos.Add(produto);

        var dto = await _service.AtivarAsync(produto.Id, TestContext.Current.CancellationToken);

        Assert.True(dto.Ativo);
        Assert.Equal(_clock.UtcNow, dto.AtualizadoEmUtc);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Ativar_JaAtivo_NaoEntraNaProximaSincronizacao()
    {
        // Sem mudança real não há AtualizadoEm novo, então o app não baixa o produto de novo.
        _produtos.Produtos.Add(Produto.Criar("Picolé", "Picolés", 5m, false, 1, Ontem));
        _clock.UtcNow = Hoje;
        var produto = _produtos.Produtos.Single();

        var dto = await _service.AtivarAsync(produto.Id, TestContext.Current.CancellationToken);
        var catalogo = await _service.ListarAsync(Ontem.AddHours(1), TestContext.Current.CancellationToken);

        Assert.True(dto.Ativo);
        Assert.Equal(Ontem, dto.AtualizadoEmUtc);
        Assert.Empty(catalogo.Produtos);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AtivarOuDesativar_NaoEncontrado_Lanca(bool ativar)
    {
        var id = Guid.NewGuid();

        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() => ativar
            ? _service.AtivarAsync(id, TestContext.Current.CancellationToken)
            : _service.DesativarAsync(id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Obter_NaoEncontrado_Lanca()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.ObterAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListarCustos_PeloEstoqueInformadoESemCusto_ComAMargemSobreOPreco()
    {
        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var leite = GrupoEstoque.Criar("Picolé ao leite", ModoControle.Contagem, 1);
        _estoque.Grupos.Add(leite);
        _estoque.Itens.Add(ItemEstoque.Criar(leite.Id, fabrica.Id, "Coco", UnidadeCompra.Caixa, 28, 25.20m, 1));
        var picole = Produto.Criar("Picolé ao leite", "Picolés", 5m, false, 1, Ontem);
        picole.LigarAoEstoque(leite.Id, 1, Ontem);
        var shake = Produto.Criar("Milk-shake", "Bebidas", 18m, false, 2, Ontem);
        var selfService = Produto.Criar("Self-service", "Self-service", 69.90m, true, 3, Ontem);
        _produtos.Produtos.AddRange([picole, shake, selfService]);
        _clock.UtcNow = Hoje;

        var semCusto = (await _service.ListarCustosAsync(TestContext.Current.CancellationToken)).Single(c => c.ProdutoId == shake.Id);
        var informado = await _service.InformarCustoAsync(shake.Id, new InformarCustoProdutoRequest(4.50m), TestContext.Current.CancellationToken);
        var custos = (await _service.ListarCustosAsync(TestContext.Current.CancellationToken)).ToDictionary(c => c.ProdutoId);

        Assert.Equal((null, null, null), (semCusto.CustoEstimado, semCusto.Origem, semCusto.Margem));
        // RN-PR-07: picolé = 25,20 ÷ 28 = 0,90; margem 4,10 de 5,00 = 82%.
        Assert.Equal((0.90m, "Estoque", 4.10m, 82.0m), (custos[picole.Id].CustoEstimado, custos[picole.Id].Origem, custos[picole.Id].Margem, custos[picole.Id].MargemPercentual));
        Assert.Equal((4.50m, 4.50m, "Informado", 13.50m, 75.0m), (informado.CustoInformado, informado.CustoEstimado, informado.Origem, informado.Margem, informado.MargemPercentual));
        Assert.Equal(1, _uow.Confirmacoes);
        Assert.Null(custos[selfService.Id].Margem); // valor livre: o preço muda a cada venda
    }

    [Fact]
    public async Task InformarCusto_Nulo_ApagaOCusto()
    {
        var shake = Produto.Criar("Milk-shake", "Bebidas", 18m, false, 2, Ontem);
        shake.InformarCusto(4.50m);
        _produtos.Produtos.Add(shake);
        _clock.UtcNow = Hoje;

        var custo = await _service.InformarCustoAsync(shake.Id, new InformarCustoProdutoRequest(null), TestContext.Current.CancellationToken);

        Assert.Equal((null, null, null), (custo.CustoInformado, custo.CustoEstimado, custo.Margem));
    }
}
