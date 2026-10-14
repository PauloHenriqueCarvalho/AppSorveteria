using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class PedidoFornecedorRepository : IPedidoFornecedorRepository
{
    private readonly AppDbContext _db;

    public PedidoFornecedorRepository(AppDbContext db)
    {
        _db = db;
    }

    private IQueryable<PedidoFornecedor> ComItens => _db.PedidosFornecedor.Include(p => p.Itens);

    public Task<PedidoFornecedor?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ComItens.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<PedidoFornecedor?> ObterRascunhoAsync(Guid fornecedorId, CancellationToken cancellationToken = default) =>
        ComItens.FirstOrDefaultAsync(p => p.FornecedorId == fornecedorId && p.Status == StatusPedido.Rascunho, cancellationToken);

    public async Task<IReadOnlyList<PedidoFornecedor>> ListarAsync(Guid? fornecedorId, DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        await ComItens
            .Where(p => fornecedorId == null || p.FornecedorId == fornecedorId)
            .Where(p => p.CriadoEm >= desdeUtc || p.Status == StatusPedido.Rascunho || p.Status == StatusPedido.Enviado)
            .OrderByDescending(p => p.CriadoEm)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PedidoFornecedor>> ListarRecebidosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        await ComItens
            .Where(p => p.Status == StatusPedido.Recebido && p.RecebidoEm >= deUtc && p.RecebidoEm < ateUtc)
            .ToListAsync(cancellationToken);

    public async Task<int> ProximoNumeroAsync(CancellationToken cancellationToken = default) =>
        (await _db.PedidosFornecedor.MaxAsync(p => (int?)p.Numero, cancellationToken) ?? 0) + 1;

    public async Task AdicionarAsync(PedidoFornecedor pedido, CancellationToken cancellationToken = default) =>
        await _db.PedidosFornecedor.AddAsync(pedido, cancellationToken);
}
