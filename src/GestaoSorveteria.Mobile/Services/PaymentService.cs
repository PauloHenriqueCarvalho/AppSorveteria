using GestaoSorveteria.Domain.Comandas;
using SorveteriaMaui.Model;
using System.Globalization;

namespace SorveteriaMaui.Services;

public class PaymentService : IPaymentService
{
    private const string Dinheiro = "Dinheiro";
    private const string Pix = "Pix";
    private const string CartaoDebito = "Cartão débito";
    private const string CartaoCredito = "Cartão crédito";

    public async Task<DadosPagamento?> PerguntarPagamentoAsync(decimal total)
    {
        var pagina = Shell.Current;

        // RN-PG-01
        var opcao = await pagina.DisplayActionSheetAsync($"Pagamento — R$ {total:F2}", "Cancelar", null, Dinheiro, Pix, CartaoDebito, CartaoCredito);
        switch (opcao)
        {
            case Dinheiro:
                // pré-preenche com o total para agilizar o pagamento exato
                var recebido = await pagina.DisplayPromptAsync("Pagamento em dinheiro", "Valor recebido:", "Confirmar", "Cancelar",
                    keyboard: Keyboard.Telephone, initialValue: total.ToString("F2", CultureInfo.InvariantCulture));
                if (recebido is null)
                {
                    return null;
                }

                if (!Conversoes.TentarLerDinheiro(recebido, out var valorRecebido))
                {
                    await pagina.DisplayAlertAsync("Atenção", "Valor recebido inválido.", "OK");
                    return null;
                }

                // RN-PG-03 (B2/B3): Valor = total da comanda; o Domain recusa recebido menor e calcula o troco
                return new DadosPagamento(FormaPagamento.Dinheiro, total, valorRecebido);
            case Pix:
                return new DadosPagamento(FormaPagamento.Pix, total);
            case CartaoDebito:
                return new DadosPagamento(FormaPagamento.CartaoDebito, total);
            case CartaoCredito:
                return new DadosPagamento(FormaPagamento.CartaoCredito, total);
            default:
                return null;
        }
    }

    public Task MostrarTrocoAsync(decimal troco) =>
        Shell.Current.DisplayAlertAsync("Troco", $"Troco a devolver: R$ {troco:F2}", "OK");
}
