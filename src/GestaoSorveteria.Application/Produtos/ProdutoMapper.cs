using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Application.Produtos;

public static class ProdutoMapper
{
    /// <param name="grupos">Grupos de estoque, para dizer se o produto pede sabor (grupo de potes, RN-ES-09).</param>
    public static ProdutoDto ToDto(this Produto produto, IReadOnlyDictionary<Guid, GrupoEstoque>? grupos = null) =>
        new(
            produto.Id,
            produto.Nome,
            produto.Categoria,
            produto.Preco,
            produto.PermiteValorLivre,
            produto.Ativo,
            produto.Ordem,
            produto.AtualizadoEm ?? produto.CriadoEm,
            produto.GrupoEstoqueId,
            produto.UnidadesPorVenda,
            produto.GrupoEstoqueId is { } grupoId
                && grupos?.GetValueOrDefault(grupoId)?.ModoControle == ModoControle.SaldoPelaVenda);
}
