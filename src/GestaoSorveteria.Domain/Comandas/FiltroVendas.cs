namespace GestaoSorveteria.Domain.Comandas;

/// <summary>
/// Filtros da lista de vendas do painel (docs/03 §6.1): situação, quem vendeu e forma de pagamento.
/// <see cref="Forma"/> pega a venda que teve pelo menos um pagamento nessa forma (pagamento dividido aparece nas duas).
/// </summary>
public sealed record FiltroVendas(StatusComanda? Status = null, Guid? UsuarioId = null, FormaPagamento? Forma = null);
