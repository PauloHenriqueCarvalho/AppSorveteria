using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Domain.Repositories;

public interface ICobrancaPixRepository
{
    Task<CobrancaPix?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CobrancaPix?> ObterPorTxidAsync(string txid, CancellationToken cancellationToken = default);

    Task AdicionarAsync(CobrancaPix cobranca, CancellationToken cancellationToken = default);
}
