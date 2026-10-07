using System.Reflection;

namespace SorveteriaMaui.Services.Api;

/// <summary>
/// Endereço da API (docs/07, "App do atendente: endereço da API").
/// <list type="bullet">
/// <item><b>Debug</b> (emulador / tablet de teste): padrão <c>http://10.0.2.2:5080</c>, editável na tela de login
/// ("Servidor: …") para apontar ao IP do PC na rede; http permitido.</item>
/// <item><b>Release</b> (APK da loja): a URL é fixa, vem do build (<c>-p:ApiUrlPadrao=https://…</c>, gravada como
/// metadado do assembly); a tela de login não mostra o campo; só https.</item>
/// </list>
/// </summary>
public sealed class ConfiguracaoApi
{
    private const string Chave = "api_url";

#if DEBUG
    public static string UrlPadrao =>
        DeviceInfo.Platform == DevicePlatform.Android ? "http://10.0.2.2:5080" : "http://localhost:5080";

    /// <summary>Em Debug a API local é http (emulador / IP do PC).</summary>
    public const bool PermiteHttp = true;

    /// <summary>Em Debug o atendente/Paulo pode trocar o endereço na tela de login.</summary>
    public const bool UrlEditavel = true;
#else
    // Sem hostname fixo no código: um subdomínio "chutado" do Render poderia ser de terceiros e receber login + PIN.
    // A URL publicada entra no build do APK (docs/07); sem ela o app avisa e não tenta logar.
    public static readonly string UrlPadrao =
        typeof(ConfiguracaoApi).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "ApiUrlPadrao")?.Value?.Trim().TrimEnd('/') ?? string.Empty;

    /// <summary>Em produção só https: o PIN não pode trafegar em texto puro.</summary>
    public const bool PermiteHttp = false;

    /// <summary>Em Release a URL é a do build; a tela de login não mostra o campo.</summary>
    public const bool UrlEditavel = false;
#endif

    /// <summary>URL base sem barra no fim (ex.: <c>http://10.0.2.2:5080</c>); vazia se o build Release veio sem URL.</summary>
    public string UrlBase => UrlEditavel ? Preferences.Default.Get(Chave, UrlPadrao) : UrlPadrao;

    public bool EstaConfigurada => UrlBase.Length > 0;

    /// <summary>Guarda a URL digitada (só em Debug). Devolve <c>false</c> se não for um endereço válido.</summary>
    public bool TentarSalvar(string? url)
    {
        if (!UrlEditavel || !TentarNormalizar(url, out var normalizada))
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
            && (uri.Scheme == Uri.UriSchemeHttps || (PermiteHttp && uri.Scheme == Uri.UriSchemeHttp));
    }
}
