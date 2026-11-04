using GestaoSorveteria.Domain.Sugestoes;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GestaoSorveteria.Infrastructure.Persistence.Configurations;

internal sealed class SugestaoConfiguration : IEntityTypeConfiguration<Sugestao>
{
    public void Configure(EntityTypeBuilder<Sugestao> builder)
    {
        builder.ToTable("sugestoes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Titulo).HasMaxLength(Sugestao.TituloMaximo).IsRequired();
        builder.Property(s => s.Descricao).HasMaxLength(Sugestao.DescricaoMaxima).IsRequired();
        builder.Property(s => s.Tipo).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Area).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Importancia).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.CriadaPorUsuarioId).IsRequired();
        builder.Property(s => s.CriadaEm).IsRequired();
        builder.Property(s => s.AtualizadaEm).IsRequired();

        // Dona e Paulo escrevendo ao mesmo tempo: a segunda gravação falha com 409 e a tela recarrega.
        builder.Property<uint>("Versao").HasColumnName("xmin").HasColumnType("xid").IsRowVersion();

        builder.Ignore(s => s.Encerrada);

        builder.HasIndex(s => s.AtualizadaEm);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(s => s.CriadaPorUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Comentarios)
            .WithOne()
            .HasForeignKey(c => c.SugestaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(s => s.Comentarios)
            .HasField("_comentarios")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SugestaoComentarioConfiguration : IEntityTypeConfiguration<SugestaoComentario>
{
    public void Configure(EntityTypeBuilder<SugestaoComentario> builder)
    {
        builder.ToTable("sugestao_comentarios");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.SugestaoId).IsRequired();
        builder.Property(c => c.UsuarioId).IsRequired();
        builder.Property(c => c.Texto).HasMaxLength(Sugestao.ComentarioMaximo).IsRequired();
        builder.Property(c => c.Em).IsRequired();

        builder.HasIndex(c => new { c.SugestaoId, c.Em });

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class SugestaoImagemConfiguration : IEntityTypeConfiguration<SugestaoImagem>
{
    public void Configure(EntityTypeBuilder<SugestaoImagem> builder)
    {
        builder.ToTable("sugestao_imagens");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.SugestaoId).IsRequired();
        builder.Property(i => i.UsuarioId).IsRequired();
        builder.Property(i => i.NomeArquivo).HasMaxLength(SugestaoImagem.NomeMaximo).IsRequired();
        builder.Property(i => i.TipoConteudo).HasMaxLength(20).IsRequired();
        builder.Property(i => i.TamanhoBytes).IsRequired();
        builder.Property(i => i.Conteudo).HasColumnType("bytea").IsRequired();
        builder.Property(i => i.EnviadaEm).IsRequired();

        builder.HasIndex(i => new { i.SugestaoId, i.EnviadaEm });

        builder.HasOne<Sugestao>()
            .WithMany()
            .HasForeignKey(i => i.SugestaoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(i => i.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
