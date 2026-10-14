using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class AvisoConfiguration : IEntityTypeConfiguration<Aviso>
{
    public void Configure(EntityTypeBuilder<Aviso> builder)
    {
        builder.ToTable("avisos");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.ItemEstoqueId);
        builder.Property(a => a.TextoLivre).HasMaxLength(120);
        builder.Property(a => a.QuantidadeAtual).IsRequired();
        builder.Property(a => a.Prioridade).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(a => a.Observacao).HasMaxLength(200);
        builder.Property(a => a.CriadoPorUsuarioId); // nulo = criado pelo sistema (RN-AV-04)
        builder.Property(a => a.CriadoEm).IsRequired();
        builder.Property(a => a.AtualizadoPorUsuarioId);
        builder.Property(a => a.AtualizadoEm).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(a => a.FechadoEm);
        builder.Property(a => a.FechadoPorUsuarioId);
        builder.Property(a => a.MotivoFechamento).HasMaxLength(200);
        builder.Property(a => a.FechadoPeloPedidoId); // pedido ao fornecedor (Sprint 6): a FK entra com a tabela de pedidos
        builder.Property(a => a.WhatsAppAbertoEm).HasColumnName("whatsapp_aberto_em");

        builder.Ignore(a => a.Aberto);
        builder.Ignore(a => a.CriadoPeloSistema);

        // RN-AV-03: um aviso aberto por item (o segundo "Está acabando" vira "ainda falta" no mesmo aviso).
        builder.HasIndex(a => a.ItemEstoqueId)
            .IsUnique()
            .HasFilter("status = 'Aberto' AND item_estoque_id IS NOT NULL")
            .HasDatabaseName("ux_avisos_um_aberto_por_item");

        builder.HasIndex(a => new { a.Status, a.CriadoEm });

        builder.HasOne<ItemEstoque>()
            .WithMany()
            .HasForeignKey(a => a.ItemEstoqueId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(a => a.CriadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(a => a.AtualizadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(a => a.FechadoPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
