using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class ProdutoRepository : IProdutoRepository
{
    private readonly AppDbContext _db;

    public ProdutoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Produto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Produtos.AsQueryable().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Produto>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default)
    {
        IQueryable<Produto> query = _db.Produtos;
        if (somenteAtivos)
        {
            query = query.Where(p => p.Ativo);
        }

        return await query
            .OrderBy(p => p.Categoria)
            .ThenBy(p => p.Ordem)
            .ThenBy(p => p.Nome)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExisteComNomeAsync(string nomeNormalizado, Guid? ignorarId = null, CancellationToken cancellationToken = default) =>
        _db.Produtos.AsQueryable().AnyAsync(
            p => p.NomeNormalizado == nomeNormalizado && (ignorarId == null || p.Id != ignorarId),
            cancellationToken);

    public async Task AdicionarAsync(Produto produto, CancellationToken cancellationToken = default) =>
        await _db.Produtos.AddAsync(produto, cancellationToken);
}
