using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Sugestoes;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Sugestoes;

namespace GestaoSorveteria.Application.Sugestoes;

/// <summary>
/// Sugestões da dona no painel (RN-SG-01..05): o que ela quer mudar ou ganhar no sistema, com imagens e a conversa com o
/// Paulo sobre o andamento. Carrega → Domain → salva → DTO.
/// </summary>
public sealed class SugestaoService
{
    private readonly ISugestaoRepository _sugestoes;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public SugestaoService(ISugestaoRepository sugestoes, IUsuarioRepository usuarios, IUnitOfWork uow, IClock clock)
    {
        _sugestoes = sugestoes;
        _usuarios = usuarios;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Abertas primeiro (Nova, Em análise, Planejada), depois as encerradas; dentro de cada, a mais recente primeiro.</summary>
    public async Task<IReadOnlyList<SugestaoResumoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var lista = await _sugestoes.ListarAsync(cancellationToken);
        var imagens = (await _sugestoes.ListarImagensAsync(lista.Select(s => s.Id).ToList(), cancellationToken))
            .GroupBy(i => i.SugestaoId)
            .ToDictionary(g => g.Key, g => g.Count());
        var nomes = await NomesAsync(cancellationToken);
        return lista
            .OrderBy(s => s.Encerrada)
            .ThenByDescending(s => s.AtualizadaEm)
            .Select(s => new SugestaoResumoDto(
                s.Id, s.Titulo, s.Tipo.ToString(), s.Area.ToString(), s.Importancia.ToString(), s.Status.ToString(),
                Nome(nomes, s.CriadaPorUsuarioId), s.CriadaEm, s.AtualizadaEm, s.Comentarios.Count, imagens.GetValueOrDefault(s.Id)))
            .ToList();
    }

    public async Task<SugestaoDto> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MapearAsync(await CarregarAsync(id, cancellationToken), cancellationToken);

    public async Task<SugestaoDto> CriarAsync(SalvarSugestaoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var sugestao = Sugestao.Criar(
            request.Titulo, request.Descricao,
            TextoEnum.Converter<TipoSugestao>(request.Tipo, "Tipo"),
            TextoEnum.Converter<AreaSugestao>(request.Area, "Parte do sistema"),
            TextoEnum.Converter<ImportanciaSugestao>(request.Importancia, "Importância"),
            usuarioId, _clock.UtcNow);
        await _sugestoes.AdicionarAsync(sugestao, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapearAsync(sugestao, cancellationToken);
    }

    public async Task<SugestaoDto> AtualizarAsync(Guid id, SalvarSugestaoRequest request, CancellationToken cancellationToken = default)
    {
        var sugestao = await CarregarAsync(id, cancellationToken);
        sugestao.Editar(
            request.Titulo, request.Descricao,
            TextoEnum.Converter<TipoSugestao>(request.Tipo, "Tipo"),
            TextoEnum.Converter<AreaSugestao>(request.Area, "Parte do sistema"),
            TextoEnum.Converter<ImportanciaSugestao>(request.Importancia, "Importância"),
            _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapearAsync(sugestao, cancellationToken);
    }

    public async Task<SugestaoDto> ComentarAsync(Guid id, ComentarSugestaoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var sugestao = await CarregarAsync(id, cancellationToken);
        sugestao.Comentar(request.Texto, usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapearAsync(sugestao, cancellationToken);
    }

    public async Task<SugestaoDto> AlterarStatusAsync(Guid id, AlterarStatusSugestaoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var sugestao = await CarregarAsync(id, cancellationToken);
        sugestao.AlterarStatus(TextoEnum.Converter<StatusSugestao>(request.Status, "Andamento"), request.Comentario, usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapearAsync(sugestao, cancellationToken);
    }

    /// <summary>RN-SG-05: anexa uma imagem (até 5 por sugestão, 2 MB cada).</summary>
    public async Task<SugestaoDto> AnexarImagemAsync(
        Guid id, string? nomeArquivo, string? tipoConteudo, byte[] conteudo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var sugestao = await CarregarAsync(id, cancellationToken);
        var jaAnexadas = (await _sugestoes.ListarImagensAsync([id], cancellationToken)).Count;
        var agora = _clock.UtcNow;
        var imagem = SugestaoImagem.Anexar(sugestao, jaAnexadas, nomeArquivo, tipoConteudo, conteudo, usuarioId, agora);
        await _sugestoes.AdicionarImagemAsync(imagem, cancellationToken);
        sugestao.Comentar($"Imagem anexada: {imagem.NomeArquivo}.", usuarioId, agora);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapearAsync(sugestao, cancellationToken);
    }

    public async Task<SugestaoImagem> ObterImagemAsync(Guid id, Guid imagemId, CancellationToken cancellationToken = default) =>
        await _sugestoes.ObterImagemAsync(id, imagemId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Imagem", imagemId);

    /// <summary>RN-SG-05: imagem errada sai enquanto a sugestão não está encerrada; a conversa registra.</summary>
    public async Task<SugestaoDto> RemoverImagemAsync(Guid id, Guid imagemId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var sugestao = await CarregarAsync(id, cancellationToken);
        if (sugestao.Encerrada)
        {
            throw new GestaoSorveteria.Domain.Common.DomainException("Esta sugestão já foi encerrada: as imagens ficam como estão.");
        }

        var imagem = await ObterImagemAsync(id, imagemId, cancellationToken);
        _sugestoes.RemoverImagem(imagem);
        sugestao.Comentar($"Imagem retirada: {imagem.NomeArquivo}.", usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MapearAsync(sugestao, cancellationToken);
    }

    private async Task<Sugestao> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _sugestoes.ObterPorIdAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Sugestão", id);

    private async Task<SugestaoDto> MapearAsync(Sugestao s, CancellationToken cancellationToken)
    {
        var nomes = await NomesAsync(cancellationToken);
        var imagens = await _sugestoes.ListarImagensAsync([s.Id], cancellationToken);
        return new SugestaoDto(
            s.Id, s.Titulo, s.Descricao, s.Tipo.ToString(), s.Area.ToString(), s.Importancia.ToString(), s.Status.ToString(),
            s.Encerrada, s.CriadaPorUsuarioId, Nome(nomes, s.CriadaPorUsuarioId), s.CriadaEm, s.AtualizadaEm,
            s.Comentarios.OrderBy(c => c.Em)
                .Select(c => new SugestaoComentarioDto(c.Id, c.UsuarioId, Nome(nomes, c.UsuarioId), c.Texto, c.Em))
                .ToList(),
            imagens.Select(i => new SugestaoImagemDto(i.Id, i.NomeArquivo, i.TipoConteudo, i.TamanhoBytes, i.EnviadaEm)).ToList());
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NomesAsync(CancellationToken cancellationToken) =>
        (await _usuarios.ListarAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Nome);

    private static string Nome(IReadOnlyDictionary<Guid, string> nomes, Guid id) => nomes.GetValueOrDefault(id, "Usuário removido");
}
