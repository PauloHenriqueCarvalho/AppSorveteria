using SorveteriaMaui.Model;
using ItemDominio = GestaoSorveteria.Domain.Comandas.ItemComanda;

namespace SorveteriaMaui.Services;

/// <summary>
/// RN-ES-04: tocar no número da quantidade abre o teclado numérico. "50 picolés" em 3 toques, em vez de 49 no "+".
/// </summary>
public static class PedirQuantidade
{
    /// <summary>Quantidade digitada (1 a 999) ou <c>null</c> se o atendente cancelou ou não digitou nada.</summary>
    public static async Task<int?> PerguntarAsync(string titulo, int atual)
    {
        var texto = await Shell.Current.DisplayPromptAsync(
            titulo,
            $"Quantidade (1 a {ItemDominio.QuantidadeMaxima}):",
            "OK",
            "Cancelar",
            placeholder: atual.ToString(),
            maxLength: 3,
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        if (!Conversoes.TentarLerQuantidade(texto, out var quantidade))
        {
            await Shell.Current.DisplayAlertAsync("Atenção", $"Digite um número de 1 a {ItemDominio.QuantidadeMaxima}.", "OK");
            return null;
        }

        return quantidade;
    }
}
