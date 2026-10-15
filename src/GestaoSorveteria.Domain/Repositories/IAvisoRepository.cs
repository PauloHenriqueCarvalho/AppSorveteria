using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Avisos "Está acabando" (RN-AV-*).</summary>
public interface IAvisoRepository
{
    Task<Aviso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>RN-AV-03: o aviso aberto do item, se houver (o índice único garante no máximo um).</summary>
    Task<Aviso?> ObterAbertoDoItemAsync(Guid itemEstoqueId, CancellationToken cancellationToken = default);

    /// <summary>Abertos (qualquer data) e, se <paramref name="fechadosDesdeUtc"/> vier, os fechados a partir dali. Mais recentes primeiro.</summary>
    Task<IReadOnlyList<Aviso>> ListarAsync(DateTime? fechadosDesdeUtc, CancellationToken cancellationToken = default);

    /// <summary>O aviso do item fechado mais recentemente, se houver (sincronização: o tablet avisou antes de a dona fechar).</summary>
    Task<Aviso?> ObterUltimoFechadoDoItemAsync(Guid itemEstoqueId, CancellationToken cancellationToken = default);

    /// <summary>Criados, atualizados ou fechados no intervalo [de, até), em qualquer situação (resumo do dia, RN-AV-07).</summary>
    Task<IReadOnlyList<Aviso>> ListarAtualizadosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Aviso aviso, CancellationToken cancellationToken = default);
}
