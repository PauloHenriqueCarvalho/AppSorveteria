using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Contracts.Caixas;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Caixas para o painel da dona: leitura e fechamento forçado (RN-CX-09). O app envia os caixas por POST /api/sync/caixas.</summary>
[ApiController]
[Route("api/caixas")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class CaixasController : ControllerBase
{
    private readonly CaixaConsultaService _caixas;
    private readonly CaixaService _acoes;

    public CaixasController(CaixaConsultaService caixas, CaixaService acoes)
    {
        _caixas = caixas;
        _acoes = acoes;
    }

    /// <summary>
    /// RN-CX-09: fecha pelo painel um caixa que o atendente esqueceu aberto. O valor contado fica 0 e a diferença
    /// é gravada; o motivo é obrigatório. Se o fechamento do celular chegar depois, a sincronização o rejeita.
    /// </summary>
    [HttpPost("{id:guid}/fechar-forcado")]
    [ProducesResponseType(typeof(CaixaDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CaixaDetalheDto>> FecharForcado(Guid id, FecharCaixaForcadoRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtTokenService.ClaimId)?.Value, out var adminId))
        {
            return Unauthorized();
        }

        return Ok(await _acoes.FecharForcadoAsync(id, request, adminId, cancellationToken));
    }

    /// <summary>
    /// Histórico de caixas abertos entre <c>de</c> e <c>ate</c> (dias no horário de Brasília, formato 2026-09-30, inclusive).
    /// Sem datas: últimos 30 dias. Período máximo: 93 dias.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CaixaResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CaixaResumoDto>>> Listar(
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        CancellationToken cancellationToken) =>
        Ok(await _caixas.ListarAsync(de, ate, cancellationToken));

    /// <summary>O caixa aberto agora, com o último estado recebido do app. 204 quando não há caixa aberto.</summary>
    [HttpGet("atual")]
    [ProducesResponseType(typeof(CaixaResumoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CaixaResumoDto>> Atual(CancellationToken cancellationToken)
    {
        var caixa = await _caixas.ObterAtualAsync(cancellationToken);
        return caixa is null ? NoContent() : Ok(caixa);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CaixaDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CaixaDetalheDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _caixas.ObterAsync(id, cancellationToken));
}
