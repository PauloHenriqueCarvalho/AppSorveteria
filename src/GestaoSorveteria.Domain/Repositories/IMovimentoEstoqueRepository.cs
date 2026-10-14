using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Movimentos do saldo dos potes (RN-ES-08/09). Só acrescenta: nada é alterado nem apagado.</summary>
public interface IMovimentoEstoqueRepository
{
    Task AdicionarAsync(MovimentoEstoque movimento, CancellationToken cancellationToken = default);

    /// <summary>Movimentos dos itens; <paramref name="desdeUtc"/> = só os que aconteceram depois (pelo <c>Em</c>).</summary>
    Task<IReadOnlyList<MovimentoEstoque>> ListarDosItensAsync(
        IReadOnlyCollection<Guid> itemEstoqueIds, DateTime? desdeUtc = null, CancellationToken cancellationToken = default);

    /// <summary>Baixas e estornos de uma venda (para estornar).</summary>
    Task<IReadOnlyList<MovimentoEstoque>> ListarDaComandaAsync(Guid comandaId, CancellationToken cancellationToken = default);
}
