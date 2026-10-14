using GestaoSorveteria.Domain.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class ItemEstoqueConfiguration : IEntityTypeConfiguration<ItemEstoque>
{
    public void Configure(EntityTypeBuilder<ItemEstoque> builder)
    {
        builder.ToTable("itens_estoque");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.GrupoId).IsRequired();
        builder.Property(i => i.FornecedorId).IsRequired();
        builder.Property(i => i.Nome).HasMaxLength(80).IsRequired();
        builder.Property(i => i.NomeNormalizado).HasMaxLength(80).IsRequired();
        builder.Property(i => i.Unidade).HasColumnName("unidade_compra").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(i => i.UnidadesPorEmbalagem).IsRequired();
        builder.Property(i => i.CustoEmbalagem).HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.EstoqueAlvoEmbalagens).HasPrecision(6, 1); // meia caixa (RN-ES-03)
        builder.Property(i => i.MinimoUnidades);
        builder.Property(i => i.Ordem).IsRequired();
        builder.Property(i => i.Ativo).IsRequired();

        builder.Ignore(i => i.PassoEmbalagens);

        // RN-ES-01: grupo + nome único (sem diferenciar maiúsculas nem a forma do acento).
        builder.HasIndex(i => new { i.GrupoId, i.NomeNormalizado }).IsUnique();
        builder.HasIndex(i => i.FornecedorId);

        builder.HasOne<GrupoEstoque>()
            .WithMany()
            .HasForeignKey(i => i.GrupoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Fornecedor>()
            .WithMany()
            .HasForeignKey(i => i.FornecedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
