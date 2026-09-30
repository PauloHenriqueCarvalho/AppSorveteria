using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Comandas;

// Vendas para o painel (docs/03 §6.1). Valores em dinheiro vêm prontos da API (gravados na venda);
// enums como texto ("Balcao"/"Delivery", "Fechada"/"Cancelada", "Dinheiro"/"Pix"/...). Datas em UTC.

/// <summary>Uma página de resultados. <paramref name="TotalItens"/> é o total sem paginação.</summary>
public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int TotalItens);

/// <summary>Linha da lista de vendas. <paramref name="FormasPagamento"/>: formas usadas, sem repetir.</summary>
public sealed record ComandaResumoDto(
    Guid Id,
    int Numero,
    Guid CaixaId,
    string Tipo,
    string Status,
    decimal Total,
    DateTime CriadaEm,
    DateTime? FechadaEm,
    string AtendenteNome,
    IReadOnlyList<string> FormasPagamento,
    bool RecebidaAposFechamentoCaixa);

/// <summary>
/// GET /api/comandas/{id}. <paramref name="CriadaEm"/> é a hora do celular e <paramref name="RecebidaEm"/> a do servidor (RN-CM-12).
/// <paramref name="EstornadaEm"/>, <paramref name="EstornadaPorNome"/> e <paramref name="MotivoEstorno"/> só vêm na venda <c>Estornada</c> (RN-CM-09).
/// </summary>
public sealed record ComandaDetalheDto(
    Guid Id,
    int Numero,
    Guid CaixaId,
    string Tipo,
    string Status,
    decimal Total,
    string? Observacao,
    DateTime CriadaEm,
    DateTime RecebidaEm,
    DateTime? FechadaEm,
    DateTime? CanceladaEm,
    string? MotivoCancelamento,
    DateTime? EstornadaEm,
    string? EstornadaPorNome,
    string? MotivoEstorno,
    bool RecebidaAposFechamentoCaixa,
    string AtendenteNome,
    IReadOnlyList<ItemComandaDto> Itens,
    IReadOnlyList<PagamentoDto> Pagamentos);

/// <summary><paramref name="ProdutoId"/> nulo = item livre (self-service, venda avulsa). Preço e descrição praticados na venda (RN-PR-04).</summary>
public sealed record ItemComandaDto(Guid Id, Guid? ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

/// <summary>RN-PG-01/03. <paramref name="Troco"/> calculado na venda, nunca digitado.</summary>
public sealed record PagamentoDto(Guid Id, string Forma, decimal Valor, decimal ValorRecebido, decimal Troco);

/// <summary>POST /api/comandas/{id}/estornar (Admin) — RN-CM-09.</summary>
public sealed record EstornarComandaRequest(
    [Required(ErrorMessage = "Informe o motivo do estorno.")]
    [StringLength(300, MinimumLength = 3, ErrorMessage = "O motivo do estorno deve ter de 3 a 300 caracteres.")]
    string Motivo);
