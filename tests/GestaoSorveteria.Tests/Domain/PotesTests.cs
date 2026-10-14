using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Potes com saldo pela venda (Sprint 7): produto ligado a grupo, sabor na venda, movimentos e saldo (RN-PR-06, RN-ES-08/09, RN-AV-04).</summary>
public class PotesTests
{
    private static readonly DateTime Agora = Cenario.Agora;
    private static readonly Guid Fabrica = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly GrupoEstoque _pote1L = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 5);
    private readonly GrupoEstoque _pote2L = GrupoEstoque.Criar("Pote 2L", ModoControle.SaldoPelaVenda, 6);
    private readonly ItemEstoque _flocos;
    private readonly ItemEstoque _chocolate;
    private readonly ItemEstoque _flocos2L;
    private readonly Produto _pote;

    public PotesTests()
    {
        _flocos = ItemEstoque.Criar(_pote1L.Id, Fabrica, "Flocos", UnidadeCompra.Unidade, 1, 14m, 1, minimoUnidades: 2);
        _chocolate = ItemEstoque.Criar(_pote1L.Id, Fabrica, "Chocolate", UnidadeCompra.Unidade, 1, 14m, 2);
        _flocos2L = ItemEstoque.Criar(_pote2L.Id, Fabrica, "Flocos", UnidadeCompra.Unidade, 1, 24m, 1);
        _pote = Produto.Criar("Pote 1L", "Potes", 28m, false, 4, Agora);
        _pote.LigarAoEstoque(_pote1L.Id, 1, Agora);
    }

    [Fact]
    public void LigarAoEstoque_GrupoEUnidades_GuardaEMarcaAtualizado()
    {
        var produto = Produto.Criar("2 potes de 1L", "Potes", 50m, false, 5, Agora);

        produto.LigarAoEstoque(_pote1L.Id, 2, Agora.AddMinutes(1));

        Assert.Equal(_pote1L.Id, produto.GrupoEstoqueId);
        Assert.Equal(2, produto.UnidadesPorVenda);
        Assert.Equal(Agora.AddMinutes(1), produto.AtualizadoEm);
    }

    [Fact]
    public void LigarAoEstoque_Desligar_VoltaParaUmaUnidade()
    {
        _pote.LigarAoEstoque(null, 3, Agora.AddMinutes(1));

        Assert.Null(_pote.GrupoEstoqueId);
        Assert.Equal(1, _pote.UnidadesPorVenda);
    }

    [Fact]
    public void LigarAoEstoque_SemMudanca_NaoMarcaAtualizado()
    {
        var produto = Produto.Criar("Pote 2L", "Potes", 48m, false, 6, Agora);
        produto.LigarAoEstoque(_pote2L.Id, 1, Agora.AddMinutes(1));

        produto.LigarAoEstoque(_pote2L.Id, 1, Agora.AddMinutes(5));

        Assert.Equal(Agora.AddMinutes(1), produto.AtualizadoEm);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void LigarAoEstoque_UnidadesForaDaFaixa_Lanca(int unidades)
    {
        Assert.Throws<DomainException>(() => _pote.LigarAoEstoque(_pote1L.Id, unidades, Agora));
    }

    [Fact]
    public void LigarAoEstoque_ValorLivre_LancaENaoDeixaVirarValorLivreDepois()
    {
        Assert.Throws<DomainException>(() => Cenario.SelfService().LigarAoEstoque(_pote1L.Id, 1, Agora));
        Assert.Throws<DomainException>(() => _pote.Atualizar("Pote 1L", "Potes", 0m, true, 4, Agora));
    }

    [Fact]
    public void AdicionarProduto_ComSabor_DescricaoComSaborELinhaPorSabor()
    {
        var comanda = Cenario.ComandaAberta();

        var flocos = comanda.AdicionarProduto(_pote, 1, sabor: _flocos);
        comanda.AdicionarProduto(_pote, 1, sabor: _flocos);
        var chocolate = comanda.AdicionarProduto(_pote, 1, sabor: _chocolate);

        Assert.Equal("Pote 1L · Flocos", flocos.Descricao);
        Assert.Equal(_flocos.Id, flocos.ItemEstoqueId);
        Assert.Equal(2, flocos.Quantidade); // mesmo sabor soma na linha
        Assert.NotEqual(flocos.Id, chocolate.Id); // sabor diferente é outra linha
        Assert.Equal(84m, comanda.Total);
    }

    [Fact]
    public void AdicionarProduto_SaborDeOutroGrupoOuProdutoSemGrupo_Lanca()
    {
        var comanda = Cenario.ComandaAberta();

        Assert.Throws<DomainException>(() => comanda.AdicionarProduto(_pote, 1, sabor: _flocos2L));
        Assert.Throws<DomainException>(() => comanda.AdicionarProduto(Cenario.Picole(), 1, sabor: _flocos));
    }

    [Fact]
    public void Restaurar_ItemLivreComSabor_Lanca()
    {
        var item = new DadosItem(Guid.NewGuid(), null, "Avulso", 1, 5m, _flocos.Id);

        Assert.Throws<DomainException>(() => Comanda.Restaurar(
            Guid.NewGuid(), Guid.NewGuid(), Cenario.Atendente, 1, TipoComanda.Balcao, StatusComanda.Aberta, Agora, Agora, null, [item]));
    }

    [Fact]
    public void Restaurar_ItemComSabor_GuardaOSabor()
    {
        var item = new DadosItem(Guid.NewGuid(), _pote.Id, "Pote 1L · Flocos", 1, 28m, _flocos.Id);

        var comanda = Comanda.Restaurar(
            Guid.NewGuid(), Guid.NewGuid(), Cenario.Atendente, 1, TipoComanda.Balcao, StatusComanda.Aberta, Agora, Agora, null, [item]);

        Assert.Equal(_flocos.Id, comanda.Itens.Single().ItemEstoqueId);
    }

    [Fact]
    public void EstornoVenda_DaBaixa_DevolveComOMomentoDaVenda()
    {
        var entrada = MovimentoEstoque.Entrada(_flocos.Id, 6, Guid.NewGuid(), Agora);
        var baixa = MovimentoEstoque.BaixaVenda(_flocos.Id, 2, Guid.NewGuid(), Guid.NewGuid(), Agora.AddHours(2), Agora.AddHours(5));
        var estorno = MovimentoEstoque.EstornoVenda(baixa, Agora.AddDays(1));

        Assert.NotEqual(Guid.Empty, entrada.Id);
        Assert.Equal(6, entrada.Efeito);
        Assert.Equal(-2, baixa.Efeito);
        Assert.Equal(2, estorno.Efeito);
        Assert.Equal(baixa.Em, estorno.Em);
        Assert.Equal(Agora.AddDays(1), estorno.RegistradoEm);
        Assert.Equal(baixa.ItemComandaId, estorno.ItemComandaId);
        Assert.Throws<DomainException>(() => MovimentoEstoque.EstornoVenda(entrada, Agora));
        Assert.Throws<DomainException>(() => MovimentoEstoque.Entrada(_flocos.Id, 0, Guid.NewGuid(), Agora));
    }

    [Fact]
    public void Saldo_MovimentoAntesDaContagem_NaoConta()
    {
        var contagem = new CalculoEstoque.BaseDoSaldo(5, Agora);
        var movimentos = new[]
        {
            // Venda de ontem que só sincronizou agora: já estava fora do freezer na contagem.
            MovimentoEstoque.BaixaVenda(_flocos.Id, 1, Guid.NewGuid(), Guid.NewGuid(), Agora.AddHours(-3), Agora.AddHours(1)),
            MovimentoEstoque.Entrada(_flocos.Id, 6, Guid.NewGuid(), Agora.AddDays(1)),
            MovimentoEstoque.BaixaVenda(_flocos.Id, 2, Guid.NewGuid(), Guid.NewGuid(), Agora.AddDays(2), Agora.AddDays(2)),
        };

        Assert.Equal(9, CalculoEstoque.Saldo(contagem, movimentos)); // 5 + 6 − 2
        Assert.Equal(3, CalculoEstoque.Saldo(null, movimentos)); // sem contagem: −1 + 6 − 2
    }

    [Fact]
    public void Saldo_VendaNaMesmaHoraDaContagem_JaEstaNoQueFoiContado()
    {
        var contagem = new CalculoEstoque.BaseDoSaldo(5, Agora);
        var venda = MovimentoEstoque.BaixaVenda(_flocos.Id, 1, Guid.NewGuid(), Guid.NewGuid(), Agora, Agora.AddMinutes(5));

        Assert.Equal(5, CalculoEstoque.Saldo(contagem, [venda]));
    }

    [Fact]
    public void Saldo_EstornoDeVendaDeAntesDaContagem_NaoMudaOSaldo()
    {
        // Venda lançada errada no sábado (o pote nem saiu), contado no domingo, estornada na segunda.
        var contagem = new CalculoEstoque.BaseDoSaldo(5, Agora);
        var baixa = MovimentoEstoque.BaixaVenda(_flocos.Id, 1, Guid.NewGuid(), Guid.NewGuid(), Agora.AddDays(-1), Agora.AddDays(-1));
        var estorno = MovimentoEstoque.EstornoVenda(baixa, Agora.AddDays(1));

        Assert.Equal(5, CalculoEstoque.Saldo(contagem, [baixa, estorno]));
    }

    [Fact]
    public void Remontar_ItemComSabor_GuardaOSabor()
    {
        var caixa = Cenario.CaixaAberto();
        var item = new DadosItemRecebido(Guid.NewGuid(), _pote.Id, "Pote 1L · Flocos", 1, 28m, 28m, _flocos.Id);

        var comanda = Comanda.Remontar(caixa, Guid.NewGuid(), Cenario.Atendente, 1, TipoComanda.Balcao, StatusComanda.Fechada,
            Agora.AddMinutes(1), Agora.AddMinutes(2), null, [item], 28m,
            [new DadosPagamento(FormaPagamento.Pix, 28m)], fechadaEmUtc: Agora.AddMinutes(1));

        Assert.Equal(_flocos.Id, comanda.Itens.Single().ItemEstoqueId);
    }

    [Fact]
    public void AdicionarProduto_SaborDesativado_Lanca()
    {
        _chocolate.Desativar();

        Assert.Throws<DomainException>(() => Cenario.ComandaAberta().AdicionarProduto(_pote, 1, sabor: _chocolate));
    }

    [Fact]
    public void Restaurar_SaborVazio_Lanca()
    {
        var item = new DadosItem(Guid.NewGuid(), _pote.Id, "Pote 1L", 1, 28m, Guid.Empty);

        Assert.Throws<DomainException>(() => Comanda.Restaurar(
            Guid.NewGuid(), Guid.NewGuid(), Cenario.Atendente, 1, TipoComanda.Balcao, StatusComanda.Aberta, Agora, Agora, null, [item]));
    }

    [Fact]
    public void AdicionarProduto_NomesLongos_EncurtaOProdutoEMantemOSabor()
    {
        var produto = Produto.Criar(new string('P', 80), "Potes", 28m, false, 9, Agora);
        produto.LigarAoEstoque(_pote1L.Id, 1, Agora);
        var sabor = ItemEstoque.Criar(_pote1L.Id, Fabrica, "Chocolate com " + new string('a', 60), UnidadeCompra.Unidade, 1, 14m, 3);

        var item = Cenario.ComandaAberta().AdicionarProduto(produto, 1, sabor: sabor);

        Assert.True(item.Descricao.Length <= ItemComanda.DescricaoMaxima);
        Assert.EndsWith(" · " + sabor.Nome, item.Descricao);
    }

    [Fact]
    public void Registrar_HoraDoItem_SoMudaQuandoONumeroMuda()
    {
        var contagem = Contagem.Iniciar(Cenario.Atendente, Agora, OrigemContagem.App);
        contagem.Registrar(_flocos, _pote1L, 3m, contadoEmUtc: Agora.AddMinutes(5));

        contagem.Registrar(_flocos, _pote1L, 3m, contadoEmUtc: Agora.AddMinutes(20)); // salvou de novo, mesmo número
        Assert.Equal(Agora.AddMinutes(5), contagem.Itens.Single().ContadoEm);

        contagem.Registrar(_flocos, _pote1L, 2m, contadoEmUtc: Agora.AddMinutes(30));
        Assert.Equal(Agora.AddMinutes(30), contagem.Itens.Single().ContadoEm);

        Assert.Throws<DomainException>(() => contagem.Registrar(_chocolate, _pote1L, 1m, contadoEmUtc: Agora.AddMinutes(-1)));
    }

    [Fact]
    public void AtualizarPeloSaldo_AvisoDePessoa_SoSobeAPrioridade()
    {
        var daAna = Aviso.DoItem(_flocos.Id, 3, PrioridadeAviso.Media, "restam 3", Cenario.Atendente, Agora);

        daAna.AtualizarPeloSaldo(0, PrioridadeAviso.Urgente, Agora.AddHours(1));
        daAna.AtualizarPeloSaldo(2, PrioridadeAviso.Alta, Agora.AddHours(2));

        Assert.Equal(PrioridadeAviso.Urgente, daAna.Prioridade);
        Assert.Equal(3, daAna.QuantidadeAtual);
        Assert.Equal(Agora, daAna.AtualizadoEm);

        var doSistema = Aviso.DoItem(_flocos.Id, 2, PrioridadeAviso.Alta, null, null, Agora);
        doSistema.AtualizarPeloSaldo(0, PrioridadeAviso.Urgente, Agora.AddHours(1));
        Assert.Equal(0, doSistema.QuantidadeAtual);
        Assert.Equal(Agora.AddHours(1), doSistema.AtualizadoEm);
    }

    [Theory]
    [InlineData(0, 2, PrioridadeAviso.Urgente)]
    [InlineData(-1, null, PrioridadeAviso.Urgente)]
    [InlineData(2, 2, PrioridadeAviso.Alta)]
    public void PrioridadePeloSaldo_ZerouOuNoMinimo_Avisa(int saldo, int? minimo, PrioridadeAviso esperada)
    {
        Assert.Equal(esperada, CalculoEstoque.PrioridadePeloSaldo(saldo, minimo));
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(1, null)]
    public void PrioridadePeloSaldo_AcimaDoMinimo_NaoAvisa(int saldo, int? minimo)
    {
        Assert.Null(CalculoEstoque.PrioridadePeloSaldo(saldo, minimo));
    }
}
