namespace SorveteriaMaui.Services.Api;

/// <summary>
/// Endereço da API (docs/07). Fica em <see cref="Preferences"/> e pode ser trocado na tela de login:
/// no emulador Android <c>10.0.2.2</c> é o PC; no tablet de verdade usa-se o IP do PC na rede local
/// (ex.: <c>http://192.168.0.10:5080</c>) e, depois do deploy, a URL do Render.
/// </summary>
public sealed class ConfiguracaoApi
{
    private const string Chave = "api_url";

#if DEBUG
    public static string UrlPadrao =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080" : "http://localhost:5080";
#else
    // Trocar pela URL publicada no Render quando o deploy existir (docs/07). Até lá é editável na tela de login.
    public const string UrlPadrao = "https://sorveteria-api.onrender.com";
#endif

    /// <summary>URL base sem barra no fim (ex.: <c>http://10.0.2.2:5080</c>).</summary>
    public string UrlBase => Preferences.Default.Get(Chave, UrlPadrao);

    /// <summary>Guarda a URL digitada. Devolve <c>false</c> se não for um endereço http/https válido.</summary>
    public bool TentarSalvar(string? url)
    {
        if (!TentarNormalizar(url, out var normalizada))
        {
            return false;
        }

        Preferences.Default.Set(Chave, normalizada);
        return true;
    }

    public static bool TentarNormalizar(string? url, out string normalizada)
    {
        normalizada = (url ?? string.Empty).Trim().TrimEnd('/');
        return Uri.TryCreate(normalizada, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
