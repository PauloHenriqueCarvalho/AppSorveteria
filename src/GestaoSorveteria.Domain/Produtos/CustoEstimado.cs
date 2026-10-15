using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Domain.Produtos;

/// <summary>
/// RN-PR-07: custo estimado de uma unidade vendida, com os custos de hoje do catálogo de estoque.
/// <list type="bullet">
/// <item>Produto ligado a um grupo de estoque: custo da embalagem ÷ unidades por embalagem × unidades por venda — do sabor
/// escolhido na venda (potes; é o item que saiu, mesmo que depois tenha mudado de grupo) ou, sem sabor ou com o sabor sem custo,
/// a média dos itens ativos do grupo (picolé ao leite: R$ 25,20 ÷ 28 = R$ 0,90). Item com custo zero conta como "sem custo".</item>
/// <item>Sem vínculo, ou grupo sem custo no catálogo: o custo informado no produto.</item>
/// <item>Nada disso, ou produto de valor livre (self-service: a quantidade não diz o peso): sem custo (nulo) — a margem fica
/// "n/d", nunca zero (RN-RL-04).</item>
/// </list>
/// Arredondado a 2 casas por unidade vendida; um custo que arredonda para R$ 0,00 conta como sem custo.
/// </summary>
public static class CustoEstimado
{
    /// <summary>De onde veio o custo: do catálogo de estoque ou informado no produto.</summary>
    public enum Origem
    {
        Estoque,
        Informado,
    }

    /// <param name="itensDoGrupo">Itens do grupo ligado ao produto (os inativos são ignorados na média).</param>
    /// <param name="sabor">O item de estoque escolhido na venda (potes), se houver.</param>
    public static (decimal Custo, Origem Origem)? PorUnidadeVendida(Produto produto, IEnumerable<ItemEstoque> itensDoGrupo, ItemEstoque? sabor = null)
    {
        if (produto.PermiteValorLivre)
        {
            return null;
        }

        if (produto.GrupoEstoqueId is { } grupoId)
        {
            var unitario = sabor is { CustoEmbalagem: > 0 }
                ? sabor.CustoEmbalagem / sabor.UnidadesPorEmbalagem
                : MediaDoGrupo(itensDoGrupo.Where(i => i.GrupoId == grupoId));
            if (unitario is { } valor && Moeda.Arredondar(valor * produto.UnidadesPorVenda) is > 0m and var custo)
            {
                return (custo, Origem.Estoque);
            }
        }

        return produto.CustoInformado is { } informado ? (informado, Origem.Informado) : null;
    }

    private static decimal? MediaDoGrupo(IEnumerable<ItemEstoque> itens)
    {
        var comCusto = itens.Where(i => i.Ativo && i.CustoEmbalagem > 0).Select(i => i.CustoEmbalagem / i.UnidadesPorEmbalagem).ToList();
        return comCusto.Count == 0 ? null : comCusto.Average();
    }
}
