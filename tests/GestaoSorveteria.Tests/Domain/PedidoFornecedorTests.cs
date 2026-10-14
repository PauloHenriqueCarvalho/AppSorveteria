using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Pedido ao fornecedor (RN-PF-04…08) e conferência da entrega (RN-RC-01…03).</summary>
public class PedidoFornecedorTests
{
    private static readonly DateTime Agora = Cenario.Agora;

    private readonly Fornecedor _fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
    private readonly GrupoEstoque _picoles = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
    private readonly ItemEstoque _limao;
    private readonly ItemEstoque _uva;
    private readonly ItemEstoque _flocos;

    public PedidoFornecedorTests()
    {
        _limao = ItemEstoque.Criar(_picoles.Id, _fabrica.Id, "Limão", UnidadeCompra.Caixa, 28, 25.20m, 1);
        _uva = ItemEstoque.Criar(_picoles.Id, _fabrica.Id, "Uva", UnidadeCompra.Caixa, 28, 25.20m, 2);
        _flocos = ItemEstoque.Criar(_picoles.Id, _fabrica.Id, "Flocos", UnidadeCompra.Balde, 1, 90m, 3);
    }

    private PedidoFornecedor Rascunho(params LinhaSugerida[] linhas) =>
        PedidoFornecedor.Montar(_fabrica, 1, Cenario.Dona, Agora, linhas);

    [Fact]
    public void Montar_ComSugestao_TotalEhPedidoVezesCustoComDuasCasas()
    {
        var pedido = Rascunho(
            new LinhaSugerida(_limao, 1m, 3m, OrigemLinhaPedido.Sugestao),
            new LinhaSugerida(_flocos, 1m, 2m, OrigemLinhaPedido.Aviso, "Ana avisou: acabou"));

        Assert.Equal(StatusPedido.Rascunho, pedido.Status);
        Assert.Equal(255.60m, pedido.TotalPedido); // 3 × 25,20 + 2 × 90
        Assert.Equal("Ana avisou: acabou", pedido.Linha(_flocos.Id)!.Observacao);
    }

    [Fact]
    public void Montar_ItemDeOutroFornecedor_Lanca()
    {
        var outro = Fornecedor.Criar("Descartáveis", null, [DayOfWeek.Thursday], 1, false, FormaEnvioPedido.Impresso, true);
        var copo = ItemEstoque.Criar(_picoles.Id, outro.Id, "Copo", UnidadeCompra.Pacote, 50, 12m, 1);

        Assert.Throws<DomainException>(() => Rascunho(new LinhaSugerida(copo, 1m, 1m, OrigemLinhaPedido.Aviso)));
    }

    [Theory]
    [InlineData(2.5, 1.0)] // fábrica sem meia caixa
    [InlineData(-1.0, 1.0)]
    public void Montar_QuantidadeForaDoPasso_Lanca(double sugerido, double passo)
    {
        Assert.Throws<DomainException>(() => Rascunho(new LinhaSugerida(_limao, (decimal)passo, (decimal)sugerido, OrigemLinhaPedido.Sugestao)));
    }

    [Fact]
    public void Montar_MeiaEmbalagemDeBalde_Lanca()
    {
        Assert.Throws<DomainException>(() => Rascunho(new LinhaSugerida(_flocos, 0.5m, 1m, OrigemLinhaPedido.Sugestao)));
    }

    [Fact]
    public void Remontar_MantemOQueADonaAlterouEAtualizaOResto()
    {
        var pedido = Rascunho(
            new LinhaSugerida(_limao, 1m, 3m, OrigemLinhaPedido.Sugestao),
            new LinhaSugerida(_uva, 1m, 2m, OrigemLinhaPedido.Sugestao));
        pedido.AlterarQuantidade(_limao.Id, 5m, Agora.AddMinutes(1));

        pedido.Remontar([new LinhaSugerida(_limao, 1m, 1m, OrigemLinhaPedido.Sugestao), new LinhaSugerida(_flocos, 1m, 1m, OrigemLinhaPedido.Aviso)],
            Agora.AddMinutes(2));

        Assert.Equal(5m, pedido.Linha(_limao.Id)!.PedidoEmbalagens); // a dona mandou 5: fica 5
        Assert.Equal(1m, pedido.Linha(_limao.Id)!.SugeridoEmbalagens);
        Assert.Equal(0m, pedido.Linha(_uva.Id)!.PedidoEmbalagens); // saiu da sugestão: fica 0, não some
        Assert.Equal(1m, pedido.Linha(_flocos.Id)!.PedidoEmbalagens);
        Assert.Equal(3, pedido.Itens.Count);
    }

    [Fact]
    public void Enviar_SemNenhumaLinhaComQuantidade_Lanca()
    {
        var pedido = Rascunho(new LinhaSugerida(_limao, 1m, 0m, OrigemLinhaPedido.Sugestao));

        Assert.Throws<DomainException>(() => pedido.Enviar(Agora));
    }

