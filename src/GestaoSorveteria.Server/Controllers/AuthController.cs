using GestaoSorveteria.Application.Auth;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GestaoSorveteria.Server.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth)
    {
        _auth = auth;
    }

    /// <summary>Login do atendente (PIN) ou da dona (senha). Devolve o JWT usado nas demais chamadas.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var resposta = await _auth.LoginAsync(request, cancellationToken);
        if (resposta is null)
        {
            return Problem(
                title: "Login ou senha inválidos.",
                detail: "Confira o login e a senha/PIN. Usuários desativados não conseguem entrar.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(resposta);
    }

    /// <summary>Quem sou eu — valida o token e devolve o usuário logado (o app usa ao abrir).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UsuarioDto> Me()
    {
        var id = User.FindFirst(JwtTokenService.ClaimId)?.Value;
        if (!Guid.TryParse(id, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(new UsuarioDto(
            usuarioId,
            User.FindFirst(JwtTokenService.ClaimNome)?.Value ?? string.Empty,
            User.FindFirst(JwtTokenService.ClaimLogin)?.Value ?? string.Empty,
            User.FindFirst(JwtTokenService.ClaimPerfil)?.Value ?? string.Empty,
            Ativo: true));
    }
}
