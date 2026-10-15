using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Relatorios;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Application.Relatorios;

/// <summary>Contas comuns aos relatórios do dia e do período (RN-RL-01/03/06/07).</summary>
internal static class CalculoRelatorio
{
    /// <summary>Rótulo dos itens livres no ranking (RN-RL-03).</summary>
    public const string RotuloItemLivre = "Self-service / venda avulsa";

    /// <summary>Total ÷ quantidade, 2 casas; 0 sem vendas.</summary>
    public static decimal TicketMedio(decimal total, int quantidade) => quantidade == 0 ? 0m : Moeda.Arredondar(total / quantidade);

    /// <summary>(atual − anterior) ÷ anterior × 100, 1 casa; anterior zero → null (RN-RL-07).</summary>
    public static decimal? Variacao(decimal atual, decimal anterior) =>
        anterior == 0m ? null : Math.Round((atual - anterior) / anterior * 100m, 1, MidpointRounding.AwayFromZero);

    /// <summary>Parte do faturamento em %, 1 casa; sem faturamento → 0.</summary>
    public static decimal Percentual(decimal parte, decimal total) =>
        total == 0m ? 0m : Math.Round(parte / total * 100m, 1, MidpointRounding.AwayFromZero);

    /// <summary>RN-RL-06: comandas pagas por hora local do fechamento; só as horas com venda.</summary>
    public static IReadOnlyList<VendasNaHoraDto> PorHora(IEnumerable<Comanda> fechadas) =>
        fechadas
            .GroupBy(c => TimeZoneInfo.ConvertTimeFromUtc(c.FechadaEm!.Value, DiaComercial.Fuso).Hour)
            .OrderBy(g => g.Key)
            .Select(g => new VendasNaHoraDto(g.Key, Moeda.Arredondar(g.Sum(c => c.Total)), g.Count()))
            .ToList();

    /// <summary>
    /// RN-RL-03: nome e categoria do cadastro atual (a linha da venda de pote traz o sabor, "Pote 1L · Flocos", e não serve
    /// de nome do produto); produto que não está mais no cadastro usa a descrição da venda mais recente. Item livre → rótulo único.
    /// </summary>
    public static (string Descricao, string Categoria) NomeECategoria(
        Guid? produtoId, IReadOnlyDictionary<Guid, Produto> produtos, string descricaoDaVenda) =>
        produtoId is null
            ? (RotuloItemLivre, RotuloItemLivre)
            : produtos.TryGetValue(produtoId.Value, out var produto)
                ? (produto.Nome, produto.Categoria)
                : (descricaoDaVenda, "Sem categoria");
}
