using GestaoSorveteria.Contracts.Versao;
using GestaoSorveteria.Server.Configuracao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

[ApiController]
[Route("api/versao")]
[Produces("application/json")]
public sealed class VersaoController : ControllerBase
{
    private static readonly string VersaoApi = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    private readonly AppVersaoOptions _app;

    public VersaoController(AppVersaoOptions app)
    {
        _app = app;
    }

    /// <summary>Versão da API e versão mínima/atual do app. Público: o app chama antes do login para saber se precisa atualizar.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VersaoResponse), StatusCodes.Status200OK)]
    public ActionResult<VersaoResponse> Obter() =>
        Ok(new VersaoResponse(
            VersaoApi,
            _app.VersaoMinima,
            _app.VersaoAtual,
            string.IsNullOrWhiteSpace(_app.LinkApk) ? null : _app.LinkApk));
}
