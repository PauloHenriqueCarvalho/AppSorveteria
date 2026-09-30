using System.ComponentModel.DataAnnotations;

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

/// <summary>
/// POST /api/produtos e PUT /api/produtos/{id} (Admin). Mesmos campos nos dois; o Id é gerado pelo servidor na criação.
/// As anotações aqui são só validação de borda (400 antes de chegar ao domínio); a fonte de verdade é <c>Produto</c> (RN-PR-01).
/// </summary>
public sealed record SalvarProdutoRequest(
    [Required(ErrorMessage = "Informe o nome do produto.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O nome do produto deve ter de 2 a 80 caracteres.")]
    string Nome,
    [Required(ErrorMessage = "Informe a categoria.")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "A categoria deve ter de 1 a 50 caracteres.")]
    string Categoria,
    [Range(0, 99999.99, ErrorMessage = "O preço deve ser de 0 a 99.999,99.")]
    decimal Preco,
    bool PermiteValorLivre,
    [Range(0, int.MaxValue, ErrorMessage = "A ordem não pode ser negativa.")]
    int Ordem);
