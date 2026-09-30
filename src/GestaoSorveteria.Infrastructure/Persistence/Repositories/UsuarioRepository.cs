using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _db;

    public UsuarioRepository(AppDbContext db)
    {
        _db = db;
    }

    // .AsQueryable() evita ambiguidade entre os operadores async do EF Core e os de System.Linq.AsyncEnumerable (.NET 10).
    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Usuarios.AsQueryable().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<Usuario?> ObterPorLoginAsync(string login, CancellationToken cancellationToken = default) =>
        _db.Usuarios.AsQueryable().FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    public Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default) =>
        _db.Usuarios.AsQueryable().AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancellationToken = default) =>
        await _db.Usuarios.OrderBy(u => u.Nome).ToListAsync(cancellationToken);

    public async Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default) =>
        await _db.Usuarios.AddAsync(usuario, cancellationToken);
}
