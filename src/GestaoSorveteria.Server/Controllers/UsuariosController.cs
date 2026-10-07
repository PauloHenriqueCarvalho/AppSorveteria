using GestaoSorveteria.Application.Usuarios;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Contracts.Usuarios;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Usuários (atendentes e administradores) geridos pela dona no painel. Só Admin (RN-US-08).</summary>
[ApiController]
[Route("api/usuarios")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class UsuariosController : ControllerBase
{
    private readonly UsuarioService _usuarios;

    public UsuariosController(UsuarioService usuarios)
    {
        _usuarios = usuarios;
    }

    /// <summary>Todos os usuários, inclusive desativados, em ordem de nome.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<UsuarioDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(CancellationToken cancellationToken) =>
        Ok(await _usuarios.ListarAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _usuarios.ObterAsync(id, cancellationToken));

    /// <summary>
    /// Cria um atendente (PIN de 4 a 6 dígitos) ou outro Admin (senha de 8+ caracteres) — RN-US-02/03.
    /// Login repetido → 400.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioDto>> Criar(CriarUsuarioRequest request, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = usuario.Id }, usuario);
    }

    /// <summary>Redefine o PIN/senha sem precisar da antiga (RN-US-08). Vale a política do perfil do usuário (RN-US-03).</summary>
    [HttpPost("{id:guid}/redefinir-senha")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> RedefinirSenha(Guid id, RedefinirSenhaRequest request, CancellationToken cancellationToken) =>
        Ok(await _usuarios.RedefinirSenhaAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/ativar")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Ativar(Guid id, CancellationToken cancellationToken) =>
        Ok(await _usuarios.AtivarAsync(id, cancellationToken));

    /// <summary>RN-US-07: usuário nunca é apagado, só desativado. RN-US-10: não dá para desativar a si mesmo.</summary>
    [HttpPost("{id:guid}/desativar")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Desativar(Guid id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtTokenService.ClaimId)?.Value, out var usuarioLogadoId))
        {
            return Unauthorized();
        }

        return Ok(await _usuarios.DesativarAsync(id, usuarioLogadoId, cancellationToken));
    }
}
