using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Application.Produtos;

public static class ProdutoMapper
{
    public static ProdutoDto ToDto(this Produto produto) =>
        new(
            produto.Id,
            produto.Nome,
            produto.Categoria,
            produto.Preco,
            produto.PermiteValorLivre,
            produto.Ativo,
            produto.Ordem,
            produto.AtualizadoEm ?? produto.CriadoEm);
}
