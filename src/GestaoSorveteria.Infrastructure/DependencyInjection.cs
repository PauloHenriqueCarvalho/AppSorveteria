using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Infrastructure.Clima;
using GestaoSorveteria.Infrastructure.Persistence;
using GestaoSorveteria.Infrastructure.Persistence.Repositories;
using GestaoSorveteria.Infrastructure.Security;
using GestaoSorveteria.Infrastructure.Seed;
using GestaoSorveteria.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoSorveteria.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registra banco, repositórios, unidade de trabalho, hash de senha, relógio e seed.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A string de conexão do banco não foi configurada (ConnectionStrings:Default).", nameof(connectionString));
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                // Include de itens + pagamentos numa comanda vira produto cartesiano em query única;
                // split query faz uma consulta por coleção.
                npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<ICaixaRepository, CaixaRepository>();
        services.AddScoped<IComandaRepository, ComandaRepository>();
        services.AddScoped<IEstoqueRepository, EstoqueRepository>();
        services.AddScoped<IAvisoRepository, AvisoRepository>();
        services.AddScoped<IContagemRepository, ContagemRepository>();
        services.AddScoped<IPedidoFornecedorRepository, PedidoFornecedorRepository>();
        services.AddScoped<IConfiguracaoLojaRepository, ConfiguracaoLojaRepository>();
        services.AddScoped<IMovimentoEstoqueRepository, MovimentoEstoqueRepository>();
        services.AddScoped<IClimaRepository, ClimaRepository>();
        services.AddScoped<ISugestaoRepository, SugestaoRepository>();

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }

    /// <summary>
    /// RN-CL-07: serviço de clima (Visual Crossing). HttpClient próprio, sem o log do IHttpClientFactory: a chave vai na URL
    /// e não pode parar nos logs do Render.
    /// </summary>
    public static IServiceCollection AddFonteClima(this IServiceCollection services, ClimaOptions opcoes)
    {
        opcoes.Normalizar();
        services.AddSingleton(opcoes);
        services.AddSingleton<IFonteClima>(_ => new VisualCrossingFonteClima(
            new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(15) })
            {
                Timeout = TimeSpan.FromSeconds(40),
            },
            opcoes));

        return services;
    }
}
