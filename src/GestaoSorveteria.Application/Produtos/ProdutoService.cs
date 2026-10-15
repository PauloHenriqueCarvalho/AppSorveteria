using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Produtos;

/// <summary>Catálogo de produtos (RN-PR-*).</summary>
public sealed class ProdutoService
{
    /// <summary>
    /// Uma alteração gravada com AtualizadoEm anterior ao geradoEm desta chamada, mas confirmada no banco depois do SELECT,
    /// ficaria de fora para sempre na próxima sincronização. Recuar o "desde" alguns minutos fecha essa janela;
    /// receber de novo um produto já conhecido é inofensivo.
    /// </summary>
    public static readonly TimeSpan MargemSeguranca = TimeSpan.FromMinutes(5);

    private readonly IProdutoRepository _produtos;
    private readonly IEstoqueRepository _estoque;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ProdutoService(IProdutoRepository produtos, IEstoqueRepository estoque, IUnitOfWork uow, IClock clock)
    {
        _produtos = produtos;
        _estoque = estoque;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>
    /// Catálogo completo (sem <paramref name="desdeUtc"/>) ou só os produtos criados/alterados a partir daquele instante.
    /// Produtos inativos vêm junto: é assim que o app fica sabendo que deve esconder o botão (RN-PR-03).
    /// A comparação é "maior ou igual" e recua <see cref="MargemSeguranca"/>: receber de novo um produto é inofensivo, perder um não.
    /// </summary>
    public async Task<CatalogoProdutosResponse> ListarAsync(DateTime? desdeUtc, CancellationToken cancellationToken = default)
    {
        var geradoEm = _clock.UtcNow;

        var produtos = desdeUtc is null
            ? await _produtos.ListarAsync(somenteAtivos: false, cancellationToken)
            : await _produtos.ListarAlteradosDesdeAsync(ParaUtc(desdeUtc.Value) - MargemSeguranca, cancellationToken);

        var grupos = await GruposAsync(cancellationToken);
        return new CatalogoProdutosResponse(geradoEm, produtos.Select(p => p.ToDto(grupos)).ToList());
    }

    public async Task<ProdutoDto> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await CarregarAsync(id, cancellationToken)).ToDto(await GruposAsync(cancellationToken));

    /// <summary>Cadastro pelo painel (RN-PR-01: nome único sem diferenciar maiúsculas).</summary>
    public async Task<ProdutoDto> CriarAsync(SalvarProdutoRequest request, CancellationToken cancellationToken = default)
    {
        await GarantirNomeLivreAsync(request.Nome, ignorarId: null, cancellationToken);

        var produto = Produto.Criar(request.Nome, request.Categoria, request.Preco, request.PermiteValorLivre, request.Ordem, _clock.UtcNow);
        await _produtos.AdicionarAsync(produto, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return produto.ToDto(await GruposAsync(cancellationToken));
    }

    /// <summary>Edição pelo painel. Mudar o preço não mexe nas vendas passadas: o item guarda o preço praticado (RN-PR-04).</summary>
    public async Task<ProdutoDto> AtualizarAsync(Guid id, SalvarProdutoRequest request, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        await GarantirNomeLivreAsync(request.Nome, ignorarId: id, cancellationToken);

        produto.Atualizar(request.Nome, request.Categoria, request.Preco, request.PermiteValorLivre, request.Ordem, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        return produto.ToDto(await GruposAsync(cancellationToken));
    }

    public async Task<ProdutoDto> AtivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        produto.Ativar(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return produto.ToDto(await GruposAsync(cancellationToken));
    }

    /// <summary>RN-PR-03: produto nunca é apagado; desativar é a única forma de tirá-lo do app.</summary>
    public async Task<ProdutoDto> DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        produto.Desativar(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return produto.ToDto(await GruposAsync(cancellationToken));
    }

    /// <summary>
    /// RN-PR-06: liga o produto a um grupo de estoque (ou desliga, com grupo nulo). Grupo de potes (<c>SaldoPelaVenda</c>) faz
    /// o app pedir o sabor e a venda baixar o saldo; grupo de insumos (<c>SoAviso</c>) não tem saldo e não liga.
    /// </summary>
    public async Task<ProdutoDto> LigarAoEstoqueAsync(Guid id, LigarProdutoEstoqueRequest request, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        if (request.GrupoEstoqueId is { } grupoId)
        {
            var grupo = await _estoque.ObterGrupoAsync(grupoId, cancellationToken)
                ?? throw new DomainException("Grupo de estoque não encontrado.");
            Exigir.Que(grupo.ModoControle != ModoControle.SoAviso,
                $"{grupo.Nome} é de insumos (só aviso), sem saldo: não dá para ligar produto de venda a ele.");
        }

        produto.LigarAoEstoque(request.GrupoEstoqueId, request.UnidadesPorVenda, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return produto.ToDto(await GruposAsync(cancellationToken));
    }

    /// <summary>RN-PR-07/RN-RL-04: custo estimado e margem de cada produto, com os custos de hoje do catálogo.</summary>
    public async Task<IReadOnlyList<CustoProdutoDto>> ListarCustosAsync(CancellationToken cancellationToken = default)
    {
        var itens = await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken);
        return (await _produtos.ListarAsync(somenteAtivos: false, cancellationToken)).Select(p => Custo(p, itens)).ToList();
    }

    /// <summary>RN-PR-07: informa (ou apaga, com nulo) o custo de uma unidade vendida.</summary>
    public async Task<CustoProdutoDto> InformarCustoAsync(Guid id, InformarCustoProdutoRequest request, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        produto.InformarCusto(request.Custo);
        await _uow.SaveChangesAsync(cancellationToken);
        return Custo(produto, await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken));
    }

    private static CustoProdutoDto Custo(Produto produto, IReadOnlyList<ItemEstoque> itens)
    {
        var custo = CustoEstimado.PorUnidadeVendida(produto, itens);
        decimal? margem = custo is { } c && !produto.PermiteValorLivre ? Moeda.Arredondar(produto.Preco - c.Custo) : null;
        return new CustoProdutoDto(
            produto.Id,
            produto.CustoInformado,
            custo?.Custo,
            custo?.Origem.ToString(),
            margem,
            margem is { } m && produto.Preco > 0 ? Relatorios.CalculoRelatorio.Percentual(m, produto.Preco) : null);
    }

    private async Task<IReadOnlyDictionary<Guid, GrupoEstoque>> GruposAsync(CancellationToken cancellationToken) =>
        (await _estoque.ListarGruposAsync(cancellationToken)).ToDictionary(g => g.Id);

    private async Task<Produto> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _produtos.ObterPorIdAsync(id, cancellationToken)
        ?? throw new EntidadeNaoEncontradaException("Produto", id);

    private async Task GarantirNomeLivreAsync(string nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        // RN-PR-01. O índice único em nome_normalizado segura a corrida (vira 409); aqui é a mensagem amigável.
        if (await _produtos.ExisteComNomeAsync(Produto.NormalizarNome(nome), ignorarId, cancellationToken))
        {
            throw new DomainException("Já existe um produto com esse nome.");
        }
    }

    private static DateTime ParaUtc(DateTime data) => data.Kind switch
    {
        DateTimeKind.Utc => data,
        DateTimeKind.Local => data.ToUniversalTime(),
        _ => DateTime.SpecifyKind(data, DateTimeKind.Utc), // querystring sem "Z" chega Unspecified: assumimos UTC (RN-TD-01)
    };
}
