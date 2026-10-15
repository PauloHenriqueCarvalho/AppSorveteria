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

/// <summary>
/// GET /api/relatorios/periodo?de=&amp;ate=&amp;agrupar= — vendas de um período (dias comerciais, inclusive; até 366 dias, sem passar
/// de hoje). Tudo calculado na API (RN-RL-01/03/08/09/10); o painel só mostra.
/// <paramref name="Agrupamento"/>: "Dia", "Semana" (segunda a domingo, recortada no período) ou "Mes" (recortado) — RN-RL-08.
/// <paramref name="PorTrecho"/>: um por dia/semana/mês do período, inclusive os sem venda.
/// <paramref name="Anterior"/>: o período de comparação (RN-RL-09).
/// <paramref name="PorHora"/>/<paramref name="PorDiaDaSemana"/>: horários e dias de pico (RN-RL-10).
/// <paramref name="Produtos"/>, <paramref name="Categorias"/>, <paramref name="Sabores"/>: rankings das vendas pagas (RN-RL-03).
/// </summary>
public sealed record RelatorioPeriodoDto(
    DateOnly De,
    DateOnly Ate,
    string Agrupamento,
    decimal TotalVendido,
    int QuantidadeComandas,
    decimal TicketMedio,
    decimal TotalEstornado,
    int QuantidadeEstornos,
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento,
    ComparativoPeriodoDto Anterior,
    IReadOnlyList<VendasNoTrechoDto> PorTrecho,
    IReadOnlyList<VendasNaHoraDto> PorHora,
    IReadOnlyList<VendasNoDiaDaSemanaDto> PorDiaDaSemana,
    IReadOnlyList<ProdutoNoRankingDto> Produtos,
    IReadOnlyList<CategoriaNoRankingDto> Categorias,
    IReadOnlyList<SaborNoRankingDto> Sabores,
    MargemPeriodoDto Margem);

/// <summary>
/// RN-RL-04: margem estimada do período com os custos de hoje (RN-PR-07), só sobre os produtos que têm custo:
/// <paramref name="MargemEstimada"/> = <paramref name="FaturamentoComCusto"/> − <paramref name="CustoEstimado"/>;
/// <paramref name="MargemPercentual"/> sobre esse faturamento (1 casa; nulo sem ele). <paramref name="FaturamentoSemCusto"/>: o que
/// ficou de fora por falta de custo (self-service, venda avulsa, produto sem custo cadastrado).
/// </summary>
public sealed record MargemPeriodoDto(
    decimal FaturamentoComCusto,
    decimal CustoEstimado,
    decimal MargemEstimada,
    decimal? MargemPercentual,
    decimal FaturamentoSemCusto);

/// <summary>
/// RN-RL-09: período de comparação. <paramref name="AteMesmoHorario"/>: o período consultado termina hoje, então o último dia do
/// anterior conta só até o mesmo horário de agora. Variações como no RN-RL-07 (<c>null</c> quando a base é zero).
/// <paramref name="PorTrecho"/>: trechos do anterior pareados <b>por posição</b> com os do período (o 1º com o 1º…), para desenhar
/// junto; comparando com o mês anterior pode ter um trecho a menos ou a mais (meses de tamanhos diferentes).
/// </summary>
public sealed record ComparativoPeriodoDto(
    DateOnly De,
    DateOnly Ate,
    bool AteMesmoHorario,
    decimal TotalVendido,
    int QuantidadeComandas,
    decimal TicketMedio,
    decimal? VariacaoTotalPercentual,
    decimal? VariacaoQuantidadePercentual,
    decimal? VariacaoTicketPercentual,
    IReadOnlyList<VendasNoTrechoDto> PorTrecho);

/// <summary>Vendas pagas de um trecho do período (um dia, uma semana ou um mês, recortados em <paramref name="De"/>–<paramref name="Ate"/>).</summary>
public sealed record VendasNoTrechoDto(DateOnly De, DateOnly Ate, decimal Total, int Quantidade);

/// <summary>
/// RN-RL-10: vendas pagas num dia da semana (<paramref name="DiaDaSemana"/>: 0 = domingo … 6 = sábado).
/// <paramref name="Dias"/>: quantos desses dias o período tem; <paramref name="MediaPorDia"/> = total ÷ dias, 2 casas.
/// </summary>
public sealed record VendasNoDiaDaSemanaDto(int DiaDaSemana, int Dias, decimal Total, int Quantidade, decimal MediaPorDia);

/// <summary>
/// RN-RL-03: produto no ranking do período. Nome e categoria do cadastro atual; itens livres juntos como
/// "Self-service / venda avulsa" (<paramref name="ProdutoId"/> nulo). <paramref name="Percentual"/>: parte do faturamento, 1 casa.
/// <paramref name="CustoEstimado"/>/<paramref name="Margem"/>/<paramref name="MargemPercentual"/>: RN-RL-04, nulos ("n/d") quando
/// alguma venda do produto não tem custo (RN-PR-07); margem % sobre o total do produto.
/// </summary>
public sealed record ProdutoNoRankingDto(
    Guid? ProdutoId,
    string Descricao,
    string Categoria,
    int Quantidade,
    decimal Total,
    decimal Percentual,
    decimal? CustoEstimado = null,
    decimal? Margem = null,
    decimal? MargemPercentual = null);

/// <summary>RN-RL-03: soma dos produtos de uma categoria; <paramref name="Percentual"/>: parte do faturamento, 1 casa.</summary>
public sealed record CategoriaNoRankingDto(string Categoria, int Quantidade, decimal Total, decimal Percentual);

/// <summary>
/// RN-RL-03: sabor (item de estoque escolhido na venda, RN-ES-09) — "Pote 1L Flocos". <paramref name="Unidades"/>: potes que
/// saíram (quantidade × unidades por venda do produto). Custo e margem como em <see cref="ProdutoNoRankingDto"/>, pelo custo do sabor.
/// </summary>
public sealed record SaborNoRankingDto(
    Guid ItemEstoqueId,
    string Descricao,
    int Unidades,
    decimal Total,
    decimal? CustoEstimado = null,
    decimal? Margem = null,
    decimal? MargemPercentual = null);
