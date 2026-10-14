using GestaoSorveteria.Application.Pedidos;
using GestaoSorveteria.Contracts.Pedidos;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Pedido ao fornecedor e conferência da entrega (docs/10 §2.4–2.5). Só Admin (RN-RC-07).</summary>
[ApiController]
[Route("api/pedidos-fornecedor")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class PedidosFornecedorController : ControllerBase
{
    private readonly PedidoService _pedidos;

    public PedidosFornecedorController(PedidoService pedidos)
    {
        _pedidos = pedidos;
    }

    /// <summary>Últimos 90 dias e os ainda não recebidos; filtra por fornecedor se informado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PedidoResumoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PedidoResumoDto>>> Listar([FromQuery] Guid? fornecedorId, CancellationToken cancellationToken) =>
        Ok(await _pedidos.ListarAsync(fornecedorId, cancellationToken));

    /// <summary>
    /// RN-PF-02…04: monta (ou remonta) o rascunho do fornecedor com a sugestão de agora; o que a dona alterou à mão é mantido.
    /// Com <c>automatico=true</c> (o painel ao abrir no dia do pedido), se o pedido de hoje já foi enviado, devolve ele em vez
    /// de começar outro.
    /// </summary>
    [HttpPost("montar")]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoDto>> Montar([FromQuery] Guid fornecedorId, [FromQuery] bool automatico, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _pedidos.MontarAsync(fornecedorId, usuarioId, automatico, cancellationToken));
    }

    /// <summary>O pedido completo: tela, folha impressa (RN-PF-07) e conferência.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _pedidos.ObterAsync(id, cancellationToken));

    /// <summary>+/− no rascunho; item que não está no pedido é acrescentado.</summary>
    /// <summary>Mensagens do WhatsApp para reabrir: o pedido (enviado/recebido) e o que veio diferente na entrega.</summary>
    [HttpGet("{id:guid}/mensagens")]
    [ProducesResponseType(typeof(MensagensPedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MensagensPedidoDto>> Mensagens(Guid id, CancellationToken cancellationToken) =>
        Ok(await _pedidos.MensagensAsync(id, cancellationToken));

    [HttpPut("{id:guid}/itens")]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoDto>> AlterarItens(Guid id, AlterarItensPedidoRequest request, CancellationToken cancellationToken) =>
        Ok(await _pedidos.AlterarItensAsync(id, request, cancellationToken));

    /// <summary>RN-PF-06: confirma; com fornecedor por WhatsApp, devolve a mensagem e o link.</summary>
    [HttpPost("{id:guid}/enviar")]
    [ProducesResponseType(typeof(EnviarPedidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnviarPedidoResponse>> Enviar(Guid id, CancellationToken cancellationToken) =>
        Ok(await _pedidos.EnviarAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancelar")]
    [ProducesResponseType(typeof(PedidoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoDto>> Cancelar(Guid id, CancelarPedidoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _pedidos.CancelarAsync(id, request, usuarioId, cancellationToken));
    }

    /// <summary>
    /// RN-RC-01…05: conferência da entrega. Listas vazias = "chegou tudo certo". Fecha os avisos dos itens que chegaram e
    /// devolve a mensagem para o fornecedor quando veio diferente.
    /// </summary>
    [HttpPost("{id:guid}/receber")]
    [ProducesResponseType(typeof(ReceberPedidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReceberPedidoResponse>> Receber(Guid id, ReceberPedidoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _pedidos.ReceberAsync(id, request, usuarioId, cancellationToken));
    }
}
