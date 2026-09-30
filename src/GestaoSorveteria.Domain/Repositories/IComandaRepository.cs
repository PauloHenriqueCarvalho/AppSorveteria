using GestaoSorveteria.Domain.Comandas;

namespace GestaoSorveteria.Domain.Repositories;

public interface IComandaRepository
{
    /// <summary>Carrega a comanda com itens e pagamentos.</summary>
    Task<Comanda?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comanda>> ListarPorCaixaAsync(Guid caixaId, StatusComanda? status = null, CancellationToken cancellationToken = default);

    /// <summary>Comandas fechadas num intervalo (para relatórios e "vendas do dia").</summary>
    Task<IReadOnlyList<Comanda>> ListarFechadasAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    /// <summary>RN-CM-02: próximo número sequencial dentro do caixa.</summary>
    Task<int> ProximoNumeroAsync(Guid caixaId, CancellationToken cancellationToken = default);

    /// <summary>RN-CX-05.</summary>
    Task<bool> ExisteAbertaNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default);

    /// <summary>RN-CX-06 / RN-PG-05: soma dos pagamentos em dinheiro das comandas fechadas do caixa.</summary>
    Task<decimal> TotalDinheiroFechadasNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Comanda comanda, CancellationToken cancellationToken = default);
}
