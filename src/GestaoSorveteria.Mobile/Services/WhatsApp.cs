using System.Diagnostics;

namespace SorveteriaMaui.Services;

/// <summary>
/// Abre o WhatsApp do aparelho com a mensagem pronta (link <c>wa.me</c>, docs/10 §2.3). O atendente só toca em enviar.
/// O app nunca afirma que a mensagem foi entregue, só que o WhatsApp foi aberto (RN-AV-08). Sem internet o link abre
/// do mesmo jeito: o próprio WhatsApp guarda a mensagem e envia quando a conexão voltar.
/// </summary>
public static class WhatsApp
{
    /// <summary>
    /// Abre <paramref name="link"/>. Se o aparelho não conseguir (sem WhatsApp nem navegador), mostra
    /// <paramref name="mensagem"/> com a opção de copiar, para mandar por outro meio. Devolve <c>true</c> se abriu.
    /// </summary>
    public static async Task<bool> AbrirAsync(string link, string mensagem)
    {
        try
        {
            if (await Microsoft.Maui.ApplicationModel.Launcher.Default.OpenAsync(new Uri(link)))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] WhatsApp não abriu: {ex}");
        }

        var copiar = await MainThread.InvokeOnMainThreadAsync<bool>(() => Shell.Current.DisplayAlertAsync(
            "Não deu para abrir o WhatsApp",
            $"Mande esta mensagem para a dona por outro celular:\n\n{mensagem}",
            "Copiar mensagem",
            "Fechar"));
        if (copiar)
        {
            await Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.SetTextAsync(mensagem);
            AvisoRapido.Mostrar("Mensagem copiada");
        }

        return false;
    }
}
