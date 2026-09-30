using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Tests.Domain;

public class ComandaTests
{
    [Fact]
    public void Abrir_ComCaixaAberto_NasceAbertaSemItens()
    {
        var caixa = Cenario.CaixaAberto();

        var comanda = Comanda.Abrir(caixa, Cenario.Atendente, 1, TipoComanda.Balcao, Cenario.Agora, Cenario.Agora, "mesa 2");

        Assert.Equal(StatusComanda.Aberta, comanda.Status);
        Assert.Equal(caixa.Id, comanda.CaixaId);
        Assert.Equal(1, comanda.Numero);
        Assert.Equal("mesa 2", comanda.Observacao);
        Assert.Empty(comanda.Itens);
        Assert.Equal(0m, comanda.Total);
        Assert.NotEqual(Guid.Empty, comanda.Id);
    }

    [Fact]
    public void Abrir_SemCaixaAberto_Lanca()
    {
        var caixa = Cenario.CaixaAberto();
        caixa.Fechar(100m, 0m, Cenario.Atendente, Cenario.Agora);

        var ex = Assert.Throws<DomainException>(() => Cenario.ComandaAberta(caixa));

        Assert.Contains("caixa aberto", ex.Message);
    }

    [Fact]
    public void Abrir_ComIdGeradoNoCliente_MantemId()
    {
        var id = Guid.NewGuid();

        var comanda = Comanda.Abrir(Cenario.CaixaAberto(), Cenario.Atendente, 7, TipoComanda.Delivery, Cenario.Agora, Cenario.Agora, id: id);

        Assert.Equal(id, comanda.Id);
        Assert.Equal(TipoComanda.Delivery, comanda.Tipo);
    }

