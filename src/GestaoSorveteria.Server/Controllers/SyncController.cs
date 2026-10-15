using GestaoSorveteria.Application.Avisos;
using GestaoSorveteria.Application.Contagens;
using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Contracts.Contagens;
using GestaoSorveteria.Contracts.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Sincronização local-first (docs/03 §8): o app envia documentos prontos, com Ids gerados no celular.
/// Responde 200 com um resultado por documento (aceita / ja_recebida / rejeitada + motivo); um documento
/// rejeitado não derruba o lote. 400 só para lote vazio ou grande demais. 409 é transitório (dois envios do
/// mesmo documento ao mesmo tempo): o app reenvia e recebe ja_recebida.
/// </summary>
[ApiController]
[Route("api/sync")]
[Produces("application/json")]
[Authorize]
public sealed class SyncController : ControllerBase
{
    private readonly SyncService _sync;
    private readonly AvisoService _avisos;
    private readonly ContagemService _contagens;

    public SyncController(SyncService sync, AvisoService avisos, ContagemService contagens)
    {
        _sync = sync;
        _avisos = avisos;
        _contagens = contagens;
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

    /// <summary>
    /// Comandas fechadas ou canceladas (comanda + itens + pagamentos), remontadas com as regras do Domain:
    /// preço da venda (RN-SY-06), total/pagamentos/troco conferidos, venda de caixa já fechado aceita e marcada (RN-CX-08).
    /// </summary>
    [HttpPost("comandas")]
    [ProducesResponseType(typeof(SyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResponse>> Comandas(SyncComandasRequest request, CancellationToken cancellationToken) =>
        Ok(await _sync.ReceberComandasAsync(request, cancellationToken));

    /// <summary>
    /// Avisos "Está acabando" criados ou atualizados no tablet (estado atual de cada um). Item que já tinha aviso aberto no
    /// servidor é juntado a ele (<c>avisoId</c> diferente do enviado); aviso que a dona fechou volta com <c>aberto = false</c>.
    /// </summary>
    [HttpPost("avisos")]
    [ProducesResponseType(typeof(SyncAvisosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncAvisosResponse>> Avisos(SyncAvisosRequest request, CancellationToken cancellationToken) =>
        Ok(await _avisos.ReceberSyncAsync(request, cancellationToken));

    /// <summary>
    /// Contagens semanais feitas no tablet (RN-ES-05), parciais ou concluídas, com todas as linhas registradas.
    /// Reenvio sem novidade → <c>ja_recebida</c>; mudança numa contagem já concluída → <c>rejeitada</c> (correção = nova contagem).
    /// </summary>
    [HttpPost("contagens")]
    [ProducesResponseType(typeof(SyncResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SyncResponse>> Contagens(SyncContagensRequest request, CancellationToken cancellationToken) =>
        Ok(await _contagens.ReceberSyncAsync(request, cancellationToken));
}
