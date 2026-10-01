namespace GestaoSorveteria.Contracts.Caixas;

// Leitura dos caixas pelo painel (docs/03 §6.1). Todo valor em dinheiro vem calculado pela API:
// o painel só mostra. Enums como texto ("Aberto"/"Fechado", "Sangria"/"Suprimento", "Dinheiro"...).

/// <summary>
/// Um caixa na lista do painel. Vendas = comandas <c>Fechada</c> e também as <c>Estornada</c> depois: o estorno não altera o caixa (RN-CM-09).
/// Caixa fechado: <paramref name="TotalVendasDinheiro"/>, <paramref name="ValorEsperado"/>, <paramref name="ValorContado"/>
/// e <paramref name="Diferenca"/> são os gravados no fechamento (RN-CX-06/07).
/// Caixa aberto: <paramref name="TotalVendasDinheiro"/> e <paramref name="ValorEsperado"/> são "até agora"
/// (com as vendas já recebidas do app) e <paramref name="ValorContado"/>/<paramref name="Diferenca"/> vêm nulos.
/// <paramref name="TotalVendas"/>, <paramref name="QuantidadeComandas"/> e o total por forma incluem as vendas que chegaram
/// depois do fechamento (RN-CX-08); o dinheiro delas vem separado em <paramref name="DinheiroRecebidoAposFechamento"/>,
/// para a dona entender por que a linha "Dinheiro" não bate com <paramref name="TotalVendasDinheiro"/>.
/// </summary>
public sealed record CaixaResumoDto(
    Guid Id,
    string Status,
    DateTime AbertoEm,
    string AbertoPorNome,
    decimal FundoTroco,
    DateTime? FechadoEm,
    string? FechadoPorNome,
    decimal TotalVendas,
    int QuantidadeComandas,
    decimal? TotalVendasDinheiro,
    decimal? ValorEsperado,
    decimal? ValorContado,
    decimal? Diferenca,
    bool DivergenciaSincronizacao,
    int VendasRecebidasAposFechamento,
    decimal DinheiroRecebidoAposFechamento);

/// <summary>GET /api/caixas/{id}: resumo + sangrias/suprimentos + vendas por forma de pagamento.</summary>
public sealed record CaixaDetalheDto(
    CaixaResumoDto Resumo,
    IReadOnlyList<MovimentoCaixaDto> Movimentos,
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento,
    string? Observacao);

/// <summary>RN-CX-04. <paramref name="Tipo"/>: "Sangria" ou "Suprimento".</summary>
public sealed record MovimentoCaixaDto(Guid Id, string Tipo, decimal Valor, string Motivo, string UsuarioNome, DateTime Em);

/// <summary>Soma dos pagamentos de uma forma (ex.: "Pix") nas comandas fechadas; <paramref name="Quantidade"/> = nº de pagamentos.</summary>
public sealed record TotalPorFormaDto(string Forma, decimal Total, int Quantidade);
