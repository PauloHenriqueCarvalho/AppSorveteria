using GestaoSorveteria.Infrastructure;
using GestaoSorveteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace GestaoSorveteria.Tests.Integracao;

/// <summary>
/// PostgreSQL de verdade para os testes de integração: cria um banco novo, aplica todas as migrações e apaga no fim.
/// Só roda com a variável <see cref="Variavel"/> (no CI vem do serviço postgres; local: o do docker compose, porta 5433).
/// Sem ela, os testes de integração são pulados — os de domínio e aplicação continuam rodando sem banco.
/// </summary>
public sealed class BancoPostgres : IAsyncLifetime
{
    public const string Variavel = "SORVETERIA_TESTES_PG";

    private ServiceProvider? _provedor;

    public bool Disponivel => _provedor is not null;

    public async ValueTask InitializeAsync()
    {
        var conexao = Environment.GetEnvironmentVariable(Variavel);
        if (string.IsNullOrWhiteSpace(conexao))
        {
            return;
        }

        var banco = new NpgsqlConnectionStringBuilder(conexao) { Database = $"sorveteria_teste_{Guid.NewGuid():N}" };
        _provedor = new ServiceCollection().AddInfrastructure(banco.ConnectionString).BuildServiceProvider();
        await using var escopo = _provedor.CreateAsyncScope();
        // Cria o banco e aplica as migrações do zero, como a API faz ao subir no Render (Database:MigrateOnStartup).
        await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    /// <summary>Um escopo = um DbContext novo (como uma requisição da API).</summary>
    public AsyncServiceScope NovoEscopo()
    {
        Assert.SkipUnless(Disponivel, $"Sem PostgreSQL para os testes de integração (defina {Variavel}).");
        return _provedor!.CreateAsyncScope();
    }

    public async ValueTask DisposeAsync()
    {
        if (_provedor is null)
        {
            return;
        }

        await using (var escopo = _provedor.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await _provedor.DisposeAsync();
    }
}
