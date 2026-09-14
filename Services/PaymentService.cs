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
        public async Task<bool> ProcessarPagamentoAsync(Comanda comanda)
        {
            if (comanda == null) return false;

            // Validação simples: não permitir vendas com valor zero ou negativo
            if (comanda.Total <= 0)
            {
                await Application.Current.MainPage.DisplayAlert("Erro", "Não é possível processar pagamento para vendas com valor zero.", "OK");
                return false;
            }

            string opcao = await Application.Current.MainPage.DisplayActionSheet("Forma de Pagamento", "Cancelar", null, "Dinheiro", "Pix", "Cartão");
            if (string.IsNullOrEmpty(opcao) || opcao == "Cancelar") return false;

            int tipoPagamento = 0;
            double valorRecebido = comanda.Total;

            if (opcao == "Dinheiro")
            {
                // pré-preencher o valor recebido com o total da comanda para agilizar pagamento exato
                string initial = comanda.Total.ToString("F2", CultureInfo.InvariantCulture);
                string recebido = await Application.Current.MainPage.DisplayPromptAsync("Pagamento", "Valor Recebido:", "Confirmar", "Cancelar", keyboard: Keyboard.Telephone, initialValue: initial);
                if (string.IsNullOrWhiteSpace(recebido)) return false;
                string recebidoTratado = recebido.Replace(",", ".");
                if (!double.TryParse(recebidoTratado, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double r))
                {
                    await Application.Current.MainPage.DisplayAlert("Erro", "Valor recebido inválido", "OK");
                    return false;
                }
                valorRecebido = r;
                tipoPagamento = 0;
            }
            else if (opcao == "Cartão")
            {
                tipoPagamento = 1;
                valorRecebido = comanda.Total;
            }
            else if (opcao == "Pix")
            {
                tipoPagamento = 2;
                valorRecebido = comanda.Total;
            }

            double troco = Math.Round(valorRecebido - comanda.Total, 2);
            if (troco < 0) troco = 0;

            if (opcao == "Dinheiro")
            {
                await Application.Current.MainPage.DisplayAlert("Troco", $"Troco a devolver: R$ {troco:F2}", "OK");
            }

            var pagamento = new Pagamento
            {
                Id = Guid.NewGuid().ToString(),
                ComandaId = comanda.Id,
                Tipo = tipoPagamento,
                Valor = valorRecebido,
                DataPagamento = DateTime.Now
            };

            await _dbService.RegistrarPagamento(pagamento);
            await _dbService.FinalizarComanda(comanda.Id);

            return true;
        }
    }
}
