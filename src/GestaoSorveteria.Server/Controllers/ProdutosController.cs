using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Contracts.Produtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

[ApiController]
[Route("api/produtos")]
[Produces("application/json")]
[Authorize]
public sealed class ProdutosController : ControllerBase
{
    private readonly ProdutoService _produtos;

    public ProdutosController(ProdutoService produtos)
    {
        _produtos = produtos;
    }

    /// <summary>
    /// Catálogo para o app. Sem <c>desde</c> vem tudo; com <c>desde</c> (data/hora com fuso, ex.: 2026-09-30T12:00:00Z)
    /// vêm só os produtos criados/alterados a partir dali, inclusive os desativados. Guarde <c>geradoEmUtc</c> para a próxima chamada.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CatalogoProdutosResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CatalogoProdutosResponse>> Listar([FromQuery] DateTime? desde, CancellationToken cancellationToken)
    {
        // O binder de DateTime usa AdjustToUniversal: com "Z" ou fuso chega Kind.Utc; sem fuso chega Unspecified (o serviço assume UTC).
        var catalogo = await _produtos.ListarAsync(desde, cancellationToken);
        return Ok(catalogo);
    }
}
