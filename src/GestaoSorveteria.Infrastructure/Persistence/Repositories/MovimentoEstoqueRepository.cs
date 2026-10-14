using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class MovimentoEstoqueRepository : IMovimentoEstoqueRepository
{
    private readonly AppDbContext _db;

    public MovimentoEstoqueRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AdicionarAsync(MovimentoEstoque movimento, CancellationToken cancellationToken = default) =>
        await _db.MovimentosEstoque.AddAsync(movimento, cancellationToken);

    public async Task<IReadOnlyList<MovimentoEstoque>> ListarDosItensAsync(
        IReadOnlyCollection<Guid> itemEstoqueIds, DateTime? desdeUtc = null, CancellationToken cancellationToken = default)
    {
        if (itemEstoqueIds.Count == 0)
        {
            return [];
        }

        var ids = itemEstoqueIds.Distinct().ToList();
        return await _db.MovimentosEstoque
            .Where(m => ids.Contains(m.ItemEstoqueId) && (desdeUtc == null || m.Em > desdeUtc))
            .OrderBy(m => m.Em)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MovimentoEstoque>> ListarDaComandaAsync(Guid comandaId, CancellationToken cancellationToken = default) =>
        await _db.MovimentosEstoque.Where(m => m.ComandaId == comandaId).ToListAsync(cancellationToken);
}