    [Fact]
    public void Abrir_ComDataNaoUtc_Lanca()
    {
        var local = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() =>
            Comanda.Abrir(Cenario.CaixaAberto(), Cenario.Atendente, 1, TipoComanda.Balcao, local, Cenario.Agora));
    }

    [Fact]
    public void AdicionarProduto_CopiaNomeEPrecoDoMomento()
    {
        var comanda = Cenario.ComandaAberta();
        var picole = Cenario.Picole(5m);

        var item = comanda.AdicionarProduto(picole, 2);
        picole.Atualizar("Picolé de morango", "Picolés", 7m, false, 1, Cenario.Agora); // preço mudou depois

        Assert.Equal(picole.Id, item.ProdutoId);
        Assert.Equal("Picolé de morango", item.Descricao);
        Assert.Equal(5m, item.PrecoUnitario);
        Assert.Equal(10m, item.Subtotal);
        Assert.Equal(10m, comanda.Total);
    }

    [Fact]
    public void AdicionarProduto_MesmoProduto_SomaNaLinhaExistente()
    {
        var comanda = Cenario.ComandaAberta();
        var picole = Cenario.Picole(5m);

        comanda.AdicionarProduto(picole, 1);
        comanda.AdicionarProduto(picole, 2);

        var item = Assert.Single(comanda.Itens);
        Assert.Equal(3, item.Quantidade);
        Assert.Equal(15m, comanda.Total);
    }

    [Fact]
    public void AdicionarProduto_ProdutosDiferentes_SomaTotal()
    {
        var comanda = Cenario.ComandaAberta();

        comanda.AdicionarProduto(Cenario.Picole(5m), 1);
        comanda.AdicionarProduto(Cenario.MilkShake(18.90m), 2);

        Assert.Equal(2, comanda.Itens.Count);
        Assert.Equal(42.80m, comanda.Total);
    }

    [Fact]
    public void AdicionarProduto_ValorLivre_ExigeValorInformado()
    {
        var comanda = Cenario.ComandaAberta();

        var ex = Assert.Throws<DomainException>(() => comanda.AdicionarProduto(Cenario.SelfService(), 1));

        Assert.Contains("Informe o valor", ex.Message);
    }

    [Fact]
    public void AdicionarProduto_ValorLivre_UsaValorInformadoENaoSoma()
    {
        var comanda = Cenario.ComandaAberta();
        var selfService = Cenario.SelfService();

        comanda.AdicionarProduto(selfService, 1, valorInformado: 23.40m);
        comanda.AdicionarProduto(selfService, 1, valorInformado: 23.40m);

        Assert.Equal(2, comanda.Itens.Count);
        Assert.Equal(46.80m, comanda.Total);
    }

    [Fact]
    public void AdicionarProduto_SemValorLivre_NaoAceitaOutroValor()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.AdicionarProduto(Cenario.Picole(5m), 1, valorInformado: 3m));
    }

    [Fact]
    public void AdicionarProduto_Inativo_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        var picole = Cenario.Picole();
        picole.Desativar(Cenario.Agora);

        Assert.Throws<DomainException>(() => comanda.AdicionarProduto(picole, 1));
    }

    [Fact]
    public void AdicionarProduto_QuantidadeZero_Lanca()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.AdicionarProduto(Cenario.Picole(), 0));
    }

    [Fact]
    public void AdicionarProduto_ComMesmoItemId_EhIdempotente()
    {
        var comanda = Cenario.ComandaAberta();
        var itemId = Guid.NewGuid();

        var primeiro = comanda.AdicionarProduto(Cenario.Picole(5m), 1, itemId: itemId);
        var segundo = comanda.AdicionarProduto(Cenario.Picole(5m), 1, itemId: itemId);

        Assert.Same(primeiro, segundo);
        Assert.Single(comanda.Itens);
        Assert.Equal(5m, comanda.Total);
    }

    [Fact]
    public void AdicionarItemLivre_VendaAvulsa()
    {
        var comanda = Cenario.ComandaAberta();

        var item = comanda.AdicionarItemLivre(Comanda.DescricaoVendaAvulsa, 12.50m);

        Assert.True(item.EhItemLivre);
        Assert.Null(item.ProdutoId);
        Assert.Equal(12.50m, comanda.Total);
    }

    [Fact]
    public void AdicionarItemLivre_ValorZero_Lanca()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.AdicionarItemLivre("Self-service", 0m));
    }

    [Fact]
    public void RemoverItem_RecalculaTotal()
    {
        var comanda = Cenario.ComandaAberta();
        var item = comanda.AdicionarProduto(Cenario.Picole(5m), 1);
        comanda.AdicionarProduto(Cenario.MilkShake(18.90m), 1);

        comanda.RemoverItem(item.Id);

        Assert.Single(comanda.Itens);
        Assert.Equal(18.90m, comanda.Total);
    }

    [Fact]
    public void RemoverItem_Inexistente_Lanca()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.RemoverItem(Guid.NewGuid()));
    }

    [Fact]
    public void AlterarQuantidade_RecalculaTotal()
    {
        var comanda = Cenario.ComandaAberta();
        var item = comanda.AdicionarProduto(Cenario.Picole(5m), 1);

        comanda.AlterarQuantidade(item.Id, 4);

        Assert.Equal(20m, comanda.Total);
    }

    [Fact]
    public void Fechar_SemItens_Lanca()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 10m)], Cenario.Agora));
    }

    [Fact]
    public void Fechar_SemPagamentos_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 1);

        Assert.Throws<DomainException>(() => comanda.Fechar([], Cenario.Agora));
    }

    [Fact]
    public void Fechar_PagamentosNaoBatemComTotal_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 2);

        var ex = Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 9m)], Cenario.Agora));

        Assert.Contains("somam", ex.Message);
        Assert.Equal(StatusComanda.Aberta, comanda.Status);
    }

    [Fact]
    public void Fechar_EmDinheiro_CalculaTroco()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarItemLivre("Self-service", 12.50m);

        comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 12.50m, ValorRecebido: 50m)], Cenario.Agora);

        var pagamento = Assert.Single(comanda.Pagamentos);
        Assert.Equal(StatusComanda.Fechada, comanda.Status);
        Assert.Equal(Cenario.Agora, comanda.FechadaEm);
        Assert.Equal(12.50m, pagamento.Valor);
        Assert.Equal(50m, pagamento.ValorRecebido);
        Assert.Equal(37.50m, pagamento.Troco);
        Assert.Equal(12.50m, comanda.TotalEmDinheiro);
        Assert.Equal(37.50m, comanda.TotalTroco);
    }

    [Fact]
    public void Fechar_DinheiroRecebidoMenorQueValor_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 2);

        Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.Dinheiro, 10m, ValorRecebido: 5m)], Cenario.Agora));
    }

    [Fact]
    public void Fechar_PixComValorRecebidoDiferente_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 2);

        Assert.Throws<DomainException>(() =>
            comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 10m, ValorRecebido: 20m)], Cenario.Agora));
    }

    [Fact]
    public void Fechar_ComVariasFormas_SomaExata()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.MilkShake(18.90m), 1);

        comanda.Fechar(
        [
            new DadosPagamento(FormaPagamento.Pix, 10m),
            new DadosPagamento(FormaPagamento.Dinheiro, 8.90m, ValorRecebido: 10m),
        ], Cenario.Agora);

        Assert.Equal(2, comanda.Pagamentos.Count);
        Assert.Equal(8.90m, comanda.TotalEmDinheiro);
        Assert.Equal(1.10m, comanda.TotalTroco);
    }

    [Fact]
    public void Fechar_DepoisDeFechada_EhImutavel()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 1);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 5m)], Cenario.Agora);

        Assert.Throws<DomainException>(() => comanda.AdicionarProduto(Cenario.Picole(5m), 1));
        Assert.Throws<DomainException>(() => comanda.AdicionarItemLivre("x", 1m));
        Assert.Throws<DomainException>(() => comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, 5m)], Cenario.Agora));
        Assert.Throws<DomainException>(() => comanda.Cancelar(null, Cenario.Agora));
    }

    [Fact]
    public void Cancelar_Aberta_ViraCancelada()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 1);

        comanda.Cancelar("cliente desistiu", Cenario.Agora);

        Assert.Equal(StatusComanda.Cancelada, comanda.Status);
        Assert.Equal("cliente desistiu", comanda.MotivoCancelamento);
        Assert.Equal(Cenario.Agora, comanda.CanceladaEm);
        Assert.Single(comanda.Itens); // nada é apagado (RN-TD-03)
    }

    private static Comanda ComandaFechada(FormaPagamento forma = FormaPagamento.Pix)
    {
        var comanda = Cenario.ComandaAberta();
        comanda.AdicionarProduto(Cenario.Picole(5m), 1);
        comanda.Fechar([new DadosPagamento(forma, 5m)], Cenario.Agora);
        return comanda;
    }

    [Fact]
    public void Estornar_Fechada_ComMotivo_ViraEstornadaEGuardaQuemQuandoEPorque()
    {
        var comanda = ComandaFechada();

        comanda.Estornar("cobrado em duplicidade", Cenario.Dona, Cenario.Agora.AddMinutes(5));

        // RN-CM-09: status próprio, diferente da Cancelada (RN-CM-08).
        Assert.Equal(StatusComanda.Estornada, comanda.Status);
        Assert.Equal("cobrado em duplicidade", comanda.MotivoEstorno);
        Assert.Equal(Cenario.Dona, comanda.EstornadaPorUsuarioId);
        Assert.Equal(Cenario.Agora.AddMinutes(5), comanda.EstornadaEm);
        Assert.Null(comanda.CanceladaEm);
        Assert.Null(comanda.MotivoCancelamento);
        Assert.Single(comanda.Pagamentos); // nada é apagado (RN-TD-03)
        Assert.Equal(5m, comanda.Total);
        Assert.False(comanda.EstaFechada);
    }

    [Fact]
    public void Estornar_Dinheiro_ContinuaEntrandoNoCaixa()
    {
        var comanda = ComandaFechada(FormaPagamento.Dinheiro);

        comanda.Estornar("cliente devolveu", Cenario.Dona, Cenario.Agora.AddMinutes(5));

        // O estorno não mexe no caixa: a devolução é uma sangria no caixa aberto (RN-CM-09).
        Assert.True(comanda.EntraNoCaixa);
        Assert.Equal(5m, comanda.TotalEmDinheiro);
    }

    [Fact]
    public void Estornar_SemMotivo_Lanca()
    {
        Assert.Throws<DomainException>(() => ComandaFechada().Estornar(" ", Cenario.Dona, Cenario.Agora));
    }

    [Fact]
    public void Estornar_Aberta_Lanca()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.Estornar("motivo", Cenario.Dona, Cenario.Agora));
    }

    [Fact]
    public void Estornar_Cancelada_Lanca()
    {
        var comanda = Cenario.ComandaAberta();
        comanda.Cancelar(null, Cenario.Agora);

        Assert.Throws<DomainException>(() => comanda.Estornar("motivo", Cenario.Dona, Cenario.Agora));
        Assert.False(comanda.EntraNoCaixa);
    }

    [Fact]
    public void Estornar_DuasVezes_Lanca()
    {
        var comanda = ComandaFechada();
        comanda.Estornar("primeiro", Cenario.Dona, Cenario.Agora.AddMinutes(5));

        var ex = Assert.Throws<DomainException>(() => comanda.Estornar("segundo", Cenario.Dona, Cenario.Agora.AddMinutes(6)));
        Assert.Contains("já foi estornada", ex.Message);
        Assert.Equal("primeiro", comanda.MotivoEstorno);
    }

    [Fact]
    public void Estornar_AntesDoFechamento_Lanca()
    {
        Assert.Throws<DomainException>(() => ComandaFechada().Estornar("motivo", Cenario.Dona, Cenario.Agora.AddMinutes(-1)));
    }

    [Fact]
    public void Restaurar_Estornada_Lanca()
    {
        // O app nunca estorna: estorno é só pelo painel (RN-CM-09).
        Assert.Throws<DomainException>(() => Comanda.Restaurar(
            Guid.NewGuid(), Guid.NewGuid(), Cenario.Atendente, 1, TipoComanda.Balcao, StatusComanda.Estornada,
            Cenario.Agora, Cenario.Agora, null, []));
    }

    [Fact]
    public void MarcarRecebidaAposFechamentoCaixa_Marca()
    {
        var comanda = Cenario.ComandaAberta();

        comanda.MarcarRecebidaAposFechamentoCaixa();

        Assert.True(comanda.RecebidaAposFechamentoCaixa);
    }
}