    [Fact]
    public void Enviar_DepoisNaoMudaMais()
    {
        var pedido = Rascunho(new LinhaSugerida(_limao, 1m, 2m, OrigemLinhaPedido.Sugestao));
        pedido.Enviar(Agora.AddMinutes(5));

        Assert.Equal(StatusPedido.Enviado, pedido.Status);
        Assert.Throws<DomainException>(() => pedido.AlterarQuantidade(_limao.Id, 3m, Agora.AddMinutes(6)));
        Assert.Throws<DomainException>(() => pedido.AdicionarItem(_uva, 1m, 1m, Agora.AddMinutes(6)));
    }

    [Fact]
    public void AdicionarItem_JaNoPedido_Lanca()
    {
        var pedido = Rascunho(new LinhaSugerida(_limao, 1m, 2m, OrigemLinhaPedido.Sugestao));

        Assert.Throws<DomainException>(() => pedido.AdicionarItem(_limao, 1m, 1m, Agora));
        pedido.AdicionarItem(_uva, 1m, 1m, Agora);
        Assert.Equal(OrigemLinhaPedido.AdicionadoPelaDona, pedido.Linha(_uva.Id)!.Origem);
    }

    [Fact]
    public void Cancelar_ExigeMotivoERecebidoNaoCancela()
    {
        var pedido = Rascunho(new LinhaSugerida(_limao, 1m, 2m, OrigemLinhaPedido.Sugestao));
        Assert.Throws<DomainException>(() => pedido.Cancelar("", Agora));

        pedido.Enviar(Agora);
        pedido.Receber([], [], Cenario.Dona, Agora.AddDays(2));

        Assert.Throws<DomainException>(() => pedido.Cancelar("Errei", Agora.AddDays(2)));
    }

    [Fact]
    public void Receber_ChegouTudoCerto_RecebidoIgualAoPedidoE100PorCento()
    {
        var pedido = Rascunho(
            new LinhaSugerida(_limao, 1m, 3m, OrigemLinhaPedido.Sugestao),
            new LinhaSugerida(_uva, 1m, 0m, OrigemLinhaPedido.Sugestao));
        pedido.Enviar(Agora);

        pedido.Receber([], [], Cenario.Dona, Agora.AddDays(2));

        Assert.Equal(StatusPedido.Recebido, pedido.Status);
        Assert.Equal(3m, pedido.Linha(_limao.Id)!.RecebidoEmbalagens);
        Assert.Equal(SituacaoRecebimento.Certo, pedido.Linha(_limao.Id)!.Situacao);
        Assert.Equal(75.60m, pedido.TotalRecebido);
        Assert.Equal(100m, pedido.PercentualAtendido);
    }

    [Fact]
    public void Receber_FaltouVeioAMaisENaoPedido_ClassificaECalculaDiferencaEPercentual()
    {
        var pedido = Rascunho(
            new LinhaSugerida(_limao, 1m, 4m, OrigemLinhaPedido.Sugestao),
            new LinhaSugerida(_uva, 1m, 2m, OrigemLinhaPedido.Sugestao));
        pedido.Enviar(Agora);

        pedido.Receber(
            [new LinhaRecebida(_limao.Id, 3m, PedirDeNovo: false), new LinhaRecebida(_uva.Id, 3m)],
            [new LinhaNaoPedida(_flocos, 1m, 1m)],
            Cenario.Dona, Agora.AddDays(2));

        var limao = pedido.Linha(_limao.Id)!;
        Assert.Equal(SituacaoRecebimento.Faltou, limao.Situacao);
        Assert.Equal(-25.20m, limao.DiferencaValor);
        Assert.Equal(1m, limao.Faltou);
        Assert.False(limao.PedirDeNovo);
        Assert.Equal(SituacaoRecebimento.VeioAMais, pedido.Linha(_uva.Id)!.Situacao);
        Assert.Equal(SituacaoRecebimento.NaoPedido, pedido.Linha(_flocos.Id)!.Situacao);
        Assert.Equal(90m, pedido.Linha(_flocos.Id)!.DiferencaValor);
        // Atendido = min(3,4) + min(3,2) = 5 de 6 pedidos.
        Assert.Equal(83.33m, pedido.PercentualAtendido);
        Assert.Equal(241.20m, pedido.TotalRecebido); // 3×25,20 + 3×25,20 + 1×90
        Assert.Equal(151.20m, pedido.TotalPedido);
    }

    [Fact]
    public void Receber_RascunhoOuItemRepetido_Lanca()
    {
        var pedido = Rascunho(new LinhaSugerida(_limao, 1m, 2m, OrigemLinhaPedido.Sugestao));
        Assert.Throws<DomainException>(() => pedido.Receber([], [], Cenario.Dona, Agora));

        pedido.Enviar(Agora);
        Assert.Throws<DomainException>(() =>
            pedido.Receber([new LinhaRecebida(_limao.Id, 1m), new LinhaRecebida(_limao.Id, 2m)], [], Cenario.Dona, Agora));
        Assert.Throws<DomainException>(() =>
            pedido.Receber([], [new LinhaNaoPedida(_limao, 1m, 1m)], Cenario.Dona, Agora)); // está no pedido: corrija a linha
    }
}
