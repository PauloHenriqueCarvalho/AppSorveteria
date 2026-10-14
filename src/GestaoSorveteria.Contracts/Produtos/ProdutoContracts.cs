using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Produtos;

/// <summary>
/// Produto como o app e o painel enxergam. Inativo vem junto para o app esconder o botão (RN-PR-03).
/// <paramref name="GrupoEstoqueId"/>/<paramref name="UnidadesPorVenda"/>: RN-PR-06. <paramref name="PedeSabor"/>: o grupo é de
/// potes (<c>SaldoPelaVenda</c>) — o app mostra a grade de sabores e manda o sabor na venda (RN-ES-09).
/// </summary>
public sealed record ProdutoDto(
    Guid Id,
    string Nome,
    string Categoria,
    decimal Preco,
    bool PermiteValorLivre,
    bool Ativo,
    int Ordem,
    DateTime AtualizadoEmUtc,
    Guid? GrupoEstoqueId = null,
    int UnidadesPorVenda = 1,
    bool PedeSabor = false);

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

/// <summary>
/// PUT /api/produtos/{id}/estoque (Admin) — RN-PR-06: liga o produto a um grupo de estoque (ou desliga, com grupo nulo).
/// Separado do cadastro para quem edita nome e preço não desligar o produto sem querer.
/// </summary>
public sealed record LigarProdutoEstoqueRequest(
    Guid? GrupoEstoqueId,
    [Range(1, 50, ErrorMessage = "Unidades por venda vai de 1 a 50.")]
    int UnidadesPorVenda = 1);
