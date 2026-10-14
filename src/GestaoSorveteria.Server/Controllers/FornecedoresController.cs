using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Fornecedores (RN-PF-01, RN-PF-09). A lista vem em <c>GET /api/estoque/catalogo</c>.</summary>
[ApiController]
[Route("api/fornecedores")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class FornecedoresController : ControllerBase
{
    private readonly CatalogoEstoqueService _catalogo;

    public FornecedoresController(CatalogoEstoqueService catalogo)
    {
        _catalogo = catalogo;
    }

    /// <summary>Dias de pedido em inglês, como no .NET ("Monday"); envio <c>Impresso</c>, <c>WhatsApp</c> ou <c>Ambos</c>.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(FornecedorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FornecedorDto>> Criar(SalvarFornecedorRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.CriarFornecedorAsync(request, cancellationToken));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FornecedorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FornecedorDto>> Atualizar(Guid id, SalvarFornecedorRequest request, CancellationToken cancellationToken) =>
        Ok(await _catalogo.AtualizarFornecedorAsync(id, request, cancellationToken));

    [HttpPost("{id:guid}/ativar")]
    [ProducesResponseType(typeof(FornecedorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FornecedorDto>> Ativar(Guid id, CancellationToken cancellationToken) =>
        Ok(await _catalogo.AtivarFornecedorAsync(id, cancellationToken));

    /// <summary>RN-PF-09: recusado (400) enquanto o fornecedor tiver itens ativos.</summary>
    [HttpPost("{id:guid}/desativar")]
    [ProducesResponseType(typeof(FornecedorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FornecedorDto>> Desativar(Guid id, CancellationToken cancellationToken) =>
        Ok(await _catalogo.DesativarFornecedorAsync(id, cancellationToken));
}
