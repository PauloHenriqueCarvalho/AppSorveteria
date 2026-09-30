using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class MovimentoCaixaConfiguration : IEntityTypeConfiguration<MovimentoCaixa>
{
    public void Configure(EntityTypeBuilder<MovimentoCaixa> builder)
    {
        builder.ToTable("movimentos_caixa");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.CaixaId).IsRequired();
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Valor).HasPrecision(12, 2).IsRequired();
        builder.Property(m => m.Motivo).HasMaxLength(200).IsRequired();
        builder.Property(m => m.UsuarioId).IsRequired();
        builder.Property(m => m.Em).IsRequired();

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(m => m.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
