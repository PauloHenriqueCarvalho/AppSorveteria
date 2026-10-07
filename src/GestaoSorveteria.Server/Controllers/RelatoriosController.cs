using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Contracts.Relatorios;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Relatórios do painel da dona (só leitura).</summary>
[ApiController]
[Route("api/relatorios")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class RelatoriosController : ControllerBase
{
    private readonly RelatorioService _relatorios;

    public RelatoriosController(RelatorioService relatorios)
    {
        _relatorios = relatorios;
    }

    /// <summary>Resumo de um dia (horário de Brasília, formato 2026-09-30; sem data = hoje): vendas, ticket médio, formas de pagamento e caixa aberto.</summary>
    [HttpGet("dia")]
    [ProducesResponseType(typeof(ResumoDiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResumoDiaDto>> Dia([FromQuery] DateOnly? data, CancellationToken cancellationToken) =>
        Ok(await _relatorios.ResumoDoDiaAsync(data, cancellationToken));
}
