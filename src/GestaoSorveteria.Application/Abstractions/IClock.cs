namespace GestaoSorveteria.Application.Abstractions;

/// <summary>Relógio injetável (RN-TD-01): sempre UTC. Nos testes, um relógio fixo.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
