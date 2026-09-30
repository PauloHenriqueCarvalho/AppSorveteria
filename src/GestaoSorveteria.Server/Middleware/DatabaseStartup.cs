using GestaoSorveteria.Infrastructure.Persistence;
using GestaoSorveteria.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Server.Middleware;

/// <summary>
/// Na subida: aplica migrações (só se Database:MigrateOnStartup = true) e cria o Admin inicial.
/// Em produção, rode `dotnet ef database update` no deploy e deixe MigrateOnStartup = false.
/// </summary>
public static class DatabaseStartup
{
    public static async Task PrepararBancoAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = app.Logger;

        try
        {
            var todas = db.Database.GetMigrations().ToList();
            if (todas.Count == 0)
            {
                logger.LogWarning(
                    "Nenhuma migração encontrada. Rode: dotnet ef migrations add InitialCreate -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server -o Persistence/Migrations");
                return;
            }

            var pendentes = (await db.Database.GetPendingMigrationsAsync()).ToList();
            if (pendentes.Count > 0)
            {
                if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
                {
                    logger.LogInformation("Aplicando {Quantidade} migração(ões) pendente(s)...", pendentes.Count);
                    await db.Database.MigrateAsync();
                }
                else
                {
                    logger.LogWarning(
                        "Há {Quantidade} migração(ões) pendente(s) e Database:MigrateOnStartup está desligado. Rode: dotnet ef database update",
                        pendentes.Count);
                    return;
                }
            }

            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
        }
        catch (Exception ex)
        {
            // Não derruba a API: o /health vai acusar o problema e o log mostra a causa.
            logger.LogError(ex, "Falha ao preparar o banco de dados. Verifique ConnectionStrings:Default e se o PostgreSQL está no ar.");
        }
    }
}
