using GestaoSorveteria.Domain.Clima;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

/// <summary>RN-CL-02: clima do dia por ponto (latitude/longitude com 2 casas). Um registro por ponto e dia.</summary>
internal sealed class ClimaDiaConfiguration : IEntityTypeConfiguration<ClimaDia>
{
    public void Configure(EntityTypeBuilder<ClimaDia> builder)
    {
        builder.ToTable("clima_dias");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Data).IsRequired();
        builder.Property(c => c.Latitude).HasPrecision(5, 2).IsRequired();
        builder.Property(c => c.Longitude).HasPrecision(5, 2).IsRequired();
        builder.Property(c => c.TempMax).HasPrecision(4, 1).IsRequired();
        builder.Property(c => c.TempMin).HasPrecision(4, 1).IsRequired();
        builder.Property(c => c.TempMedia).HasPrecision(4, 1).IsRequired();
        builder.Property(c => c.SensacaoMax).HasPrecision(4, 1);
        builder.Property(c => c.ChuvaMm).HasPrecision(6, 1);
        builder.Property(c => c.ChuvaHorasPct).HasPrecision(4, 1);
        builder.Property(c => c.NuvensPct).HasPrecision(4, 1);
        builder.Property(c => c.UmidadePct).HasPrecision(4, 1);
        builder.Property(c => c.Icone).HasMaxLength(40);
        builder.Property(c => c.Condicao).HasMaxLength(120);
        builder.Property(c => c.Origem).HasConversion<string>().HasMaxLength(12).IsRequired();
        builder.Property(c => c.Definitivo).IsRequired();
        builder.Property(c => c.Fonte).HasMaxLength(20).IsRequired();
        builder.Property(c => c.ColetadoEm).IsRequired();

        builder.Ignore(c => c.Ponto);

        builder.HasIndex(c => new { c.Latitude, c.Longitude, c.Data })
            .IsUnique()
            .HasDatabaseName("ux_clima_dias_por_ponto_e_dia");
    }
}

/// <summary>RN-CL-03: previsão guardada como estava, uma por ponto, dia em que foi feita e dia previsto. Nunca sobrescrita.</summary>
internal sealed class PrevisaoClimaConfiguration : IEntityTypeConfiguration<PrevisaoClima>
{
    public void Configure(EntityTypeBuilder<PrevisaoClima> builder)
    {
        builder.ToTable("previsoes_clima");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FeitaNoDia).IsRequired();
        builder.Property(p => p.FeitaEm).IsRequired();
        builder.Property(p => p.Data).IsRequired();
        builder.Property(p => p.Latitude).HasPrecision(5, 2).IsRequired();
        builder.Property(p => p.Longitude).HasPrecision(5, 2).IsRequired();
        builder.Property(p => p.TempMax).HasPrecision(4, 1).IsRequired();
        builder.Property(p => p.TempMin).HasPrecision(4, 1).IsRequired();
        builder.Property(p => p.ChuvaMm).HasPrecision(6, 1);
        builder.Property(p => p.ChanceChuvaPct).HasPrecision(4, 1);
        builder.Property(p => p.Icone).HasMaxLength(40);
        builder.Property(p => p.Condicao).HasMaxLength(120);
        builder.Property(p => p.Fonte).HasMaxLength(20).IsRequired();

        builder.Ignore(p => p.Antecedencia);

        builder.HasIndex(p => new { p.Latitude, p.Longitude, p.FeitaNoDia, p.Data })
            .IsUnique()
            .HasDatabaseName("ux_previsoes_clima_por_ponto_feita_e_dia");
        // Próximos dias: a previsão mais recente para um intervalo de dias.
        builder.HasIndex(p => new { p.Latitude, p.Longitude, p.Data });
    }
}
