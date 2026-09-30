using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class CaixaConfiguration : IEntityTypeConfiguration<Caixa>
{
    public void Configure(EntityTypeBuilder<Caixa> builder)
    {
        builder.ToTable("caixas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.AbertoPorUsuarioId).IsRequired();
        builder.Property(c => c.AbertoEm).IsRequired();
        builder.Property(c => c.FundoTroco).HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.FechadoPorUsuarioId);
        builder.Property(c => c.FechadoEm);
        builder.Property(c => c.TotalVendasDinheiro).HasPrecision(12, 2);
        builder.Property(c => c.ValorEsperado).HasPrecision(12, 2);
        builder.Property(c => c.ValorContado).HasPrecision(12, 2);
        builder.Property(c => c.Diferenca).HasPrecision(12, 2);
        builder.Property(c => c.Observacao).HasMaxLength(500);
        builder.Property(c => c.TotalVendasDinheiroApp).HasPrecision(12, 2);
        builder.Property(c => c.ValorEsperadoApp).HasPrecision(12, 2);
        builder.Property(c => c.DivergenciaSincronizacao).IsRequired();

        // Propriedades calculadas: não viram coluna.
        builder.Ignore(c => c.EstaAberto);
        builder.Ignore(c => c.TotalSangrias);
        builder.Ignore(c => c.TotalSuprimentos);

        // RN-CX-01: só um caixa aberto por vez (índice único parcial; a coluna vira "status" em snake_case).
        builder.HasIndex(c => c.Status)
            .IsUnique()
            .HasFilter("status = 'Aberto'")
            .HasDatabaseName("ux_caixas_unico_aberto");

        builder.HasIndex(c => c.AbertoEm);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.AbertoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.FechadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Movimentos)
            .WithOne()
            .HasForeignKey(m => m.CaixaId)
            .OnDelete(DeleteBehavior.Restrict); // nada de caixa ou movimento é apagado

        builder.Navigation(c => c.Movimentos)
            .HasField("_movimentos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
