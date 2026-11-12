using GestaoSorveteria.Domain.Pix;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class CobrancaPixRepository(AppDbContext db) : ICobrancaPixRepository
{
    public Task<CobrancaPix?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.CobrancasPix.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CobrancaPix?> ObterPorTxidAsync(string txid, CancellationToken cancellationToken = default) =>
        db.CobrancasPix.FirstOrDefaultAsync(c => c.Txid == txid, cancellationToken);

    public async Task AdicionarAsync(CobrancaPix cobranca, CancellationToken cancellationToken = default) =>
        await db.CobrancasPix.AddAsync(cobranca, cancellationToken);
}
