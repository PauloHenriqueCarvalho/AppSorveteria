using GestaoSorveteria.Domain.Comandas;

namespace SorveteriaMaui.Services;

/// <summary>Diálogos do pagamento. Não grava nada: quem valida e grava é o <see cref="ComandaAppService"/>.</summary>
public interface IPaymentService
{
    /// <summary>Pergunta a forma (e, em dinheiro, quanto o cliente entregou). Nulo = atendente cancelou.</summary>
    Task<DadosPagamento?> PerguntarPagamentoAsync(decimal total);

    Task MostrarTrocoAsync(decimal troco);
}
