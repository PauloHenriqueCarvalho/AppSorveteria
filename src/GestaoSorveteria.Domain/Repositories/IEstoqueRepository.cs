using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Catálogo de estoque da Fase 2: fornecedores, grupos e itens (docs/10).</summary>
public interface IEstoqueRepository
{
    Task<IReadOnlyList<Fornecedor>> ListarFornecedoresAsync(CancellationToken cancellationToken = default);

    Task<Fornecedor?> ObterFornecedorAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarFornecedorAsync(Fornecedor fornecedor, CancellationToken cancellationToken = default);

    /// <summary>Grupos na ordem da planilha (<see cref="GrupoEstoque.Ordem"/>, depois nome).</summary>
    Task<IReadOnlyList<GrupoEstoque>> ListarGruposAsync(CancellationToken cancellationToken = default);

    Task<GrupoEstoque?> ObterGrupoAsync(Guid id, CancellationToken cancellationToken = default);

    Task AdicionarGrupoAsync(GrupoEstoque grupo, CancellationToken cancellationToken = default);

    /// <summary>Itens ordenados por grupo e ordem.</summary>
    Task<IReadOnlyList<ItemEstoque>> ListarItensAsync(bool somenteAtivos, CancellationToken cancellationToken = default);

    Task<ItemEstoque?> ObterItemAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>RN-ES-01: grupo + nome único, sem diferenciar maiúsculas.</summary>
    Task<bool> ExisteItemComNomeAsync(Guid grupoId, string nomeNormalizado, Guid? ignorarId = null, CancellationToken cancellationToken = default);

    Task AdicionarItemAsync(ItemEstoque item, CancellationToken cancellationToken = default);
}
