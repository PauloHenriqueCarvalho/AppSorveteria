using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization.Metadata;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Contracts.Sync;

namespace SorveteriaMaui.Services.Api;

/// <summary>
/// Cliente HTTP da API. Um só para o app inteiro: monta a URL a partir de <see cref="ConfiguracaoApi"/>,
/// põe o <c>Authorization: Bearer</c> da <see cref="SessaoUsuario"/> e traduz falhas em <see cref="ApiException"/>
/// com mensagem para a tela. Timeout de 90 s porque a API gratuita dorme e leva ~1 min para acordar (docs/07).
/// </summary>
public sealed class ApiCliente(ConfiguracaoApi configuracao, SessaoUsuario sessao)
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    private readonly HttpClient _http = new() { Timeout = Timeout };

    /// <summary>POST /api/auth/login — PIN do atendente ou senha da dona (RN-US-03).</summary>
    public async Task<LoginResponse> LoginAsync(string login, string senha, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarAsync(
            HttpMethod.Post, "/api/auth/login", new LoginRequest(login, senha), ApiJsonContext.Default.LoginRequest,
            autenticado: false, cancellationToken);

        return await LerAsync(resposta, ApiJsonContext.Default.LoginResponse, cancellationToken);
    }

    /// <summary>
    /// GET /api/produtos?desde= — catálogo (RN-PR-03: inativos vêm junto). Sem <paramref name="desde"/> vem tudo;
    /// com ele, só o que mudou a partir dali (o app guarda o <c>GeradoEmUtc</c> da resposta anterior).
    /// </summary>
    public async Task<CatalogoProdutosResponse> ObterCatalogoAsync(DateTime? desde, CancellationToken cancellationToken = default)
    {
        var rota = desde is { } data
            ? $"/api/produtos?desde={Uri.EscapeDataString(DateTime.SpecifyKind(data, DateTimeKind.Utc).ToString("O"))}"
            : "/api/produtos";

        using var resposta = await EnviarAsync<object>(HttpMethod.Get, rota, null, null, autenticado: true, cancellationToken);
        return await LerAsync(resposta, ApiJsonContext.Default.CatalogoProdutosResponse, cancellationToken);
    }

    /// <summary>POST /api/sync/caixas (RN-SY-02/03). Um resultado por caixa, na ordem do lote.</summary>
    public async Task<SyncResponse> EnviarCaixasAsync(SyncCaixasRequest request, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Post, "/api/sync/caixas", request, ApiJsonContext.Default.SyncCaixasRequest, autenticado: true, cancellationToken);
        return await LerAsync(resposta, ApiJsonContext.Default.SyncResponse, cancellationToken);
    }

    /// <summary>POST /api/sync/comandas (RN-SY-02/03/06). Um resultado por comanda, na ordem do lote.</summary>
    public async Task<SyncResponse> EnviarComandasAsync(SyncComandasRequest request, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Post, "/api/sync/comandas", request, ApiJsonContext.Default.SyncComandasRequest, autenticado: true, cancellationToken);
        return await LerAsync(resposta, ApiJsonContext.Default.SyncResponse, cancellationToken);
    }

    /// <summary>Envia a requisição; 2xx volta para o chamador ler, qualquer outra vira <see cref="ApiException"/>.</summary>
    internal async Task<HttpResponseMessage> EnviarAsync<TCorpo>(
        HttpMethod metodo, string rota, TCorpo? corpo, JsonTypeInfo<TCorpo>? tipoCorpo, bool autenticado, CancellationToken cancellationToken)
        where TCorpo : class
    {
        using var requisicao = new HttpRequestMessage(metodo, new Uri(configuracao.UrlBase + rota));
        if (autenticado)
        {
            var token = await sessao.TokenAsync() ?? throw new SessaoExpiradaException();
            requisicao.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (corpo is not null && tipoCorpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, tipoCorpo);
        }

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.SendAsync(requisicao, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"[Sorveteria] Falha de rede em {metodo} {rota}: {ex.Message}");
            throw new ApiIndisponivelException(
                "Não foi possível falar com o servidor. Confira a internet e o endereço do servidor e tente de novo.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiIndisponivelException(
                "O servidor demorou demais para responder. Se ele estava dormindo, aguarde 1 minuto e tente de novo.");
        }

        if (resposta.IsSuccessStatusCode)
        {
            return resposta;
        }

        using (resposta)
        {
            throw await TraduzirErroAsync(resposta, autenticado, cancellationToken);
        }
    }

    internal static async Task<T> LerAsync<T>(HttpResponseMessage resposta, JsonTypeInfo<T> tipo, CancellationToken cancellationToken)
    {
        try
        {
            return await resposta.Content.ReadFromJsonAsync(tipo, cancellationToken)
                ?? throw new ApiException("O servidor devolveu uma resposta vazia.");
        }
        catch (Exception ex) when (ex is not ApiException and not OperationCanceledException)
        {
            Debug.WriteLine($"[Sorveteria] Resposta inesperada da API: {ex}");
            throw new ApiException("O servidor devolveu uma resposta que o app não entendeu. Confira o endereço do servidor.");
        }
    }

    private static async Task<ApiException> TraduzirErroAsync(HttpResponseMessage resposta, bool autenticado, CancellationToken cancellationToken)
    {
        var status = (int)resposta.StatusCode;
        var problema = await LerProblemaAsync(resposta, cancellationToken);

        return resposta.StatusCode switch
        {
            HttpStatusCode.Unauthorized when autenticado => new SessaoExpiradaException(),
            HttpStatusCode.Unauthorized => new ApiException(problema?.Title ?? "Login ou PIN incorretos.", status),
            HttpStatusCode.Forbidden => new ApiException("Este usuário não tem permissão para isso.", status),
            HttpStatusCode.TooManyRequests => new ApiException("Muitas tentativas. Aguarde 1 minuto e tente de novo.", status),
            HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict =>
                new ApiException(MensagemDoProblema(problema) ?? "O servidor recusou a operação.", status),
            _ when status >= 500 => new ApiIndisponivelException(
                "O servidor está com problema no momento. Tente de novo em alguns minutos.", status),
            _ => new ApiException(MensagemDoProblema(problema) ?? $"O servidor respondeu com erro {status}.", status),
        };
    }

    private static async Task<ProblemaApi?> LerProblemaAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        if (resposta.Content.Headers.ContentType?.MediaType is not ("application/problem+json" or "application/json"))
        {
            return null;
        }

        try
        {
            return await resposta.Content.ReadFromJsonAsync(ApiJsonContext.Default.ProblemaApi, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static string? MensagemDoProblema(ProblemaApi? problema)
    {
        if (problema is null)
        {
            return null;
        }

        // Erros de validação (400) vêm por campo; mostra o primeiro, que já está em português.
        var validacao = problema.Errors?.Values.SelectMany(e => e).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
        if (validacao is not null)
        {
            return validacao;
        }

        // ProblemDetails: título + detalhe (ex.: "Login ou senha inválidos. Confira o login e a senha/PIN…")
        return string.Join(" ", new[] { problema.Title, problema.Detail }.Where(t => !string.IsNullOrWhiteSpace(t))) is { Length: > 0 } texto
            ? texto
            : null;
    }
}
