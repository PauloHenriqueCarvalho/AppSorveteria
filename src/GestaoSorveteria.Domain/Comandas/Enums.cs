namespace GestaoSorveteria.Domain.Comandas;

public enum StatusComanda
{
    Aberta = 1,
    Fechada = 2,

    /// <summary>RN-CM-08: comanda aberta cancelada pelo atendente.</summary>
    Cancelada = 3,

    /// <summary>RN-CM-09: comanda fechada estornada pela dona no painel. Não mexe em caixa nenhum.</summary>
    Estornada = 4,
}

/// <summary>RN-CM-02/11: na Fase 1, Delivery é só marcação + observação.</summary>
public enum TipoComanda
{
    Balcao = 1,
    Delivery = 2,
}

/// <summary>RN-PG-01.</summary>
public enum FormaPagamento
{
    Dinheiro = 1,
    Pix = 2,
    CartaoDebito = 3,
    CartaoCredito = 4,
}
