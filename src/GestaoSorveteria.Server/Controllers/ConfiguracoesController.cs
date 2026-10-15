using GestaoSorveteria.Application.Avisos;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Configuração da loja: nome nas mensagens e WhatsApp da dona. O app lê; só a dona altera.</summary>
[ApiController]
[Route("api/configuracoes")]
[Produces("application/json")]
[Authorize]
public sealed class ConfiguracoesController : ControllerBase
{
    private readonly AvisoService _avisos;

    public ConfiguracoesController(AvisoService avisos)
    {
        _avisos = avisos;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ConfiguracaoLojaDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConfiguracaoLojaDto>> Obter(CancellationToken cancellationToken) =>
        Ok(await _avisos.ObterConfiguracaoAsync(cancellationToken));

    [HttpPut]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(ConfiguracaoLojaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ConfiguracaoLojaDto>> Salvar(SalvarConfiguracaoLojaRequest request, CancellationToken cancellationToken) =>
        Ok(await _avisos.SalvarConfiguracaoAsync(request, cancellationToken));
}
