using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class AvisoRepository : IAvisoRepository
{
    private readonly AppDbContext _db;

    public AvisoRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Aviso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Avisos.AsQueryable().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Aviso?> ObterAbertoDoItemAsync(Guid itemEstoqueId, CancellationToken cancellationToken = default) =>
        _db.Avisos.AsQueryable().FirstOrDefaultAsync(
            a => a.ItemEstoqueId == itemEstoqueId && a.Status == StatusAviso.Aberto,
            cancellationToken);

    public async Task<IReadOnlyList<Aviso>> ListarAsync(DateTime? fechadosDesdeUtc, CancellationToken cancellationToken = default) =>
        await _db.Avisos
            .Where(a => a.Status == StatusAviso.Aberto || (fechadosDesdeUtc != null && a.FechadoEm >= fechadosDesdeUtc))
            .OrderByDescending(a => a.AtualizadoEm)
            .ToListAsync(cancellationToken);

    public Task<Aviso?> ObterUltimoFechadoDoItemAsync(Guid itemEstoqueId, CancellationToken cancellationToken = default) =>
        _db.Avisos
            .Where(a => a.ItemEstoqueId == itemEstoqueId && a.Status == StatusAviso.Fechado)
            .OrderByDescending(a => a.FechadoEm)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Aviso>> ListarAtualizadosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await _db.Avisos
            .Where(a => (a.CriadoEm >= deUtc && a.CriadoEm < ateUtc)
                || (a.AtualizadoEm >= deUtc && a.AtualizadoEm < ateUtc)
                || (a.FechadoEm >= deUtc && a.FechadoEm < ateUtc))
            .ToListAsync(cancellationToken);

    public async Task AdicionarAsync(Aviso aviso, CancellationToken cancellationToken = default) =>
        await _db.Avisos.AddAsync(aviso, cancellationToken);
}
