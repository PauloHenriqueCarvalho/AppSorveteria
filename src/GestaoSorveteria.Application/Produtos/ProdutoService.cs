using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Domain.Common;
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
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ProdutoService(IProdutoRepository produtos, IUnitOfWork uow, IClock clock)
    {
        _produtos = produtos;
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

        return new CatalogoProdutosResponse(geradoEm, produtos.Select(p => p.ToDto()).ToList());
    }

    public async Task<ProdutoDto> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await CarregarAsync(id, cancellationToken)).ToDto();

    /// <summary>Cadastro pelo painel (RN-PR-01: nome único sem diferenciar maiúsculas).</summary>
    public async Task<ProdutoDto> CriarAsync(SalvarProdutoRequest request, CancellationToken cancellationToken = default)
    {
        await GarantirNomeLivreAsync(request.Nome, ignorarId: null, cancellationToken);

        var produto = Produto.Criar(request.Nome, request.Categoria, request.Preco, request.PermiteValorLivre, request.Ordem, _clock.UtcNow);
        await _produtos.AdicionarAsync(produto, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return produto.ToDto();
    }

    /// <summary>Edição pelo painel. Mudar o preço não mexe nas vendas passadas: o item guarda o preço praticado (RN-PR-04).</summary>
    public async Task<ProdutoDto> AtualizarAsync(Guid id, SalvarProdutoRequest request, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        await GarantirNomeLivreAsync(request.Nome, ignorarId: id, cancellationToken);

        produto.Atualizar(request.Nome, request.Categoria, request.Preco, request.PermiteValorLivre, request.Ordem, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        return produto.ToDto();
    }

    public async Task<ProdutoDto> AtivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        produto.Ativar(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return produto.ToDto();
    }

    /// <summary>RN-PR-03: produto nunca é apagado; desativar é a única forma de tirá-lo do app.</summary>
    public async Task<ProdutoDto> DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var produto = await CarregarAsync(id, cancellationToken);
        produto.Desativar(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return produto.ToDto();
    }

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
