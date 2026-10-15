using GestaoSorveteria.Application.Clima;
using GestaoSorveteria.Contracts.Clima;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>Clima da loja (docs/02, seção 9b): local, situação da coleta e "Atualizar agora". Só a dona.</summary>
[ApiController]
[Route("api/clima")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class ClimaController : ControllerBase
{
    private readonly ClimaService _clima;
    private readonly ColetaClimaService _coleta;

    public ClimaController(ClimaService clima, ColetaClimaService coleta)
    {
        _clima = clima;
        _coleta = coleta;
    }

    /// <summary>Chave configurada, local da loja, última coleta, último erro e os dias já guardados (RN-CL-04/07).</summary>
    [HttpGet("situacao")]
    [ProducesResponseType(typeof(SituacaoClimaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SituacaoClimaDto>> Situacao(CancellationToken cancellationToken) =>
        Ok(await _clima.SituacaoAsync(cancellationToken));

    /// <summary>RN-CL-01: o lugar que o serviço de clima encontrou pelo texto (cidade e estado), para a dona confirmar.</summary>
    [HttpGet("procurar-local")]
    [ProducesResponseType(typeof(LocalLojaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LocalLojaDto>> ProcurarLocal([FromQuery] string? texto, CancellationToken cancellationToken) =>
        Ok(await _clima.ProcurarLocalAsync(texto, cancellationToken));

    /// <summary>RN-CL-01: grava o local confirmado e já coleta o histórico e a previsão (RN-CL-04).</summary>
    [HttpPut("local")]
    [ProducesResponseType(typeof(SituacaoClimaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SituacaoClimaDto>> DefinirLocal(DefinirLocalLojaRequest request, CancellationToken cancellationToken) =>
        Ok(await _clima.DefinirLocalAsync(request, cancellationToken));

    /// <summary>RN-CL-04: coleta agora, sem esperar as 3 h (dias ainda não definitivos são buscados de novo).</summary>
    [HttpPost("atualizar")]
    [ProducesResponseType(typeof(ColetaClimaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ColetaClimaDto>> Atualizar(CancellationToken cancellationToken) =>
        Ok(await _coleta.ColetarAsync(forcar: true, cancellationToken));
}
