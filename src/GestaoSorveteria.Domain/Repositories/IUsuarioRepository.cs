using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Domain.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <param name="login">Já normalizado com <see cref="Usuario.NormalizarLogin"/>.</param>
    Task<Usuario?> ObterPorLoginAsync(string login, CancellationToken cancellationToken = default);

    Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
