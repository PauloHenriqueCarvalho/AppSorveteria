using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Server.Security;
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

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProdutoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _produtos.ObterAsync(id, cancellationToken));

    /// <summary>Cadastro pelo painel (RN-PR-01: nome único).</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(ProdutoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoDto>> Criar(SalvarProdutoRequest request, CancellationToken cancellationToken)
    {
        var produto = await _produtos.CriarAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = produto.Id }, produto);
    }

    /// <summary>Edição pelo painel. Alterar o preço não muda vendas passadas (RN-PR-04).</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(ProdutoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoDto>> Atualizar(Guid id, SalvarProdutoRequest request, CancellationToken cancellationToken) =>
        Ok(await _produtos.AtualizarAsync(id, request, cancellationToken));

    /// <summary>
    /// RN-PR-06: liga o produto a um grupo de estoque (grupo nulo = desliga). Grupo de potes faz o app pedir o sabor e a
    /// venda baixar o saldo (RN-ES-09).
    /// </summary>
    [HttpPut("{id:guid}/estoque")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(ProdutoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDto>> LigarAoEstoque(Guid id, LigarProdutoEstoqueRequest request, CancellationToken cancellationToken) =>
        Ok(await _produtos.LigarAoEstoqueAsync(id, request, cancellationToken));

    /// <summary>
    /// RN-PR-07/RN-RL-04: custo estimado de uma unidade vendida (pelo catálogo de estoque ou informado) e a margem sobre o
    /// preço atual de cada produto. Só para o painel.
    /// </summary>
    [HttpGet("custos")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(IReadOnlyList<CustoProdutoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CustoProdutoDto>>> Custos(CancellationToken cancellationToken) =>
        Ok(await _produtos.ListarCustosAsync(cancellationToken));

    /// <summary>RN-PR-07: informa o custo de uma unidade vendida (produto sem vínculo de estoque ou grupo sem custo); nulo apaga.</summary>
    [HttpPut("{id:guid}/custo")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(CustoProdutoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustoProdutoDto>> InformarCusto(Guid id, InformarCustoProdutoRequest request, CancellationToken cancellationToken) =>
        Ok(await _produtos.InformarCustoAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/ativar")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(ProdutoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDto>> Ativar(Guid id, CancellationToken cancellationToken) =>
        Ok(await _produtos.AtivarAsync(id, cancellationToken));

    /// <summary>RN-PR-03: produto nunca é apagado, só desativado (some do app na próxima sincronização).</summary>
    [HttpPost("{id:guid}/desativar")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(ProdutoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDto>> Desativar(Guid id, CancellationToken cancellationToken) =>
        Ok(await _produtos.DesativarAsync(id, cancellationToken));
}
