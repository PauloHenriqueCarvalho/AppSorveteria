using GestaoSorveteria.Domain.Estoque;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class FornecedorConfiguration : IEntityTypeConfiguration<Fornecedor>
{
    /// <summary>Nome do campo privado de <see cref="Fornecedor"/>; consultas usam <c>EF.Property&lt;List&lt;DayOfWeek&gt;&gt;(f, CampoDiasPedido)</c>.</summary>
    public const string CampoDiasPedido = "_diasPedido";

    public void Configure(EntityTypeBuilder<Fornecedor> builder)
    {
        builder.ToTable("fornecedores");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.Nome).HasMaxLength(80).IsRequired();
        builder.Property(f => f.NomeNormalizado).HasMaxLength(80).IsRequired();
        builder.Property(f => f.WhatsApp).HasColumnName("whatsapp").HasMaxLength(13);
        builder.Property(f => f.PrazoEntregaDias).IsRequired();
        builder.Property(f => f.AceitaMeiaCaixa).IsRequired();
        builder.Property(f => f.Envio).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.MostrarPrecos).IsRequired();
        builder.Property(f => f.Ativo).IsRequired();

        // Dias de pedido (RN-PF-01): text[] com o nome do dia ("Monday"), como os outros enums. Mapeia o campo (List<DayOfWeek>),
        // que é o tipo que o EF materializa; a propriedade pública só expõe uma visão somente leitura dele.
        builder.Ignore(f => f.DiasPedido);
        builder.PrimitiveCollection<List<DayOfWeek>>(CampoDiasPedido)
            .HasField(CampoDiasPedido)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("dias_pedido")
            .IsRequired()
            .ElementType(e => e.HasConversion<string>());

        // RN-ES-11: nome único sem diferenciar maiúsculas nem a forma do acento (NomeEstoque).
        builder.HasIndex(f => f.NomeNormalizado).IsUnique();
    }
}
