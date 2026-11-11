using System.Net;
using System.Text;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Pix;
using GestaoSorveteria.Infrastructure.Pix;

namespace GestaoSorveteria.Tests.Infrastructure;

/// <summary>RN-PX-01: cliente da API Pix do Inter (padrão do Banco Central), sem rede.</summary>
public class InterProvedorPixTests
{
    private const string Txid = "0f8b6c2a1d3e4f509a7b2c4d6e8f0a1b";
    private const string Token = """{"access_token":"tk-1","token_type":"Bearer","expires_in":3600,"scope":"cob.write cob.read"}""";

    private static readonly string CobAtiva = $$"""
        {"calendario":{"criacao":"2026-11-11T15:00:00.00Z","expiracao":600},"txid":"{{Txid}}","revisao":0,"status":"ATIVA",
         "valor":{"original":"23.50"},"chave":"12345678000199","pixCopiaECola":"00020101021226930014br.gov.bcb.pix2571spi"}
        """;

    private static readonly string CobPaga = $$"""
        {"txid":"{{Txid}}","status":"CONCLUIDA","valor":{"original":"23.50"},"pixCopiaECola":"000201abc",
         "pix":[{"endToEndId":"E00416968202611111501abcdef12345","txid":"{{Txid}}","valor":"23.50","horario":"2026-11-11T12:01:30.00-03:00"}]}
        """;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class BancoFake : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Corpo)> _respostas = new();

        public List<(HttpMethod Metodo, string Caminho, string? Corpo, string? Autorizacao, string? Conta)> Pedidos { get; } = new();

        public BancoFake Responder(HttpStatusCode status, string corpo)
        {
            _respostas.Enqueue((status, corpo));
            return this;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Pedidos.Add((request.Method, request.RequestUri!.AbsolutePath, corpo, request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("x-conta-corrente", out var conta) ? conta.Single() : null));
            var (status, texto) = _respostas.Count > 0 ? _respostas.Dequeue() : (HttpStatusCode.InternalServerError, "{}");
            return new HttpResponseMessage(status) { Content = new StringContent(texto, Encoding.UTF8, "application/json") };
        }
    }

    private static PixOptions Opcoes(string conta = "") => new()
    {
        Provedor = PixOptions.ProvedorInter,
        ClientId = "id",
        ClientSecret = "segredo-que-nao-pode-vazar",
        CertificadoPem = "x",
        ChavePrivadaPem = "x",
        Chave = "12345678000199",
        ContaCorrente = conta,
    };

    private static InterProvedorPix Provedor(BancoFake banco, PixOptions? opcoes = null) =>
        new(new HttpClient(banco) { BaseAddress = new Uri(InterProvedorPix.EnderecoProducao) }, opcoes ?? Opcoes());

    [Fact]
    public async Task CriarAsync_PedeTokenECriaACobrancaNoPadraoDoBancoCentral()
    {
        var banco = new BancoFake().Responder(HttpStatusCode.OK, Token).Responder(HttpStatusCode.Created, CobAtiva);

        var cob = await Provedor(banco, Opcoes(conta: "123456")).CriarAsync(new NovaCobrancaPix(Txid, 23.50m, 600, "Sorveteria — balcão"), Ct);

        Assert.Equal(StatusCobrancaPix.Ativa, cob.Situacao.Status);
        Assert.Equal(23.50m, cob.Valor);
        Assert.StartsWith("000201", cob.PixCopiaECola);

        var token = banco.Pedidos[0];
        Assert.Equal("/oauth/v2/token", token.Caminho);
        Assert.Contains("grant_type=client_credentials", token.Corpo);
        Assert.Contains("scope=cob.write+cob.read", token.Corpo);

        var criar = banco.Pedidos[1];
        Assert.Equal(HttpMethod.Put, criar.Metodo);
        Assert.Equal("/pix/v2/cob/" + Txid, criar.Caminho);
        Assert.Equal("Bearer tk-1", criar.Autorizacao);
        Assert.Equal("123456", criar.Conta);
        Assert.Contains("\"expiracao\":600", criar.Corpo);
        Assert.Contains("\"original\":\"23.50\"", criar.Corpo);
        Assert.Contains("\"chave\":\"12345678000199\"", criar.Corpo);
        Assert.Contains("\"solicitacaoPagador\":", criar.Corpo);
    }

    [Fact]
    public async Task ConsultarAsync_Concluida_LeEndToEndValorEHorarioEmUtc()
    {
        var banco = new BancoFake().Responder(HttpStatusCode.OK, Token).Responder(HttpStatusCode.OK, CobPaga);

        var cob = await Provedor(banco).ConsultarAsync(Txid, Ct);

        Assert.Equal(StatusCobrancaPix.Concluida, cob.Situacao.Status);
        Assert.Equal("E00416968202611111501abcdef12345", cob.Situacao.EndToEndId);
        Assert.Equal(23.50m, cob.Situacao.ValorPago);
        Assert.Equal(new DateTime(2026, 11, 11, 15, 1, 30, DateTimeKind.Utc), cob.Situacao.PagaEm);
        Assert.Equal(DateTimeKind.Utc, cob.Situacao.PagaEm!.Value.Kind);
    }

    [Fact]
    public async Task Token_ReaproveitadoAteVencer()
    {
        var banco = new BancoFake().Responder(HttpStatusCode.OK, Token).Responder(HttpStatusCode.OK, CobAtiva).Responder(HttpStatusCode.OK, CobAtiva);
        var provedor = Provedor(banco);

        await provedor.ConsultarAsync(Txid, Ct);
        await provedor.ConsultarAsync(Txid, Ct);

        Assert.Single(banco.Pedidos, p => p.Caminho == "/oauth/v2/token");
    }

    [Fact]
    public async Task CriarAsync_TxidJaUsado_DevolveAExistente()
    {
        var banco = new BancoFake()
            .Responder(HttpStatusCode.OK, Token)
            .Responder(HttpStatusCode.BadRequest, """{"title":"Cobrança inválida","detail":"txid já utilizado"}""")
            .Responder(HttpStatusCode.OK, CobAtiva);

        var cob = await Provedor(banco).CriarAsync(new NovaCobrancaPix(Txid, 23.50m, 600, "x"), Ct);

        Assert.Equal(Txid, cob.Txid);
        Assert.Equal(HttpMethod.Get, banco.Pedidos[2].Metodo);
    }

    [Fact]
    public async Task CancelarAsync_MandaRemovidaPeloRecebedor()
    {
        var removida = CobAtiva.Replace("\"ATIVA\"", "\"REMOVIDA_PELO_USUARIO_RECEBEDOR\"");
        var banco = new BancoFake().Responder(HttpStatusCode.OK, Token).Responder(HttpStatusCode.OK, removida);

        var cob = await Provedor(banco).CancelarAsync(Txid, Ct);

        Assert.Equal(StatusCobrancaPix.Removida, cob.Situacao.Status);
        Assert.Equal(HttpMethod.Patch, banco.Pedidos[1].Metodo);
        Assert.Contains("REMOVIDA_PELO_USUARIO_RECEBEDOR", banco.Pedidos[1].Corpo);
    }

    [Fact]
    public async Task Token_CredenciaisRecusadas_MensagemSemSegredo()
    {
        var banco = new BancoFake().Responder(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""");

        var ex = await Assert.ThrowsAsync<ProvedorPixException>(() => Provedor(banco).ConsultarAsync(Txid, Ct));

        Assert.Contains("credenciais", ex.Message);
        Assert.DoesNotContain("segredo-que-nao-pode-vazar", ex.Message);
    }

    [Fact]
    public async Task ConsultarAsync_BancoForaDoAr_MensagemAmigavel()
    {
        var banco = new BancoFake().Responder(HttpStatusCode.OK, Token).Responder(HttpStatusCode.ServiceUnavailable, "");

        var ex = await Assert.ThrowsAsync<ProvedorPixException>(() => Provedor(banco).ConsultarAsync(Txid, Ct));

        Assert.Contains("fora do ar", ex.Message);
    }

    [Fact]
    public async Task ConsultarAsync_TxidInvalido_NaoChamaOBanco()
    {
        var banco = new BancoFake();

        await Assert.ThrowsAsync<ProvedorPixException>(() => Provedor(banco).ConsultarAsync("../outra-rota", Ct));

        Assert.Empty(banco.Pedidos);
    }
}

/// <summary>RN-PX-01: configuração do Pix integrado; erro desliga em vez de derrubar a API.</summary>
public class PixOptionsTests
{
    [Fact]
    public void Normalizar_InterCompleto_Liga()
    {
        var opcoes = new PixOptions { Provedor = "inter", ClientId = "a", ClientSecret = "b", CertificadoPem = "c", ChavePrivadaPem = "d", Chave = "e" };

        opcoes.Normalizar(producao: true);

        Assert.Equal(PixOptions.ProvedorInter, opcoes.Provedor);
        Assert.Empty(opcoes.Avisos);
    }

    [Fact]
    public void Normalizar_InterSemCertificado_DesligaEAvisa()
    {
        var opcoes = new PixOptions { Provedor = "Inter", ClientId = "a", ClientSecret = "b", Chave = "e" };

        opcoes.Normalizar(producao: true);

        Assert.Equal(string.Empty, opcoes.Provedor);
        Assert.Contains(opcoes.Avisos, a => a.Contains("CertificadoPem"));
    }

    [Fact]
    public void Normalizar_SimuladoEmProducao_Desliga()
    {
        var opcoes = new PixOptions { Provedor = "Simulado" };

        opcoes.Normalizar(producao: true);

        Assert.Equal(string.Empty, opcoes.Provedor);
    }

    [Fact]
    public void Normalizar_SimuladoForaDeProducao_Liga()
    {
        var opcoes = new PixOptions { Provedor = "simulado" };

        opcoes.Normalizar(producao: false);

        Assert.Equal(PixOptions.ProvedorSimulado, opcoes.Provedor);
    }

    [Fact]
    public void Normalizar_ExpiracaoForaDaFaixa_VoltaAoPadrao()
    {
        var opcoes = new PixOptions { ExpiracaoSegundos = 5 };

        opcoes.Normalizar(producao: false);

        Assert.Equal(CobrancaPix.ExpiracaoPadraoSegundos, opcoes.ExpiracaoSegundos);
    }

    [Fact]
    public async Task Simulado_PagaDepoisDoTempoConfigurado()
    {
        var relogio = new RelogioFake(new DateTimeOffset(2026, 11, 11, 15, 0, 0, TimeSpan.Zero));
        var provedor = new SimuladoProvedorPix(new PixOptions { SimuladoPagaEmSegundos = 8 }, relogio);
        const string txid = "0f8b6c2a1d3e4f509a7b2c4d6e8f0a1b";

        var criada = await provedor.CriarAsync(new NovaCobrancaPix(txid, 10m, 600, "x"), TestContext.Current.CancellationToken);
        relogio.Avancar(TimeSpan.FromSeconds(9));
        var depois = await provedor.ConsultarAsync(txid, TestContext.Current.CancellationToken);

        Assert.Equal(StatusCobrancaPix.Ativa, criada.Situacao.Status);
        Assert.Equal(StatusCobrancaPix.Concluida, depois.Situacao.Status);
        Assert.Equal(10m, depois.Situacao.ValorPago);
    }

    private sealed class RelogioFake(DateTimeOffset agora) : TimeProvider
    {
        private DateTimeOffset _agora = agora;

        public override DateTimeOffset GetUtcNow() => _agora;

        public void Avancar(TimeSpan tempo) => _agora += tempo;
    }
}
