using GestaoSorveteria.Application.Pix;
using GestaoSorveteria.Contracts.Pix;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// RN-PX-01…06: Pix integrado do balcão. O app cria a cobrança, mostra o QR Code e consulta até o banco confirmar.
/// Atendente e dona podem usar. Banco fora do ar → 503 com a mensagem pronta (o app oferece o Pix à mão).
/// </summary>
[ApiController]
[Route("api/pix")]
[Produces("application/json")]
[Authorize]
public sealed class PixController(PixService pix) : ControllerBase
{
    /// <summary>RN-PX-01: o Pix integrado está ligado neste servidor?</summary>
    [HttpGet("situacao")]
    [ProducesResponseType(typeof(PixSituacaoDto), StatusCodes.Status200OK)]
    public ActionResult<PixSituacaoDto> Situacao() => Ok(pix.Situacao());

    /// <summary>RN-PX-02: cria (ou devolve, se o Id já existe) a cobrança com valor.</summary>
    [HttpPost("cobrancas")]
    [ProducesResponseType(typeof(CobrancaPixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CobrancaPixDto>> Criar(CriarCobrancaPixRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await pix.CriarAsync(request, usuarioId, cancellationToken));
    }

    /// <summary>RN-PX-03: como a cobrança está agora (pergunta ao banco enquanto ativa).</summary>
    [HttpGet("cobrancas/{id:guid}")]
    [ProducesResponseType(typeof(CobrancaPixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CobrancaPixDto>> Consultar(Guid id, CancellationToken cancellationToken) =>
        Ok(await pix.ConsultarAsync(id, cancellationToken));

    /// <summary>RN-PX-06: desiste da cobrança. Se já estiver paga, volta paga (o app grava a venda).</summary>
    [HttpPost("cobrancas/{id:guid}/cancelar")]
    [ProducesResponseType(typeof(CobrancaPixDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<CobrancaPixDto>> Cancelar(Guid id, CancellationToken cancellationToken) =>
        Ok(await pix.CancelarAsync(id, cancellationToken));
}
