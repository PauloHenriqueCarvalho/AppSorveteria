using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using SorveteriaMaui.Model;
using System.Globalization;

namespace SorveteriaMaui.Services;

public class PaymentService : IPaymentService
{
    private const string Dinheiro = "Dinheiro";
    private const string Pix = "Pix";
    private const string CartaoDebito = "Cartão débito";
    private const string CartaoCredito = "Cartão crédito";
    private const string Dividir = "Dividir pagamento";

    // RN-PG-01
    private static readonly Dictionary<string, FormaPagamento> Formas = new()
    {
        [Dinheiro] = FormaPagamento.Dinheiro,
        [Pix] = FormaPagamento.Pix,
        [CartaoDebito] = FormaPagamento.CartaoDebito,
        [CartaoCredito] = FormaPagamento.CartaoCredito,
    };

    public async Task<IReadOnlyList<DadosPagamento>?> PerguntarPagamentosAsync(decimal total)
    {
        var opcao = await Shell.Current.DisplayActionSheetAsync($"Pagamento — R$ {total:F2}", "Cancelar", null,
            Dinheiro, Pix, CartaoDebito, CartaoCredito, Dividir);

        if (opcao == Dividir)
        {
            return await PerguntarDivididoAsync(total);
        }

        if (opcao is null || !Formas.TryGetValue(opcao, out var forma))
        {
            return null;
        }

        // Uma forma só: paga o total (mesmos toques de antes)
        var pagamento = await CompletarAsync(forma, total);
        return pagamento is null ? null : [pagamento.Value];
    }

    public Task MostrarTrocoAsync(decimal troco) =>
        Shell.Current.DisplayAlertAsync("Troco", $"Troco a devolver: R$ {troco:F2}", "OK");

    /// <summary>
    /// RN-PG-02: várias formas na mesma comanda (ex.: metade Pix, metade dinheiro). Pergunta forma e valor
    /// até cobrir o total; a soma exata e o troco são conferidos pelo Domain ao fechar (RN-CM-07, RN-PG-03).
    /// </summary>
    private static async Task<IReadOnlyList<DadosPagamento>?> PerguntarDivididoAsync(decimal total)
    {
        var pagina = Shell.Current;
        var pagamentos = new List<DadosPagamento>();
        var restante = total;

        while (restante > 0)
        {
            var titulo = pagamentos.Count == 0
                ? $"Dividir R$ {total:F2} — 1ª forma"
                : $"Falta R$ {restante:F2} — {pagamentos.Count + 1}ª forma";

            var opcao = await pagina.DisplayActionSheetAsync(titulo, "Cancelar", null, Dinheiro, Pix, CartaoDebito, CartaoCredito);
            if (opcao is null || !Formas.TryGetValue(opcao, out var forma))
            {
                return null; // cancelou: nada é gravado
            }

            var texto = await pagina.DisplayPromptAsync(opcao, $"Quanto em {opcao}? (falta R$ {restante:F2})", "Confirmar", "Cancelar",
                keyboard: Keyboard.Telephone, initialValue: Formatar(restante));
            if (texto is null)
            {
                return null;
            }

            if (!Conversoes.TentarLerDinheiro(texto, out var valor) || valor <= 0 || valor > restante)
            {
                await pagina.DisplayAlertAsync("Atenção", $"Informe um valor maior que zero e até R$ {restante:F2}.", "OK");
                continue;
            }

            var pagamento = await CompletarAsync(forma, valor);
            if (pagamento is null)
            {
                return null;
            }

            pagamentos.Add(pagamento.Value);
            restante = Moeda.Arredondar(restante - valor);
        }

        return pagamentos;
    }

    /// <summary>Em dinheiro, pergunta quanto o cliente entregou (pré-preenchido com o valor exato).</summary>
    private static async Task<DadosPagamento?> CompletarAsync(FormaPagamento forma, decimal valor)
    {
        if (forma != FormaPagamento.Dinheiro)
        {
            return new DadosPagamento(forma, valor);
        }

        var pagina = Shell.Current;
        var recebido = await pagina.DisplayPromptAsync($"Dinheiro — R$ {valor:F2}", "Valor recebido:", "Confirmar", "Cancelar",
            keyboard: Keyboard.Telephone, initialValue: Formatar(valor));
        if (recebido is null)
        {
            return null;
        }

        if (!Conversoes.TentarLerDinheiro(recebido, out var valorRecebido))
        {
            await pagina.DisplayAlertAsync("Atenção", "Valor recebido inválido.", "OK");
            return null;
        }

        // RN-PG-03 (B2/B3): Valor abate da comanda; o Domain recusa recebido menor e calcula o troco
        return new DadosPagamento(FormaPagamento.Dinheiro, valor, valorRecebido);
    }

    private static string Formatar(decimal valor) => valor.ToString("F2", CultureInfo.InvariantCulture);
}
