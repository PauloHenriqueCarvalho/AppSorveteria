using GestaoSorveteria.Contracts.Caixas;

namespace GestaoSorveteria.Contracts.Relatorios;

/// <summary>
/// GET /api/relatorios/dia?data= — dashboard da dona. Tudo calculado na API; o painel só mostra.
/// Vendas = comandas <c>Fechada</c> cujo fechamento caiu no dia comercial <paramref name="Data"/> (RN-RL-01/02).
/// <paramref name="TicketMedio"/> = total ÷ quantidade, arredondado a 2 casas (0 sem vendas).
/// Vendas do dia estornadas depois ficam fora do total e aparecem em <paramref name="TotalEstornado"/>/<paramref name="QuantidadeEstornos"/> (RN-RL-01).
/// <paramref name="CaixaAtual"/>: o caixa aberto agora, se houver (último estado recebido do app).
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
    CaixaResumoDto? CaixaAtual);
