using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class PedidoFornecedorConfiguration : IEntityTypeConfiguration<PedidoFornecedor>
{
    public void Configure(EntityTypeBuilder<PedidoFornecedor> builder)
    {
        builder.ToTable("pedidos_fornecedor");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.FornecedorId).IsRequired();
        builder.Property(p => p.Numero).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(15).IsRequired();
        builder.Property(p => p.CriadoPorUsuarioId).IsRequired();
        builder.Property(p => p.CriadoEm).IsRequired();
        builder.Property(p => p.AtualizadoEm).IsRequired();
        builder.Property(p => p.EnviadoEm);
        builder.Property(p => p.RecebidoEm);
        builder.Property(p => p.RecebidoPorUsuarioId);
        builder.Property(p => p.CanceladoEm);
        builder.Property(p => p.MotivoCancelamento).HasMaxLength(200);
        builder.Property(p => p.TotalPedido).HasPrecision(12, 2).IsRequired();
        builder.Property(p => p.TotalRecebido).HasPrecision(12, 2);
        builder.Property(p => p.PercentualAtendido).HasPrecision(5, 2);

        // Montar e conferir ao mesmo tempo (duas abas do painel) não podem desfazer um ao outro: o segundo falha com 409.
        builder.Property<uint>("Versao").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.Ignore(p => p.EhRascunho);

        builder.HasIndex(p => p.Numero).IsUnique();
        // RN-PF-04: no máximo um rascunho por fornecedor.
        builder.HasIndex(p => p.FornecedorId)
            .IsUnique()
            .HasFilter("status = 'Rascunho'")
            .HasDatabaseName("ux_pedidos_fornecedor_um_rascunho");
        builder.HasIndex(p => new { p.Status, p.RecebidoEm });

        builder.HasOne<Fornecedor>().WithMany().HasForeignKey(p => p.FornecedorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(p => p.CriadoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Usuario>().WithMany().HasForeignKey(p => p.RecebidoPorUsuarioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Itens)
            .WithOne()
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Restrict); // nada é apagado

        builder.Navigation(p => p.Itens)
            .HasField("_itens")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ItemPedidoFornecedorConfiguration : IEntityTypeConfiguration<ItemPedidoFornecedor>
{
    public void Configure(EntityTypeBuilder<ItemPedidoFornecedor> builder)
    {
        builder.ToTable("itens_pedido_fornecedor");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.PedidoId).IsRequired();
        builder.Property(i => i.ItemEstoqueId).IsRequired();
        builder.Property(i => i.Passo).HasPrecision(3, 1).IsRequired();
        builder.Property(i => i.SugeridoEmbalagens).HasPrecision(6, 1).IsRequired();
        builder.Property(i => i.PedidoEmbalagens).HasPrecision(6, 1).IsRequired();
        builder.Property(i => i.RecebidoEmbalagens).HasPrecision(6, 1);
        builder.Property(i => i.CustoEmbalagem).HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.AlteradoPelaDona).IsRequired();
        builder.Property(i => i.Origem).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(i => i.Observacao).HasMaxLength(200);
        builder.Property(i => i.PedirDeNovo).IsRequired();

        builder.Ignore(i => i.Situacao);
        builder.Ignore(i => i.DiferencaValor);
        builder.Ignore(i => i.Faltou);

        builder.HasIndex(i => new { i.PedidoId, i.ItemEstoqueId }).IsUnique();
        builder.HasIndex(i => i.ItemEstoqueId);

        builder.HasOne<ItemEstoque>().WithMany().HasForeignKey(i => i.ItemEstoqueId).OnDelete(DeleteBehavior.Restrict);
    }
}
