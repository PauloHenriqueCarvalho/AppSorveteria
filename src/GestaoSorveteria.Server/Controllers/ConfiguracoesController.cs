using GestaoSorveteria.Application.Avisos;
using GestaoSorveteria.Application.Loja;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Contracts.Loja;
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
    private readonly PrimeirosPassosService _primeirosPassos;

    public ConfiguracoesController(AvisoService avisos, PrimeirosPassosService primeirosPassos)
    {
        _avisos = avisos;
        _primeirosPassos = primeirosPassos;
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

    /// <summary>RN-LJ-01: o que já foi feito para a loja começar a usar o sistema (lista do Início do painel).</summary>
    [HttpGet("primeiros-passos")]
    [Authorize(Policy = Politicas.Admin)]
    [ProducesResponseType(typeof(PrimeirosPassosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PrimeirosPassosDto>> PrimeirosPassos(CancellationToken cancellationToken) =>
        Ok(await _primeirosPassos.ObterAsync(cancellationToken));
}
