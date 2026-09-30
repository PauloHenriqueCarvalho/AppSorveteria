using GestaoSorveteria.Domain.Produtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("produtos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Nome).HasMaxLength(80).IsRequired();
        builder.Property(p => p.NomeNormalizado).HasMaxLength(80).IsRequired();
        builder.Property(p => p.Categoria).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Preco).HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.PermiteValorLivre).IsRequired();
        builder.Property(p => p.Ativo).IsRequired();
        builder.Property(p => p.Ordem).IsRequired();
        builder.Property(p => p.CriadoEm).IsRequired();
        builder.Property(p => p.AtualizadoEm);

        // RN-PR-01: nome único sem diferenciar maiúsculas/minúsculas.
        builder.HasIndex(p => p.NomeNormalizado).IsUnique();
        builder.HasIndex(p => new { p.Ativo, p.Ordem });
    }
}
