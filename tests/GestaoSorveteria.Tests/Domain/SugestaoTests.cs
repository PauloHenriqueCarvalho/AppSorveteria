using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Sugestoes;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Sugestões da dona (RN-SG-01..05).</summary>
public sealed class SugestaoTests
{
    private static readonly DateTime Agora = new(2026, 11, 4, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Dona = Guid.NewGuid();
    private static readonly Guid Paulo = Guid.NewGuid();

    private static Sugestao Nova() =>
        Sugestao.Criar("  Botão de picolé maior  ", "No balcão o botão é pequeno.", TipoSugestao.Mudanca, AreaSugestao.Balcao,
            ImportanciaSugestao.Alta, Dona, Agora);

    [Fact]
    public void Criar_DadosValidos_NasceNovaComTextoLimpo()
    {
        var s = Nova();

        Assert.Equal("Botão de picolé maior", s.Titulo);
        Assert.Equal(StatusSugestao.Nova, s.Status);
        Assert.Equal(Dona, s.CriadaPorUsuarioId);
        Assert.Equal(Agora, s.AtualizadaEm);
        Assert.False(s.Encerrada);
    }

    [Theory]
    [InlineData("ab", "descrição")]
    [InlineData("Título bom", "   ")]
    public void Criar_TextoCurtoOuVazio_Lanca(string titulo, string descricao) =>
        Assert.Throws<DomainException>(() =>
            Sugestao.Criar(titulo, descricao, TipoSugestao.Ideia, AreaSugestao.Painel, ImportanciaSugestao.Baixa, Dona, Agora));

    [Fact]
    public void AlterarStatus_RegistraNaConversa()
    {
        var s = Nova();

        s.AlterarStatus(StatusSugestao.Planejada, "Entra na próxima versão.", Paulo, Agora.AddHours(1));

        Assert.Equal(StatusSugestao.Planejada, s.Status);
        var registro = Assert.Single(s.Comentarios);
        Assert.Equal(Paulo, registro.UsuarioId);
        Assert.Contains("Nova → Planejada", registro.Texto);
        Assert.Contains("Entra na próxima versão.", registro.Texto);
    }

    [Fact]
    public void AlterarStatus_RecusarSemMotivo_Lanca()
    {
        var s = Nova();

        Assert.Throws<DomainException>(() => s.AlterarStatus(StatusSugestao.Recusada, " ", Paulo, Agora));
    }

    [Fact]
    public void Editar_SugestaoFeita_LancaMasAConversaContinua()
    {
        var s = Nova();
        s.AlterarStatus(StatusSugestao.Feita, null, Paulo, Agora);

        Assert.Throws<DomainException>(() =>
            s.Editar("Outro título", "Outra", TipoSugestao.Ideia, AreaSugestao.Painel, ImportanciaSugestao.Baixa, Agora));
        s.Comentar("Ficou ótimo, obrigada!", Dona, Agora.AddMinutes(5));
        Assert.Equal(2, s.Comentarios.Count);
    }

    [Fact]
    public void AnexarImagem_TipoOuTamanhoOuQuantidadeErrados_Lanca()
    {
        var s = Nova();
        var foto = new byte[] { 1, 2, 3 };

        Assert.Throws<DomainException>(() => SugestaoImagem.Anexar(s, 0, "a.pdf", "application/pdf", foto, Dona, Agora));
        Assert.Throws<DomainException>(() =>
            SugestaoImagem.Anexar(s, 0, "a.jpg", "image/jpeg", new byte[SugestaoImagem.TamanhoMaximoBytes + 1], Dona, Agora));
        Assert.Throws<DomainException>(() => SugestaoImagem.Anexar(s, Sugestao.ImagensMaximo, "a.jpg", "image/jpeg", foto, Dona, Agora));

        var ok = SugestaoImagem.Anexar(s, 0, "C:\\fotos\\balcao.JPG", "IMAGE/JPEG", foto, Dona, Agora);
        Assert.Equal("image/jpeg", ok.TipoConteudo);
        Assert.Equal(3, ok.TamanhoBytes);
        Assert.Equal(s.Id, ok.SugestaoId);
    }
}
