using System.Diagnostics;

namespace SorveteriaMaui.Services;

/// <summary>
/// B11: confirmação rápida que some sozinha e não pede toque (toast nativo do Android).
/// Sem pacote novo: o CommunityToolkit.Maui exige MAUI mais novo que o do projeto.
/// No Windows (só para testar no PC) vai para o log de debug.
/// </summary>
public static class AvisoRapido
{
    public static void Mostrar(string texto)
    {
#if ANDROID
        MainThread.BeginInvokeOnMainThread(() =>
            Android.Widget.Toast.MakeText(Platform.CurrentActivity ?? Android.App.Application.Context, texto, Android.Widget.ToastLength.Short)?.Show());
#else
        Debug.WriteLine($"[Aviso] {texto}");
#endif
    }
}
