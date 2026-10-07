using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Comanda.Restaurar: reconstrói o que o app gravou no SQLite com as mesmas regras.</summary>
public class ComandaRestaurarTests
{
    private static readonly Guid ComandaId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CaixaId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid PicoleId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static DadosItem[] Itens() =>
    [
        new(Guid.NewGuid(), PicoleId, "Picolé de morango", 3, 2.50m),
        new(Guid.NewGuid(), null, "Self-Service", 1, 15.55m),
    ];

    private static Comanda Restaurar(
        StatusComanda status,
        DadosItem[]? itens = null,
        DadosPagamento[]? pagamentos = null,
        DateTime? fechadaEm = null,
        DateTime? canceladaEm = null) =>
        Comanda.Restaurar(ComandaId, CaixaId, Cenario.Atendente, 7, TipoComanda.Balcao, status,
            Cenario.Agora, Cenario.Agora, null, itens ?? Itens(), pagamentos, fechadaEm, canceladaEm);

    [Fact]
    public void Restaurar_Aberta_RecalculaTotalEMantemIds()
    {
        var itens = Itens();

        var comanda = Restaurar(StatusComanda.Aberta, itens);

        Assert.Equal(ComandaId, comanda.Id);
        Assert.Equal(CaixaId, comanda.CaixaId);
        Assert.Equal(7, comanda.Numero);
        Assert.True(comanda.EstaAberta);
        Assert.Equal(23.05m, comanda.Total); // RN-CM-05: 3 × 2,50 + 15,55
        Assert.Equal(itens.Select(i => i.Id), comanda.Itens.Select(i => i.Id));
        Assert.Null(comanda.Itens.Last().ProdutoId); // RN-CM-03: item livre
    }

    [Fact]
    public void Restaurar_Aberta_ContinuaAceitandoAlteracoes()
    {
        var comanda = Restaurar(StatusComanda.Aberta);

        comanda.AlterarQuantidade(comanda.Itens.First().Id, 1);

        Assert.Equal(18.05m, comanda.Total);
    }

    [Fact]
    public void Restaurar_AbertaComPagamento_Lanca()
    {
        Assert.Throws<DomainException>(() =>
            Restaurar(StatusComanda.Aberta, pagamentos: [new(FormaPagamento.Pix, 23.05m)]));
    }

    [Fact]
    public void Restaurar_FechadaComPagamentosExatos_RecalculaTroco()
    {
        var comanda = Restaurar(StatusComanda.Fechada,
            pagamentos: [new(FormaPagamento.Dinheiro, 20m, 50m), new(FormaPagamento.Pix, 3.05m)],
            fechadaEm: Cenario.Agora);

        Assert.True(comanda.EstaFechada);
        Assert.Equal(30m, comanda.TotalTroco); // RN-PG-03/04
        Assert.Equal(20m, comanda.TotalEmDinheiro);
    }

    [Fact]
    public void Restaurar_FechadaComSomaDiferenteDoTotal_Lanca()
    {
        Assert.Throws<DomainException>(() => Restaurar(StatusComanda.Fechada,
            pagamentos: [new(FormaPagamento.Dinheiro, 50m, 50m)], fechadaEm: Cenario.Agora));
    }

    [Fact]
    public void Restaurar_FechadaSemDataDeFechamento_Lanca()
    {
        Assert.Throws<DomainException>(() =>
            Restaurar(StatusComanda.Fechada, pagamentos: [new(FormaPagamento.Pix, 23.05m)]));
    }

    [Fact]
    public void Restaurar_Cancelada_FicaImutavel()
    {
        var comanda = Restaurar(StatusComanda.Cancelada, canceladaEm: Cenario.Agora);

        Assert.Equal(StatusComanda.Cancelada, comanda.Status);
        Assert.Throws<DomainException>(() => comanda.AdicionarItemLivre("Venda avulsa", 5m));
    }

    [Fact]
    public void Restaurar_ItemComQuantidadeZero_Lanca()
    {
        Assert.Throws<DomainException>(() =>
            Restaurar(StatusComanda.Aberta, [new(Guid.NewGuid(), PicoleId, "Picolé", 0, 2.50m)]));
    }

    [Fact]
    public void Restaurar_ItemRepetido_Lanca()
    {
        var item = new DadosItem(Guid.NewGuid(), PicoleId, "Picolé", 1, 2.50m);

        Assert.Throws<DomainException>(() => Restaurar(StatusComanda.Aberta, [item, item]));
    }

    [Fact]
    public void Restaurar_DataNaoUtc_Lanca()
    {
        Assert.Throws<DomainException>(() =>
            Comanda.Restaurar(ComandaId, CaixaId, Cenario.Atendente, 1, TipoComanda.Balcao, StatusComanda.Aberta,
                DateTime.Now, Cenario.Agora, null, Itens()));
    }
}
