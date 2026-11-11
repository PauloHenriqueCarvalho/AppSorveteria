using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Domain.Comandas;

/// <summary>
/// Dados de um pagamento informados no fechamento da comanda.
/// <paramref name="ValorRecebido"/> só faz sentido em dinheiro (quanto o cliente entregou).
/// <paramref name="TrocoInformado"/>: troco calculado no app, conferido na sincronização (RN-SY-06); nunca é gravado.
/// Nulo = sem conferência; o SyncService sempre repassa o troco recebido do app.
/// <paramref name="PixTxid"/>: só no Pix integrado (RN-PX-05), o txid da cobrança paga; nulo no Pix conferido à mão.
/// </summary>
public readonly record struct DadosPagamento(
    FormaPagamento Forma,
    decimal Valor,
    decimal? ValorRecebido = null,
    Guid? Id = null,
    decimal? TrocoInformado = null,
    string? PixTxid = null);

/// <summary>
/// RN-PG-03/04: Valor abate da comanda; em dinheiro, Troco = ValorRecebido − Valor, calculado aqui, nunca digitado.
/// Criado somente pela <see cref="Comanda"/>.
/// </summary>
public sealed class Pagamento : Entity
{
    public Guid ComandaId { get; private set; }
    public FormaPagamento Forma { get; private set; }
    public decimal Valor { get; private set; }
    public decimal ValorRecebido { get; private set; }
    public decimal Troco { get; private set; }

    /// <summary>RN-PX-05: txid da cobrança Pix integrada que pagou; nulo em Pix conferido à mão e nas outras formas.</summary>
    public string? PixTxid { get; private set; }

    // EF Core
    private Pagamento()
    {
    }

    internal Pagamento(Guid comandaId, DadosPagamento dados) : base(dados.Id)
    {
        Guard.Contra(!Enum.IsDefined(dados.Forma), "Forma de pagamento desconhecida.");
        ComandaId = comandaId;
        Forma = dados.Forma;
        Valor = Guard.Dinheiro(dados.Valor, "o valor do pagamento", permiteZero: false);

        if (Forma == FormaPagamento.Dinheiro)
        {
            ValorRecebido = Guard.Dinheiro(dados.ValorRecebido ?? Valor, "o valor recebido", permiteZero: false);
            Guard.Contra(ValorRecebido < Valor, "O valor recebido em dinheiro é menor que o valor a pagar.");
            Troco = Moeda.Arredondar(ValorRecebido - Valor);
        }
        else
        {
            Guard.Contra(dados.ValorRecebido is { } recebido && recebido != Valor,
                "Troco só existe em pagamento em dinheiro (RN-PG-03).");
            ValorRecebido = Valor;
            Troco = 0m;
        }

        if (!string.IsNullOrWhiteSpace(dados.PixTxid))
        {
            var txid = dados.PixTxid.Trim();
            Guard.Contra(Forma != FormaPagamento.Pix, "Só pagamento em Pix pode ter cobrança Pix (RN-PX-05).");
            Guard.Contra(!CobrancaPix.TxidValido(txid), "O identificador da cobrança Pix (txid) é inválido (RN-PX-05).");
            PixTxid = txid;
        }

        Guard.Contra(dados.TrocoInformado is { } informado && informado != Troco,
            $"O troco enviado ({dados.TrocoInformado:N2}) não confere com o calculado ({Troco:N2}) (RN-PG-04).");
    }
}
