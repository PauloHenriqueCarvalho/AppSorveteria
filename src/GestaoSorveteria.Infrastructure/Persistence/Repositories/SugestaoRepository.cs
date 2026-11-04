using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Sugestoes;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class SugestaoRepository : ISugestaoRepository
{
    private readonly AppDbContext _db;

    public SugestaoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Sugestao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Sugestoes.Include(s => s.Comentarios).FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Sugestao>> ListarAsync(CancellationToken cancellationToken = default) =>
        await _db.Sugestoes
            .Include(s => s.Comentarios)
            .AsSplitQuery()
            .OrderByDescending(s => s.AtualizadaEm)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SugestaoImagemInfo>> ListarImagensAsync(
        IReadOnlyCollection<Guid> sugestaoIds, CancellationToken cancellationToken = default)
    {
        if (sugestaoIds.Count == 0)
        {
            return [];
        }

        // Sem o conteúdo: a lista e a tela da sugestão só precisam do nome e do tamanho.
        return await _db.SugestaoImagens
            .Where(i => sugestaoIds.Contains(i.SugestaoId))
            .OrderBy(i => i.EnviadaEm)
            .Select(i => new SugestaoImagemInfo(i.Id, i.SugestaoId, i.NomeArquivo, i.TipoConteudo, i.TamanhoBytes, i.EnviadaEm))
            .ToListAsync(cancellationToken);
    }

    public Task<SugestaoImagem?> ObterImagemAsync(Guid sugestaoId, Guid imagemId, CancellationToken cancellationToken = default) =>
        _db.SugestaoImagens.FirstOrDefaultAsync(i => i.Id == imagemId && i.SugestaoId == sugestaoId, cancellationToken);

    public async Task AdicionarAsync(Sugestao sugestao, CancellationToken cancellationToken = default) =>
        await _db.Sugestoes.AddAsync(sugestao, cancellationToken);

    public async Task AdicionarImagemAsync(SugestaoImagem imagem, CancellationToken cancellationToken = default) =>
        await _db.SugestaoImagens.AddAsync(imagem, cancellationToken);

    public void RemoverImagem(SugestaoImagem imagem) => _db.SugestaoImagens.Remove(imagem);
}
