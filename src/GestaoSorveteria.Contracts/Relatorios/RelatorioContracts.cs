using GestaoSorveteria.Contracts.Caixas;

namespace GestaoSorveteria.Contracts.Relatorios;

/// <summary>
/// GET /api/relatorios/dia?data= — dashboard da dona. Tudo calculado na API; o painel só mostra.
/// Vendas = comandas <c>Fechada</c> cujo fechamento caiu no dia comercial <paramref name="Data"/> (RN-RL-01/02).
/// <paramref name="TicketMedio"/> = total ÷ quantidade, arredondado a 2 casas (0 sem vendas).
/// Vendas do dia estornadas depois ficam fora do total e aparecem em <paramref name="TotalEstornado"/>/<paramref name="QuantidadeEstornos"/> (RN-RL-01).
/// <paramref name="CaixaAtual"/>: o caixa aberto agora, se houver (último estado recebido do app).
/// <paramref name="PorHora"/>: vendas por hora do dia, só as horas com venda (RN-RL-06).
/// <paramref name="SemanaPassada"/>: mesmo dia da semana anterior para comparar (RN-RL-07).
/// <paramref name="MaisVendidos"/>: até 5 produtos por faturamento (RN-RL-03).
/// </summary>
public sealed record ResumoDiaDto(
    DateOnly Data,
    decimal TotalVendido,
    int QuantidadeComandas,
    decimal TicketMedio,
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento,
    decimal TotalEstornado,
    int QuantidadeEstornos,
    int QuantidadeCanceladas,
    int VendasRecebidasAposFechamento,
    CaixaResumoDto? CaixaAtual,
    IReadOnlyList<VendasNaHoraDto> PorHora,
    ComparativoDiaDto SemanaPassada,
    IReadOnlyList<ProdutoVendidoDto> MaisVendidos);

/// <summary>Vendas pagas cujo fechamento caiu na hora <paramref name="Hora"/> (0–23, horário da loja). RN-RL-06.</summary>
public sealed record VendasNaHoraDto(int Hora, decimal Total, int Quantidade);

/// <summary>
/// RN-RL-07: o mesmo dia da semana anterior (<paramref name="Data"/>). Se o dia consultado é hoje,
/// soma só até o mesmo horário (<paramref name="AteMesmoHorario"/> = true), para comparar manhã com manhã.
/// Variações em % com 1 casa: (hoje − antes) ÷ antes × 100; <c>null</c> quando a base é zero.
/// </summary>
public sealed record ComparativoDiaDto(
    DateOnly Data,
    bool AteMesmoHorario,
    decimal TotalVendido,
    int QuantidadeComandas,
    decimal TicketMedio,
    decimal? VariacaoTotalPercentual,
    decimal? VariacaoQuantidadePercentual,
    decimal? VariacaoTicketPercentual,
    IReadOnlyList<VendasNaHoraDto> PorHora);

/// <summary>
/// RN-RL-03: produto no ranking do dia. Itens livres (sem produto) ficam juntos como "Self-service / venda avulsa"
/// (<paramref name="ProdutoId"/> nulo).
/// </summary>
public sealed record ProdutoVendidoDto(Guid? ProdutoId, string Descricao, int Quantidade, decimal Total);
