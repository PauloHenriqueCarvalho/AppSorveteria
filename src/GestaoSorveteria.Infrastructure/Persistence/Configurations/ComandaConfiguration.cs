using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class ComandaConfiguration : IEntityTypeConfiguration<Comanda>
{
    public void Configure(EntityTypeBuilder<Comanda> builder)
    {
        builder.ToTable("comandas");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Numero).IsRequired();
        builder.Property(c => c.CaixaId).IsRequired();
        builder.Property(c => c.UsuarioId).IsRequired();
        builder.Property(c => c.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.Observacao).HasMaxLength(300);
        builder.Property(c => c.Total).HasPrecision(12, 2).IsRequired();
        builder.Property(c => c.CriadaEm).IsRequired();
        builder.Property(c => c.RecebidaEm).IsRequired();
        builder.Property(c => c.FechadaEm);
        builder.Property(c => c.CanceladaEm);
        builder.Property(c => c.MotivoCancelamento).HasMaxLength(300);
        builder.Property(c => c.RecebidaAposFechamentoCaixa).IsRequired();
        builder.Property(c => c.EstornadaEm);
        builder.Property(c => c.EstornadaPorUsuarioId);
        builder.Property(c => c.MotivoEstorno).HasMaxLength(300);

        // Dois estornos (ou estorno + outra alteração) ao mesmo tempo: o segundo UPDATE falha com 409 em vez de
        // sobrescrever quem estornou e o motivo (RN-CM-09). xmin é coluna de sistema do PostgreSQL.
        builder.Property<uint>("Versao").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.Ignore(c => c.EstaAberta);
        builder.Ignore(c => c.EstaFechada);
        builder.Ignore(c => c.EntraNoCaixa);
        builder.Ignore(c => c.TotalEmDinheiro);
        builder.Ignore(c => c.TotalTroco);

        // RN-CM-02: número sequencial único dentro do caixa.
        builder.HasIndex(c => new { c.CaixaId, c.Numero }).IsUnique();
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.FechadaEm);

        builder.HasOne<Caixa>()
            .WithMany()
            .HasForeignKey(c => c.CaixaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.EstornadaPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Itens)
            .WithOne()
            .HasForeignKey(i => i.ComandaId)
            .OnDelete(DeleteBehavior.ClientCascade); // banco sem cascade; EF ainda apaga o item removido da comanda aberta (RN-CM-06)

        builder.Navigation(c => c.Itens)
            .HasField("_itens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(c => c.Pagamentos)
            .WithOne()
            .HasForeignKey(p => p.ComandaId)
            .OnDelete(DeleteBehavior.Restrict); // nada de pagamento é apagado

        builder.Navigation(c => c.Pagamentos)
            .HasField("_pagamentos")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
