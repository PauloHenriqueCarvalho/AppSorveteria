namespace GestaoSorveteria.Domain.Comandas;

/// <summary>Só o que um comparativo precisa de uma venda paga: quando fechou e o total (leitura leve, sem itens).</summary>
public readonly record struct VendaFechada(DateTime FechadaEm, decimal Total);
