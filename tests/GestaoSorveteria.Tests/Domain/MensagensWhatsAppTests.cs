using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Mensagens do WhatsApp (docs/10 §2.3), configuração da loja e atualização de aviso vinda do tablet.</summary>
public class MensagensWhatsAppTests
{
    private static readonly DateTime Agora = new(2026, 10, 14, 18, 42, 0, DateTimeKind.Utc);

    [Fact]
    public void Urgente_ComObservacao_SaiComoNoDocs10()
    {
        var texto = MensagensWhatsApp.Urgente("Verão Tropical", "Picolé ao leite Coco preto", 0, "Cliente pediu e não tinha.", "Ana",
            new DateTime(2026, 10, 14, 15, 42, 0));

        Assert.Equal("🔴 URGENTE — Verão Tropical\nPicolé ao leite Coco preto: ACABOU\nCliente pediu e não tinha.\nAna · 14/10 15:42", texto);
    }

    [Fact]
    public void Urgente_SemObservacaoComQuantidade_DizQuantosRestam()
    {
        var texto = MensagensWhatsApp.Urgente("Verão Tropical", "Pote 1L Flocos", 1, " ", "Ana", new DateTime(2026, 10, 14, 9, 5, 0));

        Assert.Equal("🔴 URGENTE — Verão Tropical\nPote 1L Flocos: restam 1\nAna · 14/10 09:05", texto);
    }

    [Fact]
    public void ResumoDoDia_SoComMedia_MostraSoAsSecoesQueTem()
    {
        var texto = MensagensWhatsApp.ResumoDoDia("Verão Tropical", new DateOnly(2026, 10, 18), [], [],
            [new MensagensWhatsApp.LinhaAviso("Massa Pistache", 1)], null);

        Assert.Equal("Resumo de domingo 18/10 — Verão Tropical\n🟡 Média (1): Massa Pistache — 1", texto);
    }

    [Theory]
    [InlineData("11987654321", "https://wa.me/5511987654321?text=Oi%20dona")]
    [InlineData("5511987654321", "https://wa.me/5511987654321?text=Oi%20dona")]
    [InlineData(null, "https://wa.me/?text=Oi%20dona")]
    public void Link_ComOuSemOCodigoDoPais_SempreLevaO55(string? numero, string esperado)
    {
        Assert.Equal(esperado, MensagensWhatsApp.Link(numero, "Oi dona"));
    }

    [Theory]
    [InlineData("1284.5", "R$ 1.284,50")]
    [InlineData("0", "R$ 0,00")]
    [InlineData("1234567.891", "R$ 1.234.567,89")]
    public void Reais_FormataComoNoBrasilSemDependerDaCultura(string valor, string esperado)
    {
        Assert.Equal(esperado, MensagensWhatsApp.Reais(decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ConfiguracaoLoja_WhatsAppInvalido_NaoMudaNada()
    {
        var configuracao = ConfiguracaoLoja.Padrao(Agora);

        Assert.Throws<DomainException>(() => configuracao.Atualizar("Verão Tropical", "1234", Agora));

        Assert.Equal(ConfiguracaoLoja.NomePadrao, configuracao.NomeLoja);
        Assert.Null(configuracao.WhatsAppDona);
    }

    [Fact]
    public void Aviso_ReceberAtualizacaoMaisAntiga_SobeAPrioridadeSemVoltarAQuantidade()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 1, PrioridadeAviso.Media, null, Cenario.Atendente, Agora);
        aviso.ReceberAtualizacao(0, PrioridadeAviso.Media, "acabou", Cenario.Atendente, Agora.AddMinutes(10));

        var virouUrgente = aviso.ReceberAtualizacao(5, PrioridadeAviso.Urgente, "velho", Cenario.Atendente, Agora.AddMinutes(5));

        Assert.True(virouUrgente);
        Assert.Equal(PrioridadeAviso.Urgente, aviso.Prioridade);
        Assert.Equal(0, aviso.QuantidadeAtual);
        Assert.Equal("acabou", aviso.Observacao);
        Assert.Equal(Agora.AddMinutes(10), aviso.AtualizadoEm);
    }

    [Fact]
    public void Aviso_ReceberAtualizacaoComPrioridadeMenor_MantemAMaior()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 1, PrioridadeAviso.Alta, null, Cenario.Atendente, Agora);

        var virouUrgente = aviso.ReceberAtualizacao(2, PrioridadeAviso.Media, null, Cenario.Atendente, Agora.AddMinutes(1));

        Assert.False(virouUrgente);
        Assert.Equal(PrioridadeAviso.Alta, aviso.Prioridade);
        Assert.Equal(2, aviso.QuantidadeAtual);
    }
}
