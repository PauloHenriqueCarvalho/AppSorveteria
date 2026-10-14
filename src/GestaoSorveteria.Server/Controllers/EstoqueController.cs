using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Catálogo de estoque da Fase 2 (docs/10): grupos, itens e importação da planilha.
/// Só Admin (RN-RC-07: traz custo e fornecedor). A lista sem custos do app fica em <see cref="EstoqueAppController"/>.
/// </summary>
[ApiController]
[Route("api/estoque")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class EstoqueController : ControllerBase
{
    private readonly CatalogoEstoqueService _catalogo;

    public EstoqueController(CatalogoEstoqueService catalogo)
    {
        _catalogo = catalogo;
    }

    /// <summary>Fornecedores, grupos (na ordem da planilha) e itens, inclusive os desativados.</summary>
    [HttpGet("catalogo")]
    [ProducesResponseType(typeof(CatalogoEstoqueResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CatalogoEstoqueResponse>> Catalogo(CancellationToken cancellationToken) =>
        Ok(await _catalogo.ObterCatalogoAsync(cancellationToken));

    /// <summary>RN-ES-02: modo de controle <c>Contagem</c>, <c>SaldoPelaVenda</c> ou <c>SoAviso</c>.</summary>
    [HttpPost("grupos")]
    [ProducesResponseType(typeof(GrupoEstoqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GrupoEstoqueDto>> CriarGrupo(SalvarGrupoEstoqueRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.CriarGrupoAsync(request, cancellationToken));

    [HttpPut("grupos/{id:guid}")]
    [ProducesResponseType(typeof(GrupoEstoqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GrupoEstoqueDto>> AtualizarGrupo(Guid id, SalvarGrupoEstoqueRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.AtualizarGrupoAsync(id, request, cancellationToken));

    /// <summary>RN-ES-01: grupo + nome único. Item de pote sem mínimo informado ganha o mínimo padrão (2).</summary>
    [HttpPost("itens")]
    [ProducesResponseType(typeof(ItemEstoqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemEstoqueDto>> CriarItem(SalvarItemEstoqueRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.CriarItemAsync(request, cancellationToken));

    [HttpPut("itens/{id:guid}")]
    [ProducesResponseType(typeof(ItemEstoqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ItemEstoqueDto>> AtualizarItem(Guid id, SalvarItemEstoqueRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.AtualizarItemAsync(id, request, cancellationToken));

    [HttpPost("itens/{id:guid}/ativar")]
    [ProducesResponseType(typeof(ItemEstoqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemEstoqueDto>> AtivarItem(Guid id, CancellationToken cancellationToken) =>
        Ok(await _catalogo.AtivarItemAsync(id, cancellationToken));

    /// <summary>Item nunca é apagado: sai da contagem e do pedido, o histórico fica.</summary>
    [HttpPost("itens/{id:guid}/desativar")]
    [ProducesResponseType(typeof(ItemEstoqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItemEstoqueDto>> DesativarItem(Guid id, CancellationToken cancellationToken) =>
        Ok(await _catalogo.DesativarItemAsync(id, cancellationToken));

    /// <summary>
    /// RN-ES-10: importa o CSV da planilha criando só o que falta. Mande primeiro com <c>simular = true</c> (prévia, nada gravado)
    /// e depois com <c>simular = false</c> para confirmar.
    /// </summary>
    [HttpPost("itens/importar")]
    [RequestSizeLimit(4_000_000)]
    [ProducesResponseType(typeof(ImportarCatalogoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ImportarCatalogoResponse>> Importar(ImportarCatalogoRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.ImportarAsync(request, cancellationToken));
}
