using FormaPagamento = GestaoSorveteria.Domain.Comandas.FormaPagamento;
using SorveteriaMaui.Model;
using System;
using System.Threading.Tasks;
using System.Globalization;

namespace SorveteriaMaui.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly DatabaseService _dbService;

        public PaymentService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        // Processa o fluxo de pagamento para uma comanda: exibe opções, pede valor recebido (se necessário), registra pagamento e finaliza comanda.
        // Retorna true se pagamento e finalização foram realizados com sucesso.
        // B2/B3 (valor = recebido, aceita dinheiro insuficiente) serão corrigidos na Etapa B, com o pagamento pelo Domain.
        public async Task<bool> ProcessarPagamentoAsync(Comanda comanda)
        {
            if (comanda == null) return false;

            // Validação simples: não permitir vendas com valor zero ou negativo
            if (comanda.Total <= 0)
            {
                await Application.Current.MainPage.DisplayAlert("Erro", "Não é possível processar pagamento para vendas com valor zero.", "OK");
                return false;
            }

            // RN-PG-01
            string opcao = await Application.Current.MainPage.DisplayActionSheet("Forma de Pagamento", "Cancelar", null, "Dinheiro", "Pix", "Cartão débito", "Cartão crédito");
            if (string.IsNullOrEmpty(opcao) || opcao == "Cancelar") return false;

            FormaPagamento forma;
            decimal valorRecebido = comanda.Total;

            switch (opcao)
            {
                case "Dinheiro":
                    // pré-preencher o valor recebido com o total da comanda para agilizar pagamento exato
                    string initial = comanda.Total.ToString("F2", CultureInfo.InvariantCulture);
                    string recebido = await Application.Current.MainPage.DisplayPromptAsync("Pagamento", "Valor Recebido:", "Confirmar", "Cancelar", keyboard: Keyboard.Telephone, initialValue: initial);
                    if (string.IsNullOrWhiteSpace(recebido)) return false;
                    if (!Conversoes.TentarLerDinheiro(recebido, out var r))
                    {
                        await Application.Current.MainPage.DisplayAlert("Erro", "Valor recebido inválido", "OK");
                        return false;
                    }
                    valorRecebido = r;
                    forma = FormaPagamento.Dinheiro;
                    break;
                case "Pix":
                    forma = FormaPagamento.Pix;
                    break;
                case "Cartão débito":
                    forma = FormaPagamento.CartaoDebito;
                    break;
                case "Cartão crédito":
                    forma = FormaPagamento.CartaoCredito;
                    break;
                default:
                    return false;
            }

            decimal troco = valorRecebido - comanda.Total;
            if (troco < 0) troco = 0;

            if (forma == FormaPagamento.Dinheiro)
            {
                await Application.Current.MainPage.DisplayAlert("Troco", $"Troco a devolver: R$ {troco:F2}", "OK");
            }

            var pagamento = new Pagamento
            {
                ComandaId = comanda.Id,
                Forma = forma,
                Valor = valorRecebido,
                ValorRecebido = valorRecebido,
                PagoEm = DateTime.UtcNow
            };

            await _dbService.RegistrarPagamento(pagamento);
            await _dbService.FinalizarComanda(comanda.Id);

            return true;
        }
    }
}
