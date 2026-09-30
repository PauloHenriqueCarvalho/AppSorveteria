using GestaoSorveteria.Domain.Comandas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class PagamentoConfiguration : IEntityTypeConfiguration<Pagamento>
{
    public void Configure(EntityTypeBuilder<Pagamento> builder)
    {
        builder.ToTable("pagamentos");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ComandaId).IsRequired();
        builder.Property(p => p.Forma).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Valor).HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.ValorRecebido).HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.Troco).HasPrecision(12, 2).IsRequired();

        builder.HasIndex(p => p.Forma);
    }
}
