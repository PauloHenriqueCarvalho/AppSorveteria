using GestaoSorveteria.Application.Contagens;
using GestaoSorveteria.Contracts.Contagens;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Contagem semanal pelo painel (RN-ES-05/06). O tablet manda as dele por <c>POST /api/sync/contagens</c>.
/// </summary>
[ApiController]
[Route("api/estoque/contagens")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class ContagensController : ControllerBase
{
    private readonly ContagemService _contagens;

    public ContagensController(ContagemService contagens)
    {
        _contagens = contagens;
    }

    /// <summary>Contagens dos últimos 60 dias e as que ainda não foram concluídas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContagemResumoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ContagemResumoDto>>> Listar(CancellationToken cancellationToken) =>
        Ok(await _contagens.ListarAsync(cancellationToken));

    /// <summary>Modelo de contagem nova (nada é gravado): todos os itens contáveis, vazios, com a contagem anterior como referência.</summary>
    [HttpGet("nova")]
    [ProducesResponseType(typeof(ContagemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContagemDto>> Nova(CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _contagens.ModeloAsync(usuarioId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContagemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContagemDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _contagens.ObterAsync(id, cancellationToken));

    /// <summary>Cria ou continua uma contagem aberta; com <c>concluir</c>, fecha (depois não muda mais).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ContagemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ContagemDto>> Salvar(SalvarContagemRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _contagens.SalvarAsync(request, usuarioId, cancellationToken));
    }
}
