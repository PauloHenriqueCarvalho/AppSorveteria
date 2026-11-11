using GestaoSorveteria.Domain.Pix;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

/// <summary>RN-PX-02/03: cobranças Pix com valor criadas no banco para o balcão. Nada é apagado.</summary>
internal sealed class CobrancaPixConfiguration : IEntityTypeConfiguration<CobrancaPix>
{
    public void Configure(EntityTypeBuilder<CobrancaPix> builder)
    {
        builder.ToTable("cobrancas_pix");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Txid).HasMaxLength(CobrancaPix.TamanhoMaximoTxid).IsRequired();
        builder.Property(c => c.UsuarioId).IsRequired();
        builder.Property(c => c.Valor).HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.PixCopiaECola).HasMaxLength(CobrancaPix.TamanhoMaximoCopiaECola).IsRequired();
        builder.Property(c => c.CriadaEm).IsRequired();
        builder.Property(c => c.ExpiraEm).IsRequired();
        builder.Property(c => c.AtualizadaEm).IsRequired();
        builder.Property(c => c.EndToEndId).HasMaxLength(CobrancaPix.TamanhoMaximoEndToEndId);
        builder.Property(c => c.ValorPago).HasPrecision(12, 2);
        builder.Ignore(c => c.PagaComValorCerto);

        builder.HasIndex(c => c.Txid).IsUnique();
        builder.HasIndex(c => c.CriadaEm);
    }
}
