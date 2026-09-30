using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Domain.Repositories;

public interface IProdutoRepository
{
    Task<Produto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Produto>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default);

    /// <param name="nomeNormalizado">Já normalizado com <see cref="Produto.NormalizarNome"/>.</param>
    Task<bool> ExisteComNomeAsync(string nomeNormalizado, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Produto produto, CancellationToken cancellationToken = default);
}
