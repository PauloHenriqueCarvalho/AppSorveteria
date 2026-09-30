using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GestaoSorveteria.Infrastructure.Persistence;

/// <summary>
/// Contexto EF Core. Também é a unidade de trabalho: os repositórios só marcam alterações;
/// quem confirma é <see cref="SaveChangesAsync(CancellationToken)"/> no fim do caso de uso.
/// </summary>
public sealed class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Produto> Produtos => Set<Produto>();
    public DbSet<Caixa> Caixas => Set<Caixa>();
    public DbSet<MovimentoCaixa> MovimentosCaixa => Set<MovimentoCaixa>();
    public DbSet<Comanda> Comandas => Set<Comanda>();
    public DbSet<ItemComanda> ItensComanda => Set<ItemComanda>();
    public DbSet<Pagamento> Pagamentos => Set<Pagamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // ADR-005: os Ids são gerados no cliente/domínio (Entity → Guid.NewGuid()), nunca pelo EF.
        // Sem isto, um filho novo adicionado a um agregado já rastreado (ex.: item numa comanda carregada)
        // seria salvo como UPDATE e falharia com DbUpdateConcurrencyException.
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            var chave = entidade.FindPrimaryKey();
            if (chave is null)
            {
                continue;
            }

            foreach (var propriedade in chave.Properties)
            {
                propriedade.ValueGenerated = ValueGenerated.Never;
            }
        }

        modelBuilder.AplicarSnakeCase();
    }
}
