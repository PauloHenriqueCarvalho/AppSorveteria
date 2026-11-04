using GestaoSorveteria.Domain.Sugestoes;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Sugestões da dona (RN-SG-*). As imagens ficam em outra tabela e só vêm com o conteúdo quando pedidas uma a uma.</summary>
public interface ISugestaoRepository
{
    /// <summary>Com a conversa.</summary>
    Task<Sugestao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Todas, com a conversa, as mais recentes (pela última atualização) primeiro.</summary>
    Task<IReadOnlyList<Sugestao>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>As imagens, sem o conteúdo, das sugestões pedidas, na ordem de envio.</summary>
    Task<IReadOnlyList<SugestaoImagemInfo>> ListarImagensAsync(IReadOnlyCollection<Guid> sugestaoIds, CancellationToken cancellationToken = default);

    /// <summary>Uma imagem, com o conteúdo.</summary>
    Task<SugestaoImagem?> ObterImagemAsync(Guid sugestaoId, Guid imagemId, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Sugestao sugestao, CancellationToken cancellationToken = default);

    Task AdicionarImagemAsync(SugestaoImagem imagem, CancellationToken cancellationToken = default);

    void RemoverImagem(SugestaoImagem imagem);
}
