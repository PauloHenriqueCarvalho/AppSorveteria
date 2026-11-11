using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>RN-PX-01…06: cobrança Pix com valor e o txid no pagamento da venda.</summary>
public class CobrancaPixTests
{
    private static readonly Guid Id = Guid.Parse("0f8b6c2a-1d3e-4f50-9a7b-2c4d6e8f0a1b");
    private const string Copia = "00020126580014BR.GOV.BCB.PIX0136chave";

    private static CobrancaPix Ativa(decimal valor = 23.50m) =>
        CobrancaPix.Criar(Id, Cenario.Atendente, valor, Copia, new SituacaoCobrancaPix(StatusCobrancaPix.Ativa), Cenario.Agora);

    [Fact]
    public void Criar_ComDadosValidos_NasceAtivaComTxidDoId()
    {
        var cobranca = Ativa();

        Assert.Equal(StatusCobrancaPix.Ativa, cobranca.Status);
        Assert.Equal("0f8b6c2a1d3e4f509a7b2c4d6e8f0a1b", cobranca.Txid);
        Assert.True(CobrancaPix.TxidValido(cobranca.Txid));
        Assert.Equal(Cenario.Agora.AddSeconds(CobrancaPix.ExpiracaoPadraoSegundos), cobranca.ExpiraEm);
        Assert.False(cobranca.PagaComValorCerto);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Criar_ValorInvalido_Lanca(decimal valor)
    {
        Assert.Throws<DomainException>(() => Ativa(valor));
    }

    [Fact]
    public void Criar_IdVazio_Lanca()
    {
        Assert.Throws<DomainException>(() =>
            CobrancaPix.Criar(Guid.Empty, Cenario.Atendente, 10m, Copia, new SituacaoCobrancaPix(StatusCobrancaPix.Ativa), Cenario.Agora));
    }

    [Fact]
    public void AtualizarPeloBanco_Concluida_GuardaPagamento()
    {
        var cobranca = Ativa();
        var pagaEm = Cenario.Agora.AddSeconds(40);

        var mudou = cobranca.AtualizarPeloBanco(
            new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, "E00416968202611111500abcdef12345", 23.50m, pagaEm), Cenario.Agora.AddMinutes(1));

        Assert.True(mudou);
        Assert.Equal(StatusCobrancaPix.Concluida, cobranca.Status);
        Assert.Equal(pagaEm, cobranca.PagaEm);
        Assert.Equal("E00416968202611111500abcdef12345", cobranca.EndToEndId);
        Assert.True(cobranca.PagaComValorCerto);
    }

    [Fact]
    public void AtualizarPeloBanco_PagoComValorDiferente_NaoLiberaVenda()
    {
        var cobranca = Ativa();

        cobranca.AtualizarPeloBanco(new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, null, 20m), Cenario.Agora);

        Assert.Equal(StatusCobrancaPix.Concluida, cobranca.Status);
        Assert.False(cobranca.PagaComValorCerto);
    }

    [Fact]
    public void AtualizarPeloBanco_ConcluidaEhFinal()
    {
        var cobranca = Ativa();
        cobranca.AtualizarPeloBanco(new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, null, 23.50m), Cenario.Agora);

        var mudou = cobranca.AtualizarPeloBanco(new SituacaoCobrancaPix(StatusCobrancaPix.Ativa), Cenario.Agora.AddHours(1));

        Assert.False(mudou);
        Assert.Equal(StatusCobrancaPix.Concluida, cobranca.Status);
    }

    [Fact]
    public void AtualizarPeloBanco_RemovidaDepoisConcluida_FicaPaga()
    {
        var cobranca = Ativa();
        cobranca.Cancelar(Cenario.Agora);

        cobranca.AtualizarPeloBanco(new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, null, 23.50m), Cenario.Agora.AddSeconds(5));

        Assert.True(cobranca.PagaComValorCerto);
    }

    [Fact]
    public void ExpirarSePassou_DepoisDaValidadeEFolga_Expira()
    {
        var cobranca = Ativa();

        Assert.False(cobranca.ExpirarSePassou(cobranca.ExpiraEm));
        Assert.True(cobranca.ExpirarSePassou(cobranca.ExpiraEm + CobrancaPix.FolgaExpiracao + TimeSpan.FromSeconds(1)));
        Assert.Equal(StatusCobrancaPix.Expirada, cobranca.Status);
    }

    [Fact]
    public void Cancelar_Ativa_FicaRemovida()
    {
        var cobranca = Ativa();

        cobranca.Cancelar(Cenario.Agora);

        Assert.Equal(StatusCobrancaPix.Removida, cobranca.Status);
    }

    [Fact]
    public void Cancelar_Paga_Lanca()
    {
        var cobranca = Ativa();
        cobranca.AtualizarPeloBanco(new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, null, 23.50m), Cenario.Agora);

        Assert.Throws<DomainException>(() => cobranca.Cancelar(Cenario.Agora));
    }

    [Theory]
    [InlineData("0f8b6c2a1d3e4f509a7b2c4d6e8f0a1b", true)]
    [InlineData("abcdefghijklmnopqrstuvwxyz", true)]
    [InlineData("curto", false)]
    [InlineData("0f8b6c2a-1d3e-4f50-9a7b-2c4d6e8f0a1b", false)]
    [InlineData("0f8b6c2a1d3e4f509a7b2c4d6e8f0a1b0123", false)]
    [InlineData(null, false)]
    public void TxidValido_SegueOPadraoDoBancoCentral(string? txid, bool valido)
    {
        Assert.Equal(valido, CobrancaPix.TxidValido(txid));
    }

    [Fact]
    public void Fechar_PixComTxid_GuardaNoPagamento()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarItemLivre("Self-service", 23.50m);

        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 23.50m, PixTxid: Ativa().Txid)], Cenario.Agora);

        Assert.Equal(Ativa().Txid, Assert.Single(comanda.Pagamentos).PixTxid);
    }

    [Fact]
    public void Fechar_TxidEmPagamentoQueNaoEhPix_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarItemLivre("Self-service", 23.50m);

        var ex = Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.CartaoDebito, 23.50m, PixTxid: Ativa().Txid)], Cenario.Agora));

        Assert.Contains("RN-PX-05", ex.Message);
    }

    [Fact]
    public void Fechar_TxidInvalido_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarItemLivre("Self-service", 23.50m);

        Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 23.50m, PixTxid: "txid com espaço")], Cenario.Agora));
    }

    [Fact]
    public void Fechar_PixSemTxid_ContinuaValido()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarItemLivre("Self-service", 23.50m);

        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 23.50m)], Cenario.Agora);

        Assert.Null(Assert.Single(comanda.Pagamentos).PixTxid);
    }

    [Fact]
    public void AtualizarPeloBanco_ConcluidaSemValorInformado_NaoLiberaVenda()
    {
        var cobranca = Ativa();

        cobranca.AtualizarPeloBanco(new SituacaoCobrancaPix(StatusCobrancaPix.Concluida), Cenario.Agora);

        Assert.Equal(StatusCobrancaPix.Concluida, cobranca.Status);
        Assert.Null(cobranca.ValorPago);
        Assert.False(cobranca.PagaComValorCerto);
    }
}
