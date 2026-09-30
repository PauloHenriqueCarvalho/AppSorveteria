namespace GestaoSorveteria.Domain.Common;

/// <summary>
/// Base de toda entidade. O Id pode vir do cliente (app offline gera o Guid)
/// ou ser gerado aqui quando não informado.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity()
    {
    }

    protected Entity(Guid? id)
    {
        Id = id is { } valor && valor != Guid.Empty ? valor : Guid.NewGuid();
    }

    public override bool Equals(object? obj) =>
        obj is Entity outra && outra.GetType() == GetType() && outra.Id == Id;

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
