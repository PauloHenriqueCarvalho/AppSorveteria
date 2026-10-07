using GestaoSorveteria.Domain.Caixas;

namespace GestaoSorveteria.Domain.Repositories;

public interface ICaixaRepository
{
    /// <summary>Carrega o caixa com os movimentos.</summary>
    Task<Caixa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>RN-CX-01: o único caixa aberto, ou null.</summary>
    Task<Caixa?> ObterAbertoAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Caixa>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    /// <summary>Id de movimento já usado em qualquer caixa (sincronização: Id repetido vira rejeição, não 409).</summary>
    Task<bool> ExisteMovimentoAsync(Guid movimentoId, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Caixa caixa, CancellationToken cancellationToken = default);
}
