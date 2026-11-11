using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Infrastructure.Pix;

/// <summary>
/// RN-PX-01: API Pix do Banco Inter (padrão do Banco Central: <c>/pix/v2/cob/{txid}</c>). Autenticação OAuth2
/// client_credentials com certificado do cliente (mTLS) — o certificado vai no <see cref="HttpClient"/> (ver
/// <see cref="CriarHandler"/>). Token guardado em memória até perto de vencer. Nenhuma credencial vai para mensagem de erro.
/// </summary>
public sealed class InterProvedorPix : IProvedorPix
{
    public const string EnderecoProducao = "https://cdpj.partners.bancointer.com.br/";
    public const string EnderecoSandbox = "https://cdpj-sandbox.partners.uatinter.co/";
    public const string Escopos = "cob.write cob.read";

    private static readonly TimeSpan FolgaToken = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly PixOptions _opcoes;
    private readonly TimeProvider _relogio;
    private readonly SemaphoreSlim _travaToken = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenVence;

    public InterProvedorPix(HttpClient http, PixOptions opcoes, TimeProvider? relogio = null)
    {
        _http = http;
        _opcoes = opcoes;
        _relogio = relogio ?? TimeProvider.System;
        _http.BaseAddress ??= new Uri(opcoes.Sandbox ? EnderecoSandbox : EnderecoProducao);
    }

    public string Nome => PixOptions.ProvedorInter;

    public bool Configurado => true;

    public int ExpiracaoSegundos => _opcoes.ExpiracaoSegundos;

