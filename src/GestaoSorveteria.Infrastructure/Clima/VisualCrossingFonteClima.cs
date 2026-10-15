using System.Globalization;
using System.Net;
using System.Text.Json;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Clima;

namespace GestaoSorveteria.Infrastructure.Clima;

/// <summary>Configuração do clima (seção <c>Clima</c>; em produção, <c>Clima__ChaveApi</c> no Render — docs/07).</summary>
public sealed class ClimaOptions
{
    public const string Secao = "Clima";

    /// <summary>Chave do Visual Crossing. Vazia = coleta desligada (RN-CL-07).</summary>
    public string ChaveApi { get; set; } = string.Empty;

    /// <summary>
    /// De quanto em quanto tempo a API acordada coleta (RN-CL-04). A recoleta de um dia ainda aberto espera 3 h (fixo,
    /// <c>ClimaDia.IntervaloRecoleta</c>): com intervalo menor, as rodadas do meio só completam dias que faltam.
    /// </summary>
    public int IntervaloHoras { get; set; } = IntervaloPadraoHoras;

    public string Endereco { get; set; } = EnderecoPadrao;

    public const int IntervaloPadraoHoras = 3;

    public const string EnderecoPadrao = "https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/";

    /// <summary>O que <see cref="Normalizar"/> corrigiu (o serviço em segundo plano registra no log ao ligar).</summary>
    public IReadOnlyList<string> Correcoes { get; private set; } = [];

    /// <summary>
    /// O clima é opcional: configuração errada não pode impedir a API de subir (e a sincronização das vendas). Valor fora da
    /// faixa volta ao padrão; devolve o que foi corrigido, para o log.
    /// </summary>
    public IReadOnlyList<string> Normalizar()
    {
        var corrigido = new List<string>();
        if (IntervaloHoras is < 1 or > 24)
        {
            corrigido.Add($"Clima:IntervaloHoras = {IntervaloHoras} fora de 1–24; usando {IntervaloPadraoHoras}.");
            IntervaloHoras = IntervaloPadraoHoras;
        }

        if (!Uri.TryCreate(Endereco, UriKind.Absolute, out var endereco) || endereco.Scheme != Uri.UriSchemeHttps)
        {
            corrigido.Add("Clima:Endereco não é uma URL https; usando o endereço do Visual Crossing.");
            Endereco = EnderecoPadrao;
        }

        Correcoes = [.. Correcoes, .. corrigido];
        return corrigido;
    }
}

/// <summary>
/// RN-CL-07: Visual Crossing Timeline API. Uma chamada traz dias passados (observados) e futuros (previsão), cada um com
/// <c>source</c> (<c>obs</c>, <c>comb</c>, <c>fcst</c>). Plano grátis: uso comercial, 1.000 registros (dias) por dia.
/// A chave vai na URL: esta classe nunca registra a URL em log nem a coloca nas mensagens de erro.
/// </summary>
public sealed class VisualCrossingFonteClima : IFonteClima
{
    public const string NomeFonte = "VisualCrossing";

    private const string Elementos =
        "datetime,tempmax,tempmin,temp,feelslikemax,precip,precipprob,precipcover,cloudcover,humidity,conditions,icon,source";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    private readonly HttpClient _http;
    private readonly ClimaOptions _opcoes;

    public VisualCrossingFonteClima(HttpClient http, ClimaOptions opcoes)
    {
        _http = http;
        _opcoes = opcoes;
    }

    public string Nome => NomeFonte;

    public string Atribuicao => "Dados do clima: Visual Crossing";

    public string AtribuicaoLink => "https://www.visualcrossing.com/";

    public bool Configurada => !string.IsNullOrWhiteSpace(_opcoes.ChaveApi);

    public async Task<IReadOnlyList<LeituraClima>> LerDiasAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
    {
        var caminho = string.Create(CultureInfo.InvariantCulture, $"timeline/{ponto}/{de:yyyy'-'MM'-'dd}/{ate:yyyy'-'MM'-'dd}");
        var json = await ObterAsync(caminho, Elementos, localDesconhecidoViraNulo: false, cancellationToken);
        return LerDias(json!);
    }

    public async Task<LocalEncontrado?> ProcurarLocalAsync(string texto, CancellationToken cancellationToken = default)
    {
        var json = await ObterAsync($"timeline/{Uri.EscapeDataString(texto.Trim())}/today", "datetime", localDesconhecidoViraNulo: true, cancellationToken);
        return json is null ? null : LerLocal(json);
    }

    /// <summary>Os dias da resposta da Timeline API (público para os testes). Dia sem data ou sem máxima/mínima fica de fora.</summary>
    /// <exception cref="FonteClimaException">JSON que não é a resposta esperada.</exception>
    public static IReadOnlyList<LeituraClima> LerDias(string json)
    {
        var resposta = Desserializar(json);
        var leituras = new List<LeituraClima>();
        foreach (var dia in resposta.Days ?? [])
        {
            if (!DateOnly.TryParseExact(dia.Datetime, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data)
                || dia.Tempmax is not { } maxima
                || dia.Tempmin is not { } minima)
            {
                continue;
            }

            leituras.Add(new LeituraClima(
                data,
                maxima,
                minima,
                dia.Temp ?? (maxima + minima) / 2m,
                dia.Feelslikemax,
                dia.Precip,
                dia.Precipcover,
                dia.Cloudcover,
                dia.Humidity,
                dia.Precipprob,
                dia.Icon,
                dia.Conditions,
                Origem(dia.Source)));
        }

        return leituras;
    }

