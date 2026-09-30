using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Produtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class ItemComandaConfiguration : IEntityTypeConfiguration<ItemComanda>
{
    public void Configure(EntityTypeBuilder<ItemComanda> builder)
    {
        builder.ToTable("itens_comanda");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ComandaId).IsRequired();
        builder.Property(i => i.ProdutoId); // nulo = item livre (RN-CM-03)
        builder.Property(i => i.Descricao).HasMaxLength(120).IsRequired();
        builder.Property(i => i.Quantidade).IsRequired();
        builder.Property(i => i.PrecoUnitario).HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.Subtotal).HasPrecision(12, 2).IsRequired();

        builder.Ignore(i => i.EhItemLivre);

        builder.HasOne<Produto>()
            .WithMany()
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
