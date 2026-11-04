using GestaoSorveteria.Application.Sugestoes;
using GestaoSorveteria.Contracts.Sugestoes;
using GestaoSorveteria.Domain.Sugestoes;
using GestaoSorveteria.Server.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GestaoSorveteria.Server.Controllers;

/// <summary>
/// Sugestões da dona (RN-SG-01..05): o que ela quer mudar ou ganhar no sistema, com imagens e a conversa sobre o
/// andamento. Só Admin (a dona e o Paulo).
/// </summary>
[ApiController]
[Route("api/sugestoes")]
[Produces("application/json")]
[Authorize(Policy = Politicas.Admin)]
public sealed class SugestoesController : ControllerBase
{
    private readonly SugestaoService _sugestoes;

    public SugestoesController(SugestaoService sugestoes)
    {
        _sugestoes = sugestoes;
    }

    /// <summary>Abertas primeiro, depois as encerradas; a mais recente primeiro.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SugestaoResumoDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SugestaoResumoDto>>> Listar(CancellationToken cancellationToken) =>
        Ok(await _sugestoes.ListarAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SugestaoDto>> Obter(Guid id, CancellationToken cancellationToken) =>
        Ok(await _sugestoes.ObterAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SugestaoDto>> Criar(SalvarSugestaoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        var sugestao = await _sugestoes.CriarAsync(request, usuarioId, cancellationToken);
        return CreatedAtAction(nameof(Obter), new { id = sugestao.Id }, sugestao);
    }

    /// <summary>RN-SG-02: muda o texto enquanto a sugestão não está encerrada.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SugestaoDto>> Atualizar(Guid id, SalvarSugestaoRequest request, CancellationToken cancellationToken) =>
        Ok(await _sugestoes.AtualizarAsync(id, request, cancellationToken));

    /// <summary>RN-SG-04: uma mensagem na conversa.</summary>
    [HttpPost("{id:guid}/comentarios")]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SugestaoDto>> Comentar(Guid id, ComentarSugestaoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _sugestoes.ComentarAsync(id, request, usuarioId, cancellationToken));
    }

    /// <summary>RN-SG-03: muda o andamento (recusar pede o motivo).</summary>
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SugestaoDto>> AlterarStatus(Guid id, AlterarStatusSugestaoRequest request, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _sugestoes.AlterarStatusAsync(id, request, usuarioId, cancellationToken));
    }

    /// <summary>RN-SG-05: anexa uma imagem (campo <c>arquivo</c> do formulário; JPG, PNG ou WebP até 2 MB).</summary>
    [HttpPost("{id:guid}/imagens")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SugestaoImagem.TamanhoMaximoBytes + 64 * 1024)]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SugestaoDto>> AnexarImagem(Guid id, IFormFile? arquivo, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        if (arquivo is null || arquivo.Length == 0)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Escolha uma imagem para anexar.");
        }

        if (arquivo.Length > SugestaoImagem.TamanhoMaximoBytes)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "A imagem passa de 2 MB. Tire um print menor ou reduza a foto.");
        }

        using var memoria = new MemoryStream();
        await arquivo.CopyToAsync(memoria, cancellationToken);
        return Ok(await _sugestoes.AnexarImagemAsync(id, arquivo.FileName, arquivo.ContentType, memoria.ToArray(), usuarioId, cancellationToken));
    }

    /// <summary>O conteúdo da imagem (o painel busca com o token e mostra).</summary>
    [HttpGet("{id:guid}/imagens/{imagemId:guid}")]
    [Produces("image/jpeg", "image/png", "image/webp")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Imagem(Guid id, Guid imagemId, CancellationToken cancellationToken)
    {
        var imagem = await _sugestoes.ObterImagemAsync(id, imagemId, cancellationToken);
        Response.Headers.CacheControl = "private, max-age=86400";
        return File(imagem.Conteudo, imagem.TipoConteudo);
    }

    /// <summary>RN-SG-05: tira uma imagem errada (só enquanto a sugestão não está encerrada).</summary>
    [HttpDelete("{id:guid}/imagens/{imagemId:guid}")]
    [ProducesResponseType(typeof(SugestaoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SugestaoDto>> RemoverImagem(Guid id, Guid imagemId, CancellationToken cancellationToken)
    {
        if (!UsuarioLogado.TentarObterId(User, out var usuarioId))
        {
            return Unauthorized();
        }

        return Ok(await _sugestoes.RemoverImagemAsync(id, imagemId, usuarioId, cancellationToken));
    }
}
