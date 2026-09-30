using GestaoSorveteria.Application.Abstractions;

namespace GestaoSorveteria.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
