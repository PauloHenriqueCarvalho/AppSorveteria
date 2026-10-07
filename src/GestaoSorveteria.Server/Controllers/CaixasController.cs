using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Contracts.Caixas;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Caixas para o painel da dona (só leitura). O app envia os caixas por POST /api/sync/caixas.</summary>
[ApiController]
[Route("api/caixas")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class CaixasController : ControllerBase
{
    private readonly CaixaConsultaService _caixas;

    public CaixasController(CaixaConsultaService caixas)
    {
        _caixas = caixas;
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
