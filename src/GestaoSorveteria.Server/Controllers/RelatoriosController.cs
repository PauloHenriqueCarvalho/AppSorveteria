using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Contracts.Clima;
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
    private readonly RelatorioPeriodoService _periodo;
    private readonly ClimaVendasService _climaVendas;

    public RelatoriosController(RelatorioService relatorios, RelatorioPeriodoService periodo, ClimaVendasService climaVendas)
    {
        _relatorios = relatorios;
        _periodo = periodo;
        _climaVendas = climaVendas;
    }

    /// <summary>Resumo de um dia (horário de Brasília, formato 2026-09-30; sem data = hoje): vendas, ticket médio, formas de pagamento e caixa aberto.</summary>
    [HttpGet("dia")]
    [ProducesResponseType(typeof(ResumoDiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ResumoDiaDto>> Dia([FromQuery] DateOnly? data, CancellationToken cancellationToken) =>
        Ok(await _relatorios.ResumoDoDiaAsync(data, cancellationToken));

    /// <summary>
    /// Vendas de um período (dias no horário de Brasília, formato 2026-10-01, inclusive; sem datas = este mês até hoje; até 366
    /// dias, sem passar de hoje): faturamento por dia/semana/mês, comparativo com o período anterior, horários e dias de pico
    /// e rankings de produtos, categorias e sabores. <c>agrupar</c>: dia (até 93 dias), semana ou mes; sem ele, escolhe pelo tamanho.
    /// </summary>
    [HttpGet("periodo")]
    [ProducesResponseType(typeof(RelatorioPeriodoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<RelatorioPeriodoDto>> Periodo(
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] string? agrupar,
        CancellationToken cancellationToken) =>
        Ok(await _periodo.ObterAsync(de, ate, agrupar, cancellationToken));

    /// <summary>
    /// RN-CL-06: Clima × vendas (dias no horário de Brasília, inclusive; sem datas = últimos 30 dias; até 366 dias, sem passar de
    /// hoje): cada dia com clima, feriado, faturamento, vendas, categorias e avisos urgentes; médias por faixa da máxima e
    /// com/sem chuva (dias úteis × fins de semana e feriados); previsão dos próximos 7 dias.
    /// </summary>
    [HttpGet("clima-vendas")]
    [ProducesResponseType(typeof(ClimaVendasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClimaVendasDto>> ClimaVendas(
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        CancellationToken cancellationToken) =>
        Ok(await _climaVendas.ObterAsync(de, ate, cancellationToken));
}
