using GestaoSorveteria.Domain.Comandas;

namespace GestaoSorveteria.Domain.Repositories;

public interface IComandaRepository
{
    /// <summary>Carrega a comanda com itens e pagamentos.</summary>
    Task<Comanda?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Comanda>> ListarPorCaixaAsync(Guid caixaId, StatusComanda? status = null, CancellationToken cancellationToken = default);

    /// <summary>Comandas fechadas num intervalo (para relatórios e "vendas do dia").</summary>
    Task<IReadOnlyList<Comanda>> ListarFechadasAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vendas para o painel: comandas cuja data da venda (fechamento, senão cancelamento, senão criação) está em
    /// [<paramref name="deUtc"/>, <paramref name="ateUtc"/>), mais recente primeiro, paginadas. Devolve também o total sem paginação.
    /// Só para leitura: as comandas vêm <b>sem os itens</b> (só pagamentos) e não rastreadas — não altere nem salve.
    /// </summary>
    Task<(IReadOnlyList<Comanda> Itens, int Total)> ListarPorPeriodoAsync(
        DateTime deUtc,
        DateTime ateUtc,
        StatusComanda? status,
        int pular,
        int quantidade,
        CancellationToken cancellationToken = default);

    /// <summary>RN-CM-02: próximo número sequencial dentro do caixa.</summary>
    Task<int> ProximoNumeroAsync(Guid caixaId, CancellationToken cancellationToken = default);

    /// <summary>RN-CX-05.</summary>
    Task<bool> ExisteAbertaNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// RN-CX-06 / RN-PG-05: soma dos pagamentos em dinheiro das vendas do caixa — fechadas e também as estornadas
    /// depois, porque o estorno não mexe no caixa (RN-CM-09; a devolução é uma sangria).
    /// </summary>
    Task<decimal> TotalDinheiroFechadasNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default);

    /// <summary>RN-CM-02: número já usado no caixa (sincronização: vira rejeição, não 409).</summary>
    Task<bool> ExisteNumeroNoCaixaAsync(Guid caixaId, int numero, CancellationToken cancellationToken = default);

    /// <summary>Algum desses Ids já existe como item ou pagamento (sincronização: vira rejeição, não 409).</summary>
    Task<bool> ExisteItemOuPagamentoAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    Task AdicionarAsync(Comanda comanda, CancellationToken cancellationToken = default);
}
