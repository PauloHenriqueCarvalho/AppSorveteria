namespace GestaoSorveteria.Server.Security;

/// <summary>
/// CORS do painel da dona (site estático no Cloudflare Pages). As origens vêm de <c>Cors:PainelOrigem</c>
/// (variável <c>Cors__PainelOrigem</c> em produção) — nunca fixas no código. Várias origens: separar por <c>;</c>.
/// Sem origem configurada, nenhum site consegue chamar a API pelo navegador (o app não usa CORS).
/// </summary>
public static class CorsPainel
{
    public const string Politica = "Painel";
    public const string ChaveConfiguracao = "Cors:PainelOrigem";

    public static string[] LerOrigens(IConfiguration configuracao)
    {
        var origens = (configuracao[ChaveConfiguracao] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origem => origem.TrimEnd('/'))
            .ToArray();

        foreach (var origem in origens)
        {
            var valida = Uri.TryCreate(origem, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                && uri.AbsolutePath == "/"
                && uri.Query.Length == 0
                && uri.Fragment.Length == 0;
            if (!valida)
            {
                throw new InvalidOperationException(
                    $"{ChaveConfiguracao} inválido: \"{origem}\". Use só esquema e domínio, ex.: https://painel.pages.dev");
            }
        }

        return origens;
    }
}
