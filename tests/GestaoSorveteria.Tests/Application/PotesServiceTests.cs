using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>
/// Potes com saldo pela venda (Sprint 7): baixa na sincronização (RN-ES-09), estorno, entrada na conferência (RN-RC-03),
/// aviso automático (RN-AV-04), saldos para o painel (RN-ES-08) e produto ligado ao estoque (RN-PR-06).
/// </summary>
public class PotesServiceTests
{
    // Segunda 19/10/2026, 9h em Brasília.
    private static readonly DateTime Abertura = new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Venda = Abertura.AddHours(1);

    private readonly CaixaRepositoryFake _caixas = new();
    private readonly ComandaRepositoryFake _comandas = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly ContagemRepositoryFake _contagens = new();
    private readonly AvisoRepositoryFake _avisos = new();
    private readonly MovimentoEstoqueRepositoryFake _movimentos = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Abertura.AddHours(3) };
    private readonly SaldoEstoqueService _saldos;
    private readonly SyncService _sync;

    private readonly Usuario _ana;
    private readonly Fornecedor _fabrica;
    private readonly GrupoEstoque _pote1L;
    private readonly GrupoEstoque _picoles;
    private readonly GrupoEstoque _copos;
    private readonly ItemEstoque _flocos;
    private readonly ItemEstoque _chocolate;
    private readonly ItemEstoque _limao;
    private readonly Produto _pote;
    private readonly Produto _doisPotes;
    private readonly Guid _caixaId = Guid.NewGuid();
    private int _numero;

    public PotesServiceTests()
    {
        _ana = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Abertura.AddDays(-30));
        _usuarios.Usuarios.Add(_ana);

        _fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        _pote1L = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 5);
        _picoles = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
        _copos = GrupoEstoque.Criar("Copos", ModoControle.SoAviso, 9);
        _flocos = ItemEstoque.Criar(_pote1L.Id, _fabrica.Id, "Flocos", UnidadeCompra.Unidade, 1, 14m, 1, minimoUnidades: 2);
        _chocolate = ItemEstoque.Criar(_pote1L.Id, _fabrica.Id, "Chocolate", UnidadeCompra.Unidade, 1, 14m, 2);
        _limao = ItemEstoque.Criar(_picoles.Id, _fabrica.Id, "Limão", UnidadeCompra.Caixa, 28, 25.20m, 1);
        _estoque.Fornecedores.Add(_fabrica);
        _estoque.Grupos.AddRange([_pote1L, _picoles, _copos]);
        _estoque.Itens.AddRange([_flocos, _chocolate, _limao]);

        _pote = Produto.Criar("Pote 1L", "Potes", 28m, false, 4, Abertura.AddDays(-30));
        _pote.LigarAoEstoque(_pote1L.Id, 1, Abertura.AddDays(-30));
        _doisPotes = Produto.Criar("2 potes de 1L", "Potes", 50m, false, 5, Abertura.AddDays(-30));
        _doisPotes.LigarAoEstoque(_pote1L.Id, 2, Abertura.AddDays(-30));
        _produtos.Produtos.AddRange([_pote, _doisPotes]);

        // Contagem de domingo à noite: 5 de Flocos, 1 de Chocolate.
        var contagem = Contagem.Iniciar(_ana.Id, Abertura.AddDays(-1), OrigemContagem.App);
        contagem.Registrar(_flocos, _pote1L, 5m);
        contagem.Registrar(_chocolate, _pote1L, 1m);
        contagem.Concluir(Abertura.AddDays(-1).AddMinutes(20));
        _contagens.Contagens.Add(contagem);

        _saldos = new SaldoEstoqueService(_movimentos, _contagens, _estoque, _avisos, _clock);
        _sync = new SyncService(_caixas, _comandas, _usuarios, _produtos, _estoque, _saldos, _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task AbrirCaixa() =>
        await _sync.ReceberCaixasAsync(new SyncCaixasRequest([new CaixaSyncDto(_caixaId, _ana.Id, Abertura, 100m, [])]), Ct);

    private ComandaSyncDto VendaDe(Produto produto, int quantidade, ItemEstoque? sabor, string status = "Fechada")
    {
        var total = quantidade * produto.Preco;
        var item = new ItemComandaSyncDto(
            Guid.NewGuid(), produto.Id, $"{produto.Nome} · {sabor?.Nome}", quantidade, produto.Preco, total, sabor?.Id);
        return status == "Fechada"
            ? new ComandaSyncDto(Guid.NewGuid(), _caixaId, _ana.Id, ++_numero, "Balcao", "Fechada", total, Venda, [item],
                [new PagamentoSyncDto(Guid.NewGuid(), "Pix", total, total, 0m)], FechadaEm: Venda.AddMinutes(1))
            : new ComandaSyncDto(Guid.NewGuid(), _caixaId, _ana.Id, ++_numero, "Balcao", "Cancelada", total, Venda, [item], [],
                CanceladaEm: Venda.AddMinutes(1));
    }

    private async Task<string> Enviar(ComandaSyncDto venda) =>
        Assert.Single((await _sync.ReceberComandasAsync(new SyncComandasRequest([venda]), Ct)).Resultados).Status;

    private async Task<int> SaldoDe(ItemEstoque item) => (await _saldos.SaldosAsync([item.Id], cancellationToken: Ct))[item.Id];

    [Fact]
    public async Task Sync_VendaDePoteComSabor_BaixaQuantidadeVezesUnidadesPorVenda()
    {
        await AbrirCaixa();

        Assert.Equal(StatusSync.Aceita, await Enviar(VendaDe(_pote, 1, _flocos)));
        Assert.Equal(StatusSync.Aceita, await Enviar(VendaDe(_doisPotes, 1, _flocos)));

        Assert.Equal(2, await SaldoDe(_flocos)); // 5 − 1 − 2
        var baixa = _movimentos.Movimentos.Last();
        Assert.Equal(TipoMovimentoEstoque.BaixaVenda, baixa.Tipo);
        Assert.Equal(Venda.AddMinutes(1), baixa.Em); // hora da venda, não a da sincronização
        Assert.Equal(_flocos.Id, _comandas.Comandas.Last().Itens.Single().ItemEstoqueId);
    }

    [Fact]
    public async Task Sync_PoteChegaNoMinimoEDepoisZera_AvisoAltaQueViraUrgente()
    {
        await AbrirCaixa();

        await Enviar(VendaDe(_pote, 3, _flocos)); // 5 → 2 (mínimo 2)
        var aviso = Assert.Single(_avisos.Avisos);
        Assert.Equal(PrioridadeAviso.Alta, aviso.Prioridade);
        Assert.True(aviso.CriadoPeloSistema);
        Assert.Equal(2, aviso.QuantidadeAtual);

        await Enviar(VendaDe(_pote, 2, _flocos)); // → 0
        Assert.Single(_avisos.Avisos); // o mesmo aviso
        Assert.Equal(PrioridadeAviso.Urgente, aviso.Prioridade);
        Assert.Equal(0, aviso.QuantidadeAtual);
    }

    [Fact]
    public async Task Sync_AvisoDaAtendente_SoSobeAPrioridade()
    {
        await AbrirCaixa();
        var daAna = Aviso.DoItem(_chocolate.Id, 1, PrioridadeAviso.Media, "último no freezer", _ana.Id, Abertura);
        _avisos.Avisos.Add(daAna);

        await Enviar(VendaDe(_pote, 1, _chocolate)); // 1 → 0

        Assert.Single(_avisos.Avisos);
        Assert.Equal(PrioridadeAviso.Urgente, daAna.Prioridade);
        Assert.Equal(_ana.Id, daAna.CriadoPorUsuarioId);
    }

    [Fact]
    public async Task Sync_SaborDeOutroGrupoOuInexistente_AceitaAVendaSemSaborESemBaixa()
    {
        await AbrirCaixa();

        Assert.Equal(StatusSync.Aceita, await Enviar(VendaDe(_pote, 1, _limao)));
        var inexistente = ItemEstoque.Criar(_pote1L.Id, _fabrica.Id, "Sumiu", UnidadeCompra.Unidade, 1, 1m, 9);
        Assert.Equal(StatusSync.Aceita, await Enviar(VendaDe(_pote, 1, inexistente)));

        Assert.All(_comandas.Comandas, c => Assert.Null(c.Itens.Single().ItemEstoqueId));
        Assert.Empty(_movimentos.Movimentos);
    }

    [Fact]
    public async Task Sync_ReenvioOuCancelada_NaoBaixa()
    {
        await AbrirCaixa();
        var venda = VendaDe(_pote, 1, _flocos);

        await Enviar(venda);
        Assert.Equal(StatusSync.JaRecebida, await Enviar(venda));
        Assert.Equal(StatusSync.Aceita, await Enviar(VendaDe(_pote, 1, _flocos, status: "Cancelada")));

        Assert.Single(_movimentos.Movimentos);
        Assert.Equal(4, await SaldoDe(_flocos));
    }

    [Fact]
    public async Task EstornarVenda_DevolveUmaVezSo()
    {
        await AbrirCaixa();
        await Enviar(VendaDe(_doisPotes, 1, _flocos));
        var comandaId = _comandas.Comandas.Single().Id;
        _clock.UtcNow = Abertura.AddDays(1);

        await _saldos.EstornarVendaAsync(comandaId, Ct);
        await _saldos.EstornarVendaAsync(comandaId, Ct);

        Assert.Equal(5, await SaldoDe(_flocos));
        var estorno = Assert.Single(_movimentos.Movimentos, m => m.Tipo == TipoMovimentoEstoque.EstornoVenda);
        Assert.Equal(Venda.AddMinutes(1), estorno.Em);
    }

    [Fact]
    public async Task RegistrarEntrada_PotesQueVieram_EntramNaHoraQueAEntregaChegou()
    {
        var pedido = PedidoFornecedor.Montar(_fabrica, 1, _ana.Id, Abertura.AddDays(-3),
        [
            new LinhaSugerida(_flocos, 1m, 6m, OrigemLinhaPedido.Sugestao),
            new LinhaSugerida(_chocolate, 1m, 2m, OrigemLinhaPedido.Sugestao),
            new LinhaSugerida(_limao, 1m, 1m, OrigemLinhaPedido.Sugestao),
        ]);
        pedido.Enviar(Abertura.AddDays(-3));
        pedido.Receber([new LinhaRecebida(_chocolate.Id, 0m)], [], _ana.Id, Abertura.AddHours(2), chegouEmUtc: Abertura.AddHours(1));

        await _saldos.RegistrarEntradaAsync(pedido, Ct);

        var entrada = Assert.Single(_movimentos.Movimentos); // chocolate veio 0; limão não é pote
        Assert.Equal(TipoMovimentoEstoque.Entrada, entrada.Tipo);
        Assert.Equal(Abertura.AddHours(1), entrada.Em);
        Assert.Equal(11, await SaldoDe(_flocos));
    }

    [Fact]
    public async Task Listar_SaldosDosPotesComAUltimaContagemEAsVendasDaSemana()
    {
        await AbrirCaixa();
        await Enviar(VendaDe(_doisPotes, 1, _flocos));

        var saldos = await _saldos.ListarAsync(Ct);

        Assert.Equal(["Flocos", "Chocolate"], saldos.Select(s => s.ItemNome).ToArray());
        var flocos = saldos[0];
        Assert.Equal(3, flocos.SaldoUnidades);
        Assert.Equal(5, flocos.UltimaContagemUnidades);
        Assert.Equal(2, flocos.VendidosNaSemana);
        Assert.Equal(2, flocos.MinimoUnidades);
    }

    [Fact]
    public async Task LigarAoEstoque_GrupoDePotes_PedeSaborEGrupoDeInsumosRecusa()
    {
        var produtos = new ProdutoService(_produtos, _estoque, _uow, _clock);
        var milkShake = Produto.Criar("Milk-shake", "Bebidas", 18m, false, 7, Abertura);
        _produtos.Produtos.Add(milkShake);

        var ligado = await produtos.LigarAoEstoqueAsync(milkShake.Id, new LigarProdutoEstoqueRequest(_pote1L.Id, 1), Ct);

        Assert.True(ligado.PedeSabor);
        Assert.Equal(_pote1L.Id, ligado.GrupoEstoqueId);
        await Assert.ThrowsAsync<DomainException>(() =>
            produtos.LigarAoEstoqueAsync(milkShake.Id, new LigarProdutoEstoqueRequest(_copos.Id, 1), Ct));
        var desligado = await produtos.LigarAoEstoqueAsync(milkShake.Id, new LigarProdutoEstoqueRequest(null), Ct);
        Assert.False(desligado.PedeSabor);
        Assert.Contains((await produtos.ListarAsync(null, Ct)).Produtos, p => p.Id == _pote.Id && p.PedeSabor);
    }
}
