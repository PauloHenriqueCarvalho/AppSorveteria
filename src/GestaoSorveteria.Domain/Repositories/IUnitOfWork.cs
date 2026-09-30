namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Confirma, numa única transação, tudo que os repositórios alteraram no caso de uso.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
