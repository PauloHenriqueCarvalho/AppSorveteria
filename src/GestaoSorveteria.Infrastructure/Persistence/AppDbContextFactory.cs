using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GestaoSorveteria.Infrastructure.Persistence;

/// <summary>
/// Usado só pelas ferramentas (`dotnet ef migrations add`, `dotnet ef database update`).
/// Lê a variável ConnectionStrings__Default ou usa o PostgreSQL local do docker-compose.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public const string ConexaoLocalPadrao =
        "Host=localhost;Port=5432;Database=sorveteria;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = ConexaoLocalPadrao;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
