using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Server.Security;

/// <summary>Nomes das políticas de autorização e de rate limit usados nos controllers.</summary>
public static class Politicas
{
    /// <summary>RN-US-01: só a dona.</summary>
    public const string Admin = "Admin";

    public static readonly string PerfilAdmin = nameof(PerfilUsuario.Admin);
    public static readonly string PerfilAtendente = nameof(PerfilUsuario.Atendente);
}

public static class RateLimitPolicies
{
    /// <summary>RN-US-05: 10 tentativas por minuto por IP.</summary>
    public const string Login = "login";
}
