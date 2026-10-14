using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

/// <summary>RN-ES-08/09: movimentos do saldo dos potes. Nunca apagados; os índices únicos seguram a repetição da sincronização.</summary>
internal sealed class MovimentoEstoqueConfiguration : IEntityTypeConfiguration<MovimentoEstoque>
{
    public void Configure(EntityTypeBuilder<MovimentoEstoque> builder)
    {
        builder.ToTable("movimentos_estoque");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.ItemEstoqueId).IsRequired();
        builder.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(15).IsRequired();
        builder.Property(m => m.Unidades).IsRequired();
        builder.Property(m => m.Em).IsRequired();
        builder.Property(m => m.RegistradoEm).IsRequired();
        builder.Property(m => m.PedidoId);
        builder.Property(m => m.ComandaId);
        builder.Property(m => m.ItemComandaId);

        builder.Ignore(m => m.Efeito);

        // Saldo de um item: movimentos depois da última contagem.
        builder.HasIndex(m => new { m.ItemEstoqueId, m.Em });
        // Uma baixa e no máximo um estorno por linha de venda (a comanda pode chegar de novo na sincronização).
        builder.HasIndex(m => new { m.Tipo, m.ItemComandaId })
            .IsUnique()
            .HasFilter("item_comanda_id IS NOT NULL")
            .HasDatabaseName("ux_movimentos_estoque_por_linha_de_venda");
        // Uma entrada por item em cada pedido conferido.
        builder.HasIndex(m => new { m.Tipo, m.PedidoId, m.ItemEstoqueId })
            .IsUnique()
            .HasFilter("pedido_id IS NOT NULL")
            .HasDatabaseName("ux_movimentos_estoque_entrada_por_pedido");
        builder.HasIndex(m => m.ComandaId);

        builder.HasOne<ItemEstoque>().WithMany().HasForeignKey(m => m.ItemEstoqueId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PedidoFornecedor>().WithMany().HasForeignKey(m => m.PedidoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Comanda>().WithMany().HasForeignKey(m => m.ComandaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ItemComanda>().WithMany().HasForeignKey(m => m.ItemComandaId).OnDelete(DeleteBehavior.Restrict);
    }
}
