namespace SorveteriaMaui.Services.Api;

/// <summary>A API recusou a chamada. A mensagem está pronta para aparecer na tela.</summary>
public class ApiException(string mensagem, int? status = null) : Exception(mensagem)
{
    public int? Status { get; } = status;
}

/// <summary>Sem internet, servidor dormindo (docs/07) ou erro 5xx: tentar de novo mais tarde, sem descartar nada.</summary>
public sealed class ApiIndisponivelException(string mensagem, int? status = null) : ApiException(mensagem, status);

/// <summary>Token ausente, vencido (RN-US-06) ou recusado (401): pedir o PIN de novo quando houver internet.</summary>
public sealed class SessaoExpiradaException()
    : ApiException("Sua sessão venceu. Entre de novo com o PIN para continuar.", 401);
