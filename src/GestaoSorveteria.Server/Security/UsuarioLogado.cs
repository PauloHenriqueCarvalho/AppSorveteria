using System.Security.Claims;

namespace GestaoSorveteria.Server.Security;

/// <summary>Id do usuário do token (claim <see cref="JwtTokenService.ClaimId"/>).</summary>
public static class UsuarioLogado
{
    public static bool TentarObterId(ClaimsPrincipal usuario, out Guid id) =>
        Guid.TryParse(usuario.FindFirst(JwtTokenService.ClaimId)?.Value, out id);
}
