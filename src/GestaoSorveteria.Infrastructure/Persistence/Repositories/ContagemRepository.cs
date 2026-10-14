using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class ContagemRepository : IContagemRepository
{
    /// <summary>Referência "contagem anterior": só olha os últimos meses (item parado há mais tempo fica sem referência).</summary>
    private static readonly TimeSpan JanelaDaAnterior = TimeSpan.FromDays(180);

    private readonly AppDbContext _db;

    public ContagemRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Contagem?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Contagens.Include(c => c.Itens).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Contagem>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await _db.Contagens
            .Include(c => c.Itens)
            .Where(c => c.ConcluidaEm == null || (c.IniciadaEm >= deUtc && c.IniciadaEm < ateUtc))
            .OrderByDescending(c => c.IniciadaEm)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, UltimaContagemDoItem>> UltimaContagemPorItemAsync(DateTime antesDeUtc, CancellationToken cancellationToken = default)
    {
        var desde = antesDeUtc - JanelaDaAnterior;
        var linhas = await (
                from linha in _db.ItensContagem
                join contagem in _db.Contagens on linha.ContagemId equals contagem.Id
                where contagem.ConcluidaEm != null && contagem.ConcluidaEm < antesDeUtc && contagem.ConcluidaEm >= desde
                    && linha.ContadoUnidades != null
                select new { linha.ItemEstoqueId, contagem.ConcluidaEm, linha.ContadoUnidades, linha.ContadoEm })
            .ToListAsync(cancellationToken);

        return linhas
            .GroupBy(l => l.ItemEstoqueId)
            .ToDictionary(g => g.Key, g =>
            {
                var ultima = g.OrderByDescending(l => l.ConcluidaEm).First();
                return new UltimaContagemDoItem(ultima.ContadoUnidades!.Value, ultima.ConcluidaEm!.Value, ultima.ContadoEm);
            });
    }

    public async Task AdicionarAsync(Contagem contagem, CancellationToken cancellationToken = default) =>
        await _db.Contagens.AddAsync(contagem, cancellationToken);
}
