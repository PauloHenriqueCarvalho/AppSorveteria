using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Comandas;

/// <summary>
/// Dados de um pagamento informados no fechamento da comanda.
/// <paramref name="ValorRecebido"/> só faz sentido em dinheiro (quanto o cliente entregou).
/// </summary>
public readonly record struct DadosPagamento(FormaPagamento Forma, decimal Valor, decimal? ValorRecebido = null, Guid? Id = null);

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
    }
}
