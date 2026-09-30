namespace GestaoSorveteria.Contracts.Produtos;

/// <summary>Produto como o app e o painel enxergam. Inativo vem junto para o app esconder o botão (RN-PR-03).</summary>
public sealed record ProdutoDto(
    Guid Id,
    string Nome,
    string Categoria,
    decimal Preco,
    bool PermiteValorLivre,
    bool Ativo,
    int Ordem,
    DateTime AtualizadoEmUtc);

/// <summary>
/// GET /api/produtos?desde=. O app guarda <see cref="GeradoEmUtc"/> e manda de volta em <c>desde</c>
/// na próxima sincronização, assim baixa só o que mudou.
/// </summary>
public sealed record CatalogoProdutosResponse(DateTime GeradoEmUtc, IReadOnlyList<ProdutoDto> Produtos);