    /// <summary>Handler com o certificado da integração (mTLS). Recriado em PFX para funcionar também no Windows.</summary>
    public static SocketsHttpHandler CriarHandler(PixOptions opcoes)
    {
        using var pem = System.Security.Cryptography.X509Certificates.X509Certificate2.CreateFromPem(
            opcoes.CertificadoPem, opcoes.ChavePrivadaPem);
        var certificado = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12(
            pem.Export(System.Security.Cryptography.X509Certificates.X509ContentType.Pkcs12), password: null);
        var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) };
        handler.SslOptions.ClientCertificates = new System.Security.Cryptography.X509Certificates.X509CertificateCollection { certificado };
        return handler;
    }

    public async Task<CobrancaNoBanco> CriarAsync(NovaCobrancaPix cobranca, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cobranca);
        var corpo = new CobRequisicao(
            new Calendario(cobranca.ExpiracaoSegundos),
            new ValorCob(cobranca.Valor.ToString("0.00", CultureInfo.InvariantCulture)),
            _opcoes.Chave.Trim(),
            cobranca.Descricao);

        using var resposta = await EnviarAsync(HttpMethod.Put, Caminho(cobranca.Txid), corpo, cancellationToken);
        if (resposta.IsSuccessStatusCode)
        {
            return await LerAsync(resposta, cancellationToken);
        }

        if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            // Txid já usado (pedido repetido depois de uma queda): a cobrança existente vale.
            using var existente = await EnviarAsync(HttpMethod.Get, Caminho(cobranca.Txid), null, cancellationToken);
            if (existente.IsSuccessStatusCode)
            {
                return await LerAsync(existente, cancellationToken);
            }
        }

        throw await ErroAsync(resposta, "criar a cobrança Pix", cancellationToken);
    }

    public async Task<CobrancaNoBanco> ConsultarAsync(string txid, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Get, Caminho(txid), null, cancellationToken);
        return resposta.IsSuccessStatusCode
            ? await LerAsync(resposta, cancellationToken)
            : throw await ErroAsync(resposta, "consultar a cobrança Pix", cancellationToken);
    }

    public async Task<CobrancaNoBanco> CancelarAsync(string txid, CancellationToken cancellationToken = default)
    {
        using var resposta = await EnviarAsync(HttpMethod.Patch, Caminho(txid), new RevisaoStatus("REMOVIDA_PELO_USUARIO_RECEBEDOR"), cancellationToken);
        if (resposta.IsSuccessStatusCode)
        {
            return await LerAsync(resposta, cancellationToken);
        }

        // Paga no último segundo: o banco recusa a remoção. A consulta diz como ficou.
        if (resposta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
        {
            return await ConsultarAsync(txid, cancellationToken);
        }

        throw await ErroAsync(resposta, "cancelar a cobrança Pix", cancellationToken);
    }

    private static string Caminho(string txid)
    {
        if (!CobrancaPix.TxidValido(txid))
        {
            throw new ProvedorPixException("Identificador da cobrança Pix inválido.");
        }

        return "pix/v2/cob/" + txid;
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string caminho, object? corpo, CancellationToken cancellationToken)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var token = await TokenAsync(cancellationToken);
            using var pedido = new HttpRequestMessage(metodo, caminho);
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrWhiteSpace(_opcoes.ContaCorrente))
            {
                pedido.Headers.Add("x-conta-corrente", _opcoes.ContaCorrente.Trim());
            }

            if (corpo is not null)
            {
                pedido.Content = JsonContent.Create(corpo, corpo.GetType(), options: Json);
            }

            HttpResponseMessage resposta;
            try
            {
                resposta = await _http.SendAsync(pedido, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new ProvedorPixException("O banco não respondeu. Confira a internet e tente de novo.", ex);
            }

            if (resposta.StatusCode != HttpStatusCode.Unauthorized || tentativa > 1)
            {
                return resposta;
            }

            // Token revogado ou vencido antes da hora: pega outro e repete uma vez
            resposta.Dispose();
            _token = null;
        }
    }

    private async Task<string> TokenAsync(CancellationToken cancellationToken)
    {
        if (_token is not null && _relogio.GetUtcNow() < _tokenVence)
        {
            return _token;
        }

        await _travaToken.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && _relogio.GetUtcNow() < _tokenVence)
            {
                return _token;
            }

            using var pedido = new HttpRequestMessage(HttpMethod.Post, "oauth/v2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _opcoes.ClientId.Trim(),
                    ["client_secret"] = _opcoes.ClientSecret.Trim(),
                    ["grant_type"] = "client_credentials",
                    ["scope"] = Escopos,
                }),
            };

            HttpResponseMessage resposta;
            try
            {
                resposta = await _http.SendAsync(pedido, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                throw new ProvedorPixException("O banco não respondeu. Confira a internet e tente de novo.", ex);
            }

            using (resposta)
            {
                if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.BadRequest)
                {
                    throw new ProvedorPixException(
                        "O banco recusou as credenciais do Pix integrado (Client ID, Secret ou certificado). Confira a integração no Inter.");
                }

                if (!resposta.IsSuccessStatusCode)
                {
                    throw new ProvedorPixException($"O banco está fora do ar (erro {(int)resposta.StatusCode}). Tente de novo em instantes.");
                }

                var token = await LerJsonAsync<TokenResposta>(resposta, cancellationToken);
                if (string.IsNullOrWhiteSpace(token.AccessToken))
                {
                    throw new ProvedorPixException("O banco respondeu sem o token de acesso.");
                }

                var validade = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn ?? 3600));
                _token = token.AccessToken;
                _tokenVence = _relogio.GetUtcNow() + validade - FolgaToken;
                return _token;
            }
        }
        finally
        {
            _travaToken.Release();
        }
    }

    private static async Task<CobrancaNoBanco> LerAsync(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        var cob = await LerJsonAsync<CobResposta>(resposta, cancellationToken);
        if (string.IsNullOrWhiteSpace(cob.Txid) || cob.Valor?.Original is null
            || !decimal.TryParse(cob.Valor.Original, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor))
        {
            throw new ProvedorPixException("O banco respondeu a cobrança Pix sem txid ou valor.");
        }

        var status = (cob.Status ?? string.Empty).ToUpperInvariant() switch
        {
            "ATIVA" => StatusCobrancaPix.Ativa,
            "CONCLUIDA" => StatusCobrancaPix.Concluida,
            "REMOVIDA_PELO_USUARIO_RECEBEDOR" or "REMOVIDA_PELO_PSP" => StatusCobrancaPix.Removida,
            _ => throw new ProvedorPixException($"Situação da cobrança Pix desconhecida: {cob.Status}."),
        };

        // RN-PX-04: soma de todos os Pix recebidos na cobrança; qualquer valor ilegível deixa o pago desconhecido (não libera)
        var recebidos = cob.Pix ?? [];
        decimal? pago = recebidos.Count == 0 ? null : 0m;
        DateTime? horario = null;
        string? endToEnd = null;
        foreach (var recebido in recebidos)
        {
            pago = recebido.Valor is { } texto && decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) && pago is { } soma
                ? soma + v
                : null;
            var quando = recebido.Horario is { } h && DateTimeOffset.TryParse(h, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
                ? d.UtcDateTime
                : (DateTime?)null;
            if (horario is null || quando > horario)
            {
                horario = quando ?? horario;
                endToEnd = recebido.EndToEndId ?? endToEnd;
            }
        }

        var situacao = status == StatusCobrancaPix.Concluida
            ? new SituacaoCobrancaPix(status, endToEnd, pago, horario)
            : new SituacaoCobrancaPix(status);
        return new CobrancaNoBanco(cob.Txid, valor, cob.PixCopiaECola ?? string.Empty, situacao);
    }

    private static async Task<T> LerJsonAsync<T>(HttpResponseMessage resposta, CancellationToken cancellationToken)
    {
        try
        {
            return await resposta.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                ?? throw new ProvedorPixException("O banco respondeu vazio.");
        }
        catch (JsonException ex)
        {
            throw new ProvedorPixException("O banco respondeu num formato inesperado.", ex);
        }
    }

    private static async Task<ProvedorPixException> ErroAsync(HttpResponseMessage resposta, string acao, CancellationToken cancellationToken)
    {
        var status = (int)resposta.StatusCode;
        if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return new ProvedorPixException($"O banco recusou o acesso ao {acao}. Confira as permissões da integração no Inter (cob.write e cob.read).");
        }

        if (status >= 500 || resposta.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return new ProvedorPixException($"O banco está fora do ar ou ocupado (erro {status}). Tente de novo em instantes.");
        }

        // Erros de negócio do padrão do Banco Central vêm em problem+json; o "detail" é seguro para mostrar.
        string? detalhe = null;
        try
        {
            var problema = await resposta.Content.ReadFromJsonAsync<Problema>(Json, cancellationToken);
            detalhe = problema?.Detail ?? problema?.Title;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
        }

        return new ProvedorPixException(string.IsNullOrWhiteSpace(detalhe)
            ? $"O banco não aceitou {acao} (erro {status})."
            : $"O banco não aceitou {acao}: {Limitar(detalhe)}");
    }

    private static string Limitar(string texto) => texto.Length <= 200 ? texto : texto[..200];

    private sealed record TokenResposta(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);

    private sealed record Calendario(int Expiracao);

    private sealed record ValorCob(string? Original);

    private sealed record CobRequisicao(Calendario Calendario, ValorCob Valor, string Chave, string? SolicitacaoPagador);

    private sealed record RevisaoStatus(string Status);

    private sealed record PixRecebido(string? EndToEndId, string? Valor, string? Horario);

    private sealed record CobResposta(string? Txid, string? Status, ValorCob? Valor, string? PixCopiaECola, IReadOnlyList<PixRecebido>? Pix);

    private sealed record Problema(string? Title, string? Detail);
}
