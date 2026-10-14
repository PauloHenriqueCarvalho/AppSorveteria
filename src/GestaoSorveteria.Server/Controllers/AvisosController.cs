using GestaoSorveteria.Application.Avisos;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Avisos "Está acabando" (docs/10 §2.2): atendente e dona criam e atualizam; só a dona fecha (RN-RC-07).
/// O tablet sem internet manda os dele por <c>POST /api/sync/avisos</c>.
/// </summary>
[ApiController]
[Route("api/avisos")]
[Produces("application/json")]
[Authorize]
public sealed class AvisosController : ControllerBase
{
    private readonly AvisoService _avisos;
    private readonly IAuthorizationService _autorizacao;
    private readonly IClock _clock;

    public AvisosController(AvisoService avisos, IAuthorizationService autorizacao, IClock clock)
    {
        _avisos = avisos;
        _autorizacao = autorizacao;
        _clock = clock;
    }

    /// <summary>Abertos (urgente primeiro). Com <c>abertos=false</c> vêm também os fechados dos últimos 30 dias.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvisoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AvisoDto>>> Listar([FromQuery] bool abertos = true, CancellationToken cancellationToken = default) =>
        Ok(await _avisos.ListarAsync(abertos, cancellationToken));

    /// <summary>
    /// RN-AV-01/03: item que já tem aviso aberto não ganha outro (volta <c>jaExistia</c>). Urgente devolve <c>enviarAgora</c>
    /// com a mensagem e o link do WhatsApp da dona (RN-AV-06).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AvisoSalvoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvisoSalvoResponse>> Criar(CriarAvisoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _avisos.CriarAsync(request, usuarioId, cancellationToken));
    }

    /// <summary>RN-AV-03 "Ainda falta": nova quantidade; a prioridade só pode subir.</summary>
    [HttpPost("{id:guid}/ainda-falta")]
    [ProducesResponseType(typeof(AvisoSalvoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvisoSalvoResponse>> AindaFalta(Guid id, AindaFaltaRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _avisos.AindaFaltaAsync(id, request, usuarioId, cancellationToken));
    }

    /// <summary>RN-AV-05b: a dona fecha com motivo ("comprei no mercado").</summary>
    [HttpPost("{id:guid}/fechar")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(AvisoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvisoDto>> Fechar(Guid id, FecharAvisoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _avisos.FecharAsync(id, request, usuarioId, cancellationToken));
    }

    /// <summary>RN-AV-08: o link do WhatsApp foi aberto (não garante que a mensagem foi enviada).</summary>
    [HttpPost("{id:guid}/whatsapp-aberto")]
    [ProducesResponseType(typeof(AvisoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvisoDto>> WhatsAppAberto(Guid id, CancellationToken cancellationToken) =>
        Ok(await _avisos.MarcarWhatsAppAbertoAsync(id, cancellationToken));

    /// <summary>
    /// RN-AV-07: texto do resumo do dia e o link para o WhatsApp da dona. Sem <c>data</c> = hoje.
    /// O resumo traz o total vendido: o atendente só pede o de hoje (fechamento do caixa); outros dias, só a dona.
    /// </summary>
    [HttpGet("resumo-do-dia")]
    [ProducesResponseType(typeof(ResumoDoDiaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResumoDoDiaResponse>> ResumoDoDia([FromQuery] DateOnly? data, CancellationToken cancellationToken)
    {
        if (data is { } dia && dia != DiaComercial.DataDe(_clock.UtcNow)
            && !(await _autorizacao.AuthorizeAsync(User, Politicas.Admin)).Succeeded)
        {
            return Forbid();
        }

        return Ok(await _avisos.ResumoDoDiaAsync(data, cancellationToken));
    }
}
