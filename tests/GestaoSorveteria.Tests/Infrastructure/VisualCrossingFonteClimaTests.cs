using System.Net;
using System.Text;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Infrastructure.Clima;

namespace GestaoSorveteria.Tests.Infrastructure;

/// <summary>Visual Crossing (RN-CL-07): leitura da Timeline API, URL e erros, sem rede.</summary>
public class VisualCrossingFonteClimaTests
{
    private const string Chave = "abc+123";

    private const string RespostaDias = """
        {"queryCost":5,"latitude":-21.18,"longitude":-47.81,"resolvedAddress":"-21.18,-47.81","address":"-21.18,-47.81",
         "timezone":"America/Sao_Paulo","tzoffset":-3.0,
         "days":[
          {"datetime":"2026-10-14","tempmax":31.4,"tempmin":18.2,"temp":24.6,"feelslikemax":32.1,"humidity":55.3,"precip":0.0,
           "precipprob":0.0,"precipcover":0.0,"cloudcover":20.1,"conditions":"Parcialmente nublado","icon":"partly-cloudy-day","source":"obs"},
          {"datetime":"2026-10-15","tempmax":30.0,"tempmin":19.0,"temp":24.0,"feelslikemax":null,"humidity":60.0,"precip":3.2,
           "precipprob":100.0,"precipcover":12.5,"cloudcover":70.0,"conditions":"Chuva, Nublado","icon":"rain","source":"comb"},
          {"datetime":"2026-10-16","tempmax":28.5,"tempmin":17.0,"temp":null,"precip":null,"precipprob":45,"source":"fcst"},
          {"datetime":"2026-10-17","tempmin":17.0,"source":"fcst"},
          {"datetime":"lixo","tempmax":20,"tempmin":10}
         ],
         "stations":{"A711":{"distance":5000.0}}}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class HandlerFake(HttpStatusCode status, string corpo, Exception? erro = null) : HttpMessageHandler
    {
        public Uri? Pedido { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Pedido = request.RequestUri;
            if (erro is not null)
            {
                throw erro;
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(corpo, Encoding.UTF8, "application/json") });
        }
    }

    private static (VisualCrossingFonteClima Fonte, HandlerFake Handler) Fonte(HttpStatusCode status, string corpo, string chave = Chave, Exception? erro = null)
    {
        var handler = new HandlerFake(status, corpo, erro);
        return (new VisualCrossingFonteClima(new HttpClient(handler), new ClimaOptions { ChaveApi = chave }), handler);
    }

    [Fact]
    public void LerDias_ConverteCadaDiaEAOrigem_EIgnoraDiaIncompleto()
    {
        var leituras = VisualCrossingFonteClima.LerDias(RespostaDias);

        Assert.Equal(3, leituras.Count);
        var observado = leituras[0];
        Assert.Equal(new DateOnly(2026, 10, 14), observado.Data);
        Assert.Equal((31.4m, 18.2m, 24.6m, 32.1m), (observado.TempMax, observado.TempMin, observado.TempMedia, observado.SensacaoMax!.Value));
        Assert.Equal((0m, 20.1m, 55.3m), (observado.ChuvaMm!.Value, observado.NuvensPct!.Value, observado.UmidadePct!.Value));
        Assert.Equal(("partly-cloudy-day", "Parcialmente nublado", OrigemClima.Observado), (observado.Icone, observado.Condicao, observado.Origem));
        Assert.Equal((3.2m, 12.5m, OrigemClima.Combinado), (leituras[1].ChuvaMm!.Value, leituras[1].ChuvaHorasPct!.Value, leituras[1].Origem));
        Assert.Null(leituras[1].SensacaoMax);
        Assert.Equal((22.75m, 45m, OrigemClima.Previsto), (leituras[2].TempMedia, leituras[2].ChanceChuvaPct!.Value, leituras[2].Origem));
        Assert.Null(leituras[2].ChuvaMm);
    }

    [Fact]
    public void LerDias_JsonQueNaoEhDaApi_Lanca() =>
        Assert.Throws<FonteClimaException>(() => VisualCrossingFonteClima.LerDias("<html>erro</html>"));

    [Fact]
    public void LerLocal_NomeECoordenadasComQuatroCasas()
    {
        var local = VisualCrossingFonteClima.LerLocal(
            """{"latitude":-21.177512,"longitude":-47.81035,"resolvedAddress":"Ribeirão Preto, SP, Brasil","address":"ribeirao preto sp","days":[]}""");

        Assert.Equal(new LocalEncontrado("Ribeirão Preto, SP, Brasil", -21.1775m, -47.8104m), local);
    }

    [Fact]
    public void LerLocal_SemCoordenadas_Nulo() =>
        Assert.Null(VisualCrossingFonteClima.LerLocal("""{"resolvedAddress":"Lugar","days":[]}"""));

    [Fact]
    public async Task LerDiasAsync_UrlComPontoDatasUnidadesMetricasEAChaveEscapada()
    {
        var (fonte, handler) = Fonte(HttpStatusCode.OK, RespostaDias);

        var leituras = await fonte.LerDiasAsync(PontoClima.De(-21.1775m, -47.8103m), new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 29), Ct);

        Assert.Equal(3, leituras.Count);
        var url = handler.Pedido!.AbsoluteUri;
        Assert.StartsWith("https://weather.visualcrossing.com/VisualCrossingWebServices/rest/services/timeline/-21.18,-47.81/2026-10-09/2026-10-29?", url);
        Assert.Contains("unitGroup=metric", url);
        Assert.Contains("include=days", url);
        Assert.Contains("lang=pt", url);
        Assert.Contains("key=abc%2B123", url);
    }

    [Fact]
    public async Task ProcurarLocalAsync_TextoEscapadoNoCaminho()
    {
        var (fonte, handler) = Fonte(HttpStatusCode.OK, """{"latitude":-21.18,"longitude":-47.81,"resolvedAddress":"Ribeirão Preto, SP, Brasil"}""");

        var local = await fonte.ProcurarLocalAsync("Ribeirão Preto, SP", Ct);

        Assert.Equal("Ribeirão Preto, SP, Brasil", local!.Nome);
        Assert.Contains("/timeline/Ribeir%C3%A3o%20Preto%2C%20SP/today?", handler.Pedido!.AbsoluteUri);
    }

    [Fact]
    public async Task ProcurarLocalAsync_LugarQueNaoExiste_Nulo()
    {
        var (fonte, _) = Fonte(HttpStatusCode.BadRequest, "Bad API Request:Invalid location parameter value.");

        Assert.Null(await fonte.ProcurarLocalAsync("Xyzópolis", Ct));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "chave do serviço de clima foi recusada")]
    [InlineData(HttpStatusCode.TooManyRequests, "limite diário")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    [InlineData(HttpStatusCode.BadRequest, "HTTP 400")]
    public async Task LerDiasAsync_Erro_MensagemProntaSemAChave(HttpStatusCode status, string esperado)
    {
        var (fonte, _) = Fonte(status, "No account found with API key");

        var erro = await Assert.ThrowsAsync<FonteClimaException>(() =>
            fonte.LerDiasAsync(PontoClima.De(-21m, -47m), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), Ct));

        Assert.Contains(esperado, erro.Message);
        Assert.DoesNotContain(Chave, erro.Message);
    }

    [Fact]
    public async Task LerDiasAsync_SemRede_MensagemDeNovaTentativa()
    {
        var (fonte, _) = Fonte(HttpStatusCode.OK, "", erro: new HttpRequestException("Name or service not known"));

        var erro = await Assert.ThrowsAsync<FonteClimaException>(() =>
            fonte.LerDiasAsync(PontoClima.De(-21m, -47m), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), Ct));

        Assert.Contains("não respondeu", erro.Message);
    }

    [Fact]
    public async Task SemChave_NaoConfiguradaENaoChamaOServico()
    {
        var (fonte, handler) = Fonte(HttpStatusCode.OK, RespostaDias, chave: " ");

        Assert.False(fonte.Configurada);
        await Assert.ThrowsAsync<FonteClimaException>(() =>
            fonte.LerDiasAsync(PontoClima.De(-21m, -47m), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), Ct));
        Assert.Null(handler.Pedido);
    }

    [Fact]
    public void Opcoes_IntervaloForaDaFaixaOuEnderecoSemHttps_Lanca()
    {
        Assert.Throws<InvalidOperationException>(() => new ClimaOptions { IntervaloHoras = 0 }.Validar());
        Assert.Throws<InvalidOperationException>(() => new ClimaOptions { Endereco = "http://exemplo.com/" }.Validar());
        new ClimaOptions().Validar();
    }
}
