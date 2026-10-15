using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Contracts.Estoque;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Catálogo de estoque para o app do atendente ("Está acabando" e contagem): sem custo. Separado do
/// <see cref="EstoqueController"/> (só Admin) para nenhuma ação nova de lá ficar aberta ao atendente por engano.
/// </summary>
[ApiController]
[Route("api/estoque/itens")]
[Produces("application/json")]
[Authorize]
public sealed class EstoqueAppController : ControllerBase
{
    private readonly CatalogoEstoqueService _catalogo;

    public EstoqueAppController(CatalogoEstoqueService catalogo)
    {
        _catalogo = catalogo;
    }

    /// <summary>Grupos e itens, inclusive os desativados (o app esconde), sem custo nem fornecedor.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(CatalogoEstoqueAppResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CatalogoEstoqueAppResponse>> Listar(CancellationToken cancellationToken) =>
        Ok(await _catalogo.ObterCatalogoAppAsync(cancellationToken));
}
