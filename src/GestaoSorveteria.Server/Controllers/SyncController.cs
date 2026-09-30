using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Contracts.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Sincronização local-first (docs/03 §8): o app envia documentos prontos, com Ids gerados no celular.
/// Responde 200 com um resultado por documento (aceita / ja_recebida / rejeitada + motivo); um documento
/// rejeitado não derruba o lote. 400 só para lote vazio ou grande demais.
/// </summary>
[ApiController]
[Route("api/sync")]
[Produces("application/json")]
[Authorize]
public sealed class SyncController : ControllerBase
{
    private readonly SyncService _sync;

    public SyncController(SyncService sync)
    {
        _sync = sync;
    }

    /// <summary>
    /// Caixas (abertura + movimentos + fechamento, se houver). Ordem: caixa aberto → comandas → caixa fechado.
    /// Reenviar o mesmo caixa só acrescenta; caixa já fechado no servidor não muda (RN-CX-07).
    /// </summary>
    [HttpPost("caixas")]
    [ProducesResponseType(typeof(SyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResponse>> Caixas(SyncCaixasRequest request, CancellationToken cancellationToken) =>
        Ok(await _sync.ReceberCaixasAsync(request, cancellationToken));
}
