namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Confirma, numa única transação, tudo que os repositórios alteraram no caso de uso.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Esquece o que foi alterado e ainda não confirmado. Usado na sincronização em lote: um documento rejeitado
    /// não pode deixar alteração pela metade para o próximo <see cref="SaveChangesAsync"/> do mesmo lote.
    /// </summary>
    void DescartarAlteracoes();
}
