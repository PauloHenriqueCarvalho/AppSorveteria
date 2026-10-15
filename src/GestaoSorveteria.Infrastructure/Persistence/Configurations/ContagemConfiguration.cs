using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class ContagemConfiguration : IEntityTypeConfiguration<Contagem>
{
    public void Configure(EntityTypeBuilder<Contagem> builder)
    {
        builder.ToTable("contagens");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UsuarioId).IsRequired();
        builder.Property(c => c.IniciadaEm).IsRequired();
        builder.Property(c => c.ConcluidaEm);
        builder.Property(c => c.Origem).HasConversion<string>().HasMaxLength(10).IsRequired();

        builder.Ignore(c => c.Concluida);

        // Só a contagem concluída vale (RN-ES-05): a última concluída é a base da saída da semana.
        builder.HasIndex(c => c.ConcluidaEm);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Itens)
            .WithOne()
            .HasForeignKey(i => i.ContagemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(c => c.Itens)
            .HasField("_itens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ItemContagemConfiguration : IEntityTypeConfiguration<ItemContagem>
{
    public void Configure(EntityTypeBuilder<ItemContagem> builder)
    {
        builder.ToTable("itens_contagem");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ContagemId).IsRequired();
        builder.Property(i => i.ItemEstoqueId).IsRequired();
        builder.Property(i => i.ContadoUnidades); // nulo = não contado (RN-ES-06)
        builder.Property(i => i.SaldoSistemaUnidades); // potes: saldo no momento da contagem (RN-ES-08)
        builder.Property(i => i.ContadoEm); // quando o item foi contado (base do saldo); nulo = vale a conclusão

        builder.Ignore(i => i.Diferenca);

        // Uma linha por item em cada contagem: registrar de novo corrige a mesma linha.
        builder.HasIndex(i => new { i.ContagemId, i.ItemEstoqueId }).IsUnique();
        builder.HasIndex(i => i.ItemEstoqueId);

        builder.HasOne<ItemEstoque>()
            .WithMany()
            .HasForeignKey(i => i.ItemEstoqueId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
