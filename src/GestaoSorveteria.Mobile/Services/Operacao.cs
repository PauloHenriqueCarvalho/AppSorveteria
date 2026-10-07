using System.Diagnostics;
using GestaoSorveteria.Domain.Common;

namespace SorveteriaMaui.Services;

/// <summary>
/// B7: nenhuma falha é silenciosa. Toda operação da tela passa por aqui; se falhar, o atendente vê um alerta
/// e quem chamou recebe <c>false</c> para parar o fluxo (não mostra "Sucesso", não navega, não segue para o pagamento).
/// </summary>
public static class Operacao
{
    /// <summary>Gravação: comanda, item, pagamento, produto. As gravações são transacionais, então nada fica pela metade.</summary>
    public static Task<bool> GravarAsync(Func<Task> acao) =>
        ExecutarAsync(acao, "Não foi possível salvar",
            "O celular não conseguiu gravar esta operação e nada foi salvo. Tente de novo; se continuar, chame o responsável.");

    /// <summary>Leitura para a tela (listas, detalhes).</summary>
    public static Task<bool> CarregarAsync(Func<Task> acao) =>
        ExecutarAsync(acao, "Não foi possível carregar",
            "O celular não conseguiu ler os dados. Volte e abra a tela de novo; se continuar, chame o responsável.");

    private static async Task<bool> ExecutarAsync(Func<Task> acao, string titulo, string mensagem)
    {
        try
        {
            await acao();
            return true;
        }
        catch (DomainException ex)
        {
            // Regra recusada (ex.: dinheiro insuficiente): a mensagem do Domain já está pronta para a tela.
            await AvisarAsync("Atenção", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] {titulo}: {ex}");
            await AvisarAsync(titulo, $"{mensagem}\n\nDetalhe: {ex.Message}");
            return false;
        }
    }

    private static Task AvisarAsync(string titulo, string mensagem) =>
        MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(titulo, mensagem, "OK"));
}
