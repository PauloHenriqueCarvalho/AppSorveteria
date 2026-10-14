using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class EstoqueRepository : IEstoqueRepository
{
    private readonly AppDbContext _db;

    public EstoqueRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Fornecedor>> ListarFornecedoresAsync(CancellationToken cancellationToken = default) =>
        await _db.Fornecedores.OrderBy(f => f.Nome).ToListAsync(cancellationToken);

    public Task<Fornecedor?> ObterFornecedorAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Fornecedores.AsQueryable().FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public async Task AdicionarFornecedorAsync(Fornecedor fornecedor, CancellationToken cancellationToken = default) =>
        await _db.Fornecedores.AddAsync(fornecedor, cancellationToken);

    public async Task<IReadOnlyList<GrupoEstoque>> ListarGruposAsync(CancellationToken cancellationToken = default) =>
        await _db.GruposEstoque.OrderBy(g => g.Ordem).ThenBy(g => g.Nome).ToListAsync(cancellationToken);

    public Task<GrupoEstoque?> ObterGrupoAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.GruposEstoque.AsQueryable().FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task AdicionarGrupoAsync(GrupoEstoque grupo, CancellationToken cancellationToken = default) =>
        await _db.GruposEstoque.AddAsync(grupo, cancellationToken);

    public async Task<IReadOnlyList<ItemEstoque>> ListarItensAsync(bool somenteAtivos, CancellationToken cancellationToken = default)
    {
        var query = from item in _db.ItensEstoque
                    join grupo in _db.GruposEstoque on item.GrupoId equals grupo.Id
                    where !somenteAtivos || item.Ativo
                    orderby grupo.Ordem, grupo.Nome, item.Ordem, item.Nome
                    select item;

        return await query.ToListAsync(cancellationToken);
    }

    public Task<ItemEstoque?> ObterItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ItensEstoque.AsQueryable().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<bool> ExisteItemComNomeAsync(Guid grupoId, string nomeNormalizado, Guid? ignorarId = null, CancellationToken cancellationToken = default) =>
        _db.ItensEstoque.AsQueryable().AnyAsync(
            i => i.GrupoId == grupoId && i.NomeNormalizado == nomeNormalizado && (ignorarId == null || i.Id != ignorarId),
            cancellationToken);

    public async Task AdicionarItemAsync(ItemEstoque item, CancellationToken cancellationToken = default) =>
        await _db.ItensEstoque.AddAsync(item, cancellationToken);
}
