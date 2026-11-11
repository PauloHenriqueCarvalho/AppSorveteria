using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Pix;
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

        // RN-PX-05: txid da cobrança Pix integrada que pagou (conferência com cobrancas_pix).
        builder.Property(p => p.PixTxid).HasMaxLength(CobrancaPix.TamanhoMaximoTxid);
        builder.HasIndex(p => p.PixTxid);

        builder.HasIndex(p => p.Forma);
    }
}
