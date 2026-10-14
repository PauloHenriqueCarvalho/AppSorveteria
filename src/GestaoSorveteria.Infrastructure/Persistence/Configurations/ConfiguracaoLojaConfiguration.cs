using GestaoSorveteria.Domain.Loja;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class ConfiguracaoLojaConfiguration : IEntityTypeConfiguration<ConfiguracaoLoja>
{
    public void Configure(EntityTypeBuilder<ConfiguracaoLoja> builder)
    {
        builder.ToTable("configuracao_loja");
        builder.HasKey(c => c.Id); // uma linha só: Id fixo (ConfiguracaoLoja.IdUnico)

        builder.Property(c => c.NomeLoja).HasMaxLength(60).IsRequired();
        builder.Property(c => c.WhatsAppDona).HasColumnName("whatsapp_dona").HasMaxLength(13);
        builder.Property(c => c.AtualizadoEm).IsRequired();
    }
}
