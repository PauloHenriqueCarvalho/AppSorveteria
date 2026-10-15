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

/// <summary>RN-PF-08: Rascunho → Enviado → Recebido; ou Cancelado (com motivo) a partir de Rascunho/Enviado.</summary>
public enum StatusPedido
{
    Rascunho = 1,
    Enviado = 2,
    Recebido = 3,
    Cancelado = 4,
}

/// <summary>De onde veio a linha do pedido (aparece marcada para a dona).</summary>
public enum OrigemLinhaPedido
{
    /// <summary>Conta da saída semanal ou do estoque alvo (RN-PF-02).</summary>
    Sugestao = 1,

    /// <summary>Aviso "Está acabando" aberto (RN-PF-03).</summary>
    Aviso = 2,

    /// <summary>Faltou na última entrega (RN-RC-04).</summary>
    FaltouAnterior = 3,

    /// <summary>A dona acrescentou à mão.</summary>
    AdicionadoPelaDona = 4,

    /// <summary>Veio na entrega sem estar no pedido (sabor trocado, RN-RC-01).</summary>
    NaoPedido = 5,
}

/// <summary>RN-RC-02: resultado de cada linha na conferência da entrega.</summary>
public enum SituacaoRecebimento
{
    Certo = 1,
    Faltou = 2,
    VeioAMais = 3,
    NaoPedido = 4,
}

/// <summary>RN-ES-08: o que mexe no saldo dos potes (grupo <c>SaldoPelaVenda</c>). A contagem não é movimento: ela é a nova base.</summary>
public enum TipoMovimentoEstoque
{
    /// <summary>Entrega conferida (RN-RC-03).</summary>
    Entrada = 1,

    /// <summary>Venda fechada que chegou ao servidor (RN-ES-09).</summary>
    BaixaVenda = 2,

    /// <summary>Venda estornada pelo painel: devolve o que a baixa tirou.</summary>
    EstornoVenda = 3,
}
