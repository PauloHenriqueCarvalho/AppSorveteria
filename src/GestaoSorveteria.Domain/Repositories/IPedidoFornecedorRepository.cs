using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Pedidos aos fornecedores (RN-PF-04…08), sempre com as linhas.</summary>
public interface IPedidoFornecedorRepository
{
    Task<PedidoFornecedor?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>RN-PF-04: o rascunho do fornecedor, se houver (o índice único garante no máximo um).</summary>
    Task<PedidoFornecedor?> ObterRascunhoAsync(Guid fornecedorId, CancellationToken cancellationToken = default);

    /// <summary>Último pedido recebido do fornecedor (o que faltou nele volta no próximo, RN-RC-04).</summary>
    Task<PedidoFornecedor?> ObterUltimoRecebidoAsync(Guid fornecedorId, CancellationToken cancellationToken = default);

    /// <summary>Pedidos criados desde <paramref name="desdeUtc"/> e todos os ainda não recebidos nem cancelados. Mais recentes primeiro.</summary>
    Task<IReadOnlyList<PedidoFornecedor>> ListarAsync(Guid? fornecedorId, DateTime desdeUtc, CancellationToken cancellationToken = default);

    /// <summary>Recebidos no intervalo [de, até): o recebido entra na conta da saída semanal (RN-ES-07).</summary>
    Task<IReadOnlyList<PedidoFornecedor>> ListarRecebidosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default);

    Task<int> ProximoNumeroAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(PedidoFornecedor pedido, CancellationToken cancellationToken = default);
}
