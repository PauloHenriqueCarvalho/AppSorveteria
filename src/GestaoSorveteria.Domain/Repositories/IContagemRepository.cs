using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Contagens semanais (RN-ES-05/06), com as linhas.</summary>
public interface IContagemRepository
{
    Task<Contagem?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Iniciadas no intervalo [de, até) e todas as que ainda não foram concluídas. Mais recentes primeiro.</summary>
    Task<IReadOnlyList<Contagem>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// A última quantidade contada de cada item em contagens concluídas antes de <paramref name="antesDeUtc"/>
    /// (referência da contagem anterior, RN-ES-06). Item nunca contado não aparece.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> UltimaContagemPorItemAsync(DateTime antesDeUtc, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Contagem contagem, CancellationToken cancellationToken = default);
}
