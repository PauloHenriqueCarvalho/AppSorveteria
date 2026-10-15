using GestaoSorveteria.Domain.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class GrupoEstoqueConfiguration : IEntityTypeConfiguration<GrupoEstoque>
{
    public void Configure(EntityTypeBuilder<GrupoEstoque> builder)
    {
        builder.ToTable("grupos_estoque");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Nome).HasMaxLength(40).IsRequired();
        builder.Property(g => g.NomeNormalizado).HasMaxLength(40).IsRequired();
        builder.Property(g => g.ModoControle).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(g => g.Ordem).IsRequired();

        builder.Ignore(g => g.EntraNaContagem);

        // RN-ES-11.
        builder.HasIndex(g => g.NomeNormalizado).IsUnique();
    }
}
