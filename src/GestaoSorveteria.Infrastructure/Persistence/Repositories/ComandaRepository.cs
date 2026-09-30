using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class ComandaRepository : IComandaRepository
{
    private readonly AppDbContext _db;

    public ComandaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<Comanda?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Comandas
            .Include(c => c.Itens)
            .Include(c => c.Pagamentos)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Comanda>> ListarPorCaixaAsync(Guid caixaId, StatusComanda? status = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Comanda> query = _db.Comandas
            .Include(c => c.Itens)
            .Include(c => c.Pagamentos)
            .Where(c => c.CaixaId == caixaId);

        if (status is { } filtro)
        {
            query = query.Where(c => c.Status == filtro);
        }

        return await query
            .OrderBy(c => c.Numero)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Comanda>> ListarFechadasAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await _db.Comandas
            .Include(c => c.Itens)
            .Include(c => c.Pagamentos)
            .Where(c => c.Status == StatusComanda.Fechada && c.FechadaEm >= deUtc && c.FechadaEm < ateUtc)
            .OrderBy(c => c.FechadaEm)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// RN-CM-02: próximo número sequencial dentro do caixa.
    /// Dois celulares abrindo comanda no mesmo instante podem receber o mesmo número: o índice único
    /// (caixa_id, numero) rejeita o segundo (409) e o caso de uso (Sprint 1) deve tentar de novo com o próximo número.
    /// </summary>
    public async Task<int> ProximoNumeroAsync(Guid caixaId, CancellationToken cancellationToken = default)
    {
        var maior = await _db.Comandas
            .Where(c => c.CaixaId == caixaId)
            .MaxAsync(c => (int?)c.Numero, cancellationToken);

        return (maior ?? 0) + 1;
    }

    public Task<bool> ExisteAbertaNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        _db.Comandas.AsQueryable().AnyAsync(c => c.CaixaId == caixaId && c.Status == StatusComanda.Aberta, cancellationToken);

    public async Task<decimal> TotalDinheiroFechadasNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default)
    {
        var valores =
            from pagamento in _db.Pagamentos
            join comanda in _db.Comandas on pagamento.ComandaId equals comanda.Id
            where comanda.CaixaId == caixaId
                  && comanda.Status == StatusComanda.Fechada
                  && pagamento.Forma == FormaPagamento.Dinheiro
            select pagamento.Valor;

        return await valores.SumAsync(cancellationToken);
    }

    public Task<bool> ExisteNumeroNoCaixaAsync(Guid caixaId, int numero, CancellationToken cancellationToken = default) =>
        _db.Comandas.AnyAsync(c => c.CaixaId == caixaId && c.Numero == numero, cancellationToken);

    public async Task<bool> ExisteItemOuPagamentoAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        ids.Count > 0
        && (await _db.ItensComanda.AnyAsync(i => ids.Contains(i.Id), cancellationToken)
            || await _db.Pagamentos.AnyAsync(p => ids.Contains(p.Id), cancellationToken));

    public async Task AdicionarAsync(Comanda comanda, CancellationToken cancellationToken = default) =>
        await _db.Comandas.AddAsync(comanda, cancellationToken);
}