    /// <summary>O lugar resolvido pela Timeline API (público para os testes); nulo sem coordenadas válidas.</summary>
    /// <exception cref="FonteClimaException">JSON que não é a resposta esperada.</exception>
    public static LocalEncontrado? LerLocal(string json)
    {
        var resposta = Desserializar(json);
        if (resposta.Latitude is not { } latitude || resposta.Longitude is not { } longitude
            || latitude is < -90m or > 90m || longitude is < -180m or > 180m)
        {
            return null;
        }

        var nome = string.IsNullOrWhiteSpace(resposta.ResolvedAddress) ? resposta.Address : resposta.ResolvedAddress;
        if (string.IsNullOrWhiteSpace(nome))
        {
            nome = string.Create(CultureInfo.InvariantCulture, $"{latitude:0.####}, {longitude:0.####}");
        }

        nome = nome.Trim();
        return new LocalEncontrado(
            nome.Length <= 120 ? nome : nome[..120],
            Math.Round(latitude, 4, MidpointRounding.AwayFromZero),
            Math.Round(longitude, 4, MidpointRounding.AwayFromZero));
    }

    /// <summary><c>obs</c> = estação; <c>comb</c> = observação completada; <c>fcst</c>/<c>stats</c> = previsão.</summary>
    private static OrigemClima Origem(string? source) => source?.Trim().ToLowerInvariant() switch
    {
        "obs" => OrigemClima.Observado,
        "fcst" or "stats" => OrigemClima.Previsto,
        _ => OrigemClima.Combinado,
    };

    private static RespostaTimeline Desserializar(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<RespostaTimeline>(json, Json)
                ?? throw new FonteClimaException("O serviço de clima mandou uma resposta vazia.");
        }
        catch (JsonException ex)
        {
            throw new FonteClimaException("O serviço de clima mandou uma resposta que o sistema não entendeu.", ex);
        }
    }

    /// <summary>GET no serviço; nulo só quando <paramref name="localDesconhecidoViraNulo"/> e o serviço não achou o lugar.</summary>
    private async Task<string?> ObterAsync(string caminho, string elementos, bool localDesconhecidoViraNulo, CancellationToken cancellationToken)
    {
        if (!Configurada)
        {
            throw new FonteClimaException("Falta a chave do serviço de clima (Clima__ChaveApi).");
        }

        var url = $"{_opcoes.Endereco.TrimEnd('/')}/{caminho}?unitGroup=metric&include=days&lang=pt&contentType=json"
            + $"&elements={elementos}&key={Uri.EscapeDataString(_opcoes.ChaveApi.Trim())}";

        HttpResponseMessage resposta;
        try
        {
            resposta = await _http.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new FonteClimaException("O serviço de clima não respondeu. A próxima tentativa é automática.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FonteClimaException("O serviço de clima demorou demais para responder. A próxima tentativa é automática.", ex);
        }

        using (resposta)
        {
            var corpo = await resposta.Content.ReadAsStringAsync(cancellationToken);
            if (resposta.IsSuccessStatusCode)
            {
                return corpo;
            }

            var status = (int)resposta.StatusCode;
            if (resposta.StatusCode == HttpStatusCode.BadRequest && localDesconhecidoViraNulo
                && corpo.Contains("location", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new FonteClimaException(resposta.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    "A chave do serviço de clima foi recusada. Confira Clima__ChaveApi no servidor.",
                HttpStatusCode.TooManyRequests =>
                    "O limite diário do serviço de clima acabou. A coleta volta sozinha amanhã.",
                _ => $"O serviço de clima recusou a consulta (código {status}){Trecho(SemAChave(corpo))}",
            });
        }
    }

    /// <summary>
    /// A resposta de erro de um serviço de fora pode repetir a URL pedida: a chave sai do texto (como foi enviada e como fica
    /// escapada na URL) antes de a mensagem ir para o painel e para o log.
    /// </summary>
    private string SemAChave(string corpo)
    {
        var chave = _opcoes.ChaveApi.Trim();
        if (chave.Length == 0)
        {
            return corpo;
        }

        return corpo
            .Replace(chave, "***", StringComparison.Ordinal)
            .Replace(Uri.EscapeDataString(chave), "***", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Começo da mensagem do serviço (já sem a chave), para o painel mostrar.</summary>
    private static string Trecho(string corpo)
    {
        var texto = corpo.Trim();
        if (texto.Length == 0)
        {
            return ".";
        }

        return ": " + (texto.Length <= 160 ? texto : texto[..160] + "…");
    }

    private sealed class RespostaTimeline
    {
        public decimal? Latitude { get; set; }

        public decimal? Longitude { get; set; }

        public string? ResolvedAddress { get; set; }

        public string? Address { get; set; }

        public List<DiaTimeline>? Days { get; set; }
    }

    private sealed class DiaTimeline
    {
        public string? Datetime { get; set; }

        public decimal? Tempmax { get; set; }

        public decimal? Tempmin { get; set; }

        public decimal? Temp { get; set; }

        public decimal? Feelslikemax { get; set; }

        public decimal? Precip { get; set; }

        public decimal? Precipprob { get; set; }

        public decimal? Precipcover { get; set; }

        public decimal? Cloudcover { get; set; }

        public decimal? Humidity { get; set; }

        public string? Conditions { get; set; }

        public string? Icon { get; set; }

        public string? Source { get; set; }
    }
}
