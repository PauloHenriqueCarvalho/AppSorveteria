using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class CaixaRepository : ICaixaRepository
{
    private readonly AppDbContext _db;

    public CaixaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Caixa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Caixas
            .Include(c => c.Movimentos)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Caixa?> ObterAbertoAsync(CancellationToken cancellationToken = default) =>
        _db.Caixas
            .Include(c => c.Movimentos)
            .FirstOrDefaultAsync(c => c.Status == StatusCaixa.Aberto, cancellationToken);

    public async Task<IReadOnlyList<Caixa>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await _db.Caixas
            .Include(c => c.Movimentos)
            .Where(c => c.AbertoEm >= deUtc && c.AbertoEm < ateUtc)
            .OrderByDescending(c => c.AbertoEm)
            .ToListAsync(cancellationToken);

    public async Task AdicionarAsync(Caixa caixa, CancellationToken cancellationToken = default) =>
        await _db.Caixas.AddAsync(caixa, cancellationToken);
}
