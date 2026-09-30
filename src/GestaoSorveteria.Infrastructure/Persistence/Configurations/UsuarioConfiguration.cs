using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Nome).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Login).HasMaxLength(50).IsRequired();
        builder.Property(u => u.SenhaHash).HasMaxLength(300).IsRequired();
        builder.Property(u => u.Perfil).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(u => u.Ativo).IsRequired();
        builder.Property(u => u.CriadoEm).IsRequired();

        // RN-US-02: login único (já gravado em minúsculas pela entidade).
        builder.HasIndex(u => u.Login).IsUnique();
    }
}
