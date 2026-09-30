using GestaoSorveteria.Application.Comandas;
using GestaoSorveteria.Contracts.Comandas;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Vendas para o painel da dona (leitura e estorno). O app envia as vendas por POST /api/sync/comandas.</summary>
[ApiController]
[Route("api/comandas")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class ComandasController : ControllerBase
{
    private readonly ComandaConsultaService _comandas;
    private readonly EstornoService _estorno;

    public ComandasController(ComandaConsultaService comandas, EstornoService estorno)
    {
        _comandas = comandas;
        _estorno = estorno;
    }

    /// <summary>
    /// Vendas do período (dias no horário de Brasília, formato 2026-09-30, inclusive; sem datas = hoje; máx. 93 dias),
    /// mais recente primeiro. <c>status</c>: Fechada, Cancelada ou Aberta. <c>tamanho</c>: 1 a 100 (padrão 50).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaDto<ComandaResumoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaDto<ComandaResumoDto>>> Listar(
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] string? status,
        [FromQuery] int? pagina,
        [FromQuery] int? tamanho,
        CancellationToken cancellationToken) =>
        Ok(await _comandas.ListarAsync(de, ate, status, pagina, tamanho, cancellationToken));

    /// <summary>Venda com itens e pagamentos.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ComandaDetalheDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ComandaDetalheDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _comandas.ObterAsync(id, cancellationToken));

    /// <summary>
    /// RN-CM-09: estorna uma venda fechada (motivo obrigatório). Vira "Estornada"; o caixa não muda — dinheiro devolvido
    /// ao cliente é uma sangria registrada no caixa aberto, pelo app.
    /// </summary>
    [HttpPost("{id:guid}/estornar")]
    [ProducesResponseType(typeof(EstornoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EstornoResponse>> Estornar(Guid id, EstornarComandaRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(JwtTokenService.ClaimId)?.Value, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _estorno.EstornarAsync(id, request, usuarioId, cancellationToken));
    }
}
