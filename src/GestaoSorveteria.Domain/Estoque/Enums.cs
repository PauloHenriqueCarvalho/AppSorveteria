namespace GestaoSorveteria.Domain.Estoque;

/// <summary>RN-ES-02: como o sistema sabe quanto tem de cada item do grupo.</summary>
public enum ModoControle
{
    /// <summary>Picolé, Skimo, Moreninha, massa, açaí: contagem semanal (padrão).</summary>
    Contagem = 1,

    /// <summary>Potes (teste): saldo pela entrega e pela venda com sabor; a contagem confere.</summary>
    SaldoPelaVenda = 2,

    /// <summary>Copos, casquinhas, insumos: sem saldo, só avisos e pedido.</summary>
    SoAviso = 3,
}

/// <summary>RN-ES-01: como o fornecedor vende o item.</summary>
public enum UnidadeCompra
{
    Caixa = 1,
    Balde = 2,
    Unidade = 3,
    Pacote = 4,
}

/// <summary>RN-PF-01: como o pedido chega ao fornecedor (a fábrica dos pais da dona prefere impresso).</summary>
public enum FormaEnvioPedido
{
    Impresso = 1,
    WhatsApp = 2,
    Ambos = 3,
}

/// <summary>RN-AV-02: significado fixo; o valor maior é o mais grave ("Ainda falta" só sobe — RN-AV-03).</summary>
public enum PrioridadeAviso
{
    Media = 1,
    Alta = 2,
    Urgente = 3,
}

public enum StatusAviso
{
    Aberto = 1,
    Fechado = 2,
}

/// <summary>Onde a contagem foi feita (RN-ES-05).</summary>
public enum OrigemContagem
{
    App = 1,
    Painel = 2,
}
