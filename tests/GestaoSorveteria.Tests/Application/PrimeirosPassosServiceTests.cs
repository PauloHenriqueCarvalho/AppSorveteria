using GestaoSorveteria.Application.Loja;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Usuarios;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

/// <summary>RN-LJ-01: primeiros passos da loja no Início do painel.</summary>
public class PrimeirosPassosServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc);

    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ConfiguracaoLojaRepositoryFake _configuracao = new();
    private readonly ContagemRepositoryFake _contagens = new();
    private readonly ComandasEmMemoria _comandas = new();
    private readonly PrimeirosPassosService _service;

    public PrimeirosPassosServiceTests()
    {
        _service = new PrimeirosPassosService(
            _estoque, _produtos, _usuarios, _configuracao, _contagens, _comandas, new ClockFake { UtcNow = Agora });
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Obter_SistemaNovoSoComADona_NadaFeito()
    {
        _usuarios.Usuarios.Add(Usuario.Criar("Dona", "dona", "hash", PerfilUsuario.Admin, Agora.AddDays(-1)));

        var passos = await _service.ObterAsync(Ct);

        Assert.Equal((false, false, false, false, false, false),
            (passos.CatalogoDeEstoque, passos.Produtos, passos.Atendentes, passos.WhatsAppDona, passos.ContagemConcluida, passos.VendaDoTablet));
    }

    [Fact]
    public async Task Obter_LojaPronta_TudoFeito()
    {
        var ana = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Agora.AddDays(-5));
        _usuarios.Usuarios.Add(ana);
        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var grupo = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
        var limao = ItemEstoque.Criar(grupo.Id, fabrica.Id, "Limão", UnidadeCompra.Caixa, 28, 25.20m, 1);
        _estoque.Fornecedores.Add(fabrica);
        _estoque.Grupos.Add(grupo);
        _estoque.Itens.Add(limao);
        var picole = Produto.Criar("Picolé", "Picolés", 5m, false, 1, Agora.AddDays(-5));
        _produtos.Produtos.Add(picole);
        var configuracao = ConfiguracaoLoja.Padrao(Agora.AddDays(-5));
        configuracao.Atualizar("Verão Tropical", "(11) 98765-4321", Agora.AddDays(-5));
        _configuracao.Configuracao = configuracao;
        var contagem = Contagem.Iniciar(ana.Id, Agora.AddDays(-2), OrigemContagem.App);
        contagem.Registrar(limao, grupo, 2.5m);
        contagem.Concluir(Agora.AddDays(-2).AddMinutes(30));
        _contagens.Contagens.Add(contagem);
        var caixa = Caixa.Abrir(ana.Id, 100m, Agora.AddHours(-3));
        var venda = Comanda.Abrir(caixa, ana.Id, 1, TipoComanda.Balcao, Agora.AddHours(-2), Agora.AddHours(-2));
        venda.AdicionarProduto(picole, 2);
        venda.Fechar([new DadosPagamento(FormaPagamento.Pix, 10m)], Agora.AddHours(-2).AddMinutes(1));
        _comandas.Comandas.Add(venda);

        var passos = await _service.ObterAsync(Ct);

        Assert.Equal((true, true, true, true, true, true),
            (passos.CatalogoDeEstoque, passos.Produtos, passos.Atendentes, passos.WhatsAppDona, passos.ContagemConcluida, passos.VendaDoTablet));
    }

    [Fact]
    public async Task Obter_ContagemSoEmRascunhoEAtendenteDesativada_NaoContam()
    {
        var ana = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Agora.AddDays(-5));
        ana.Desativar();
        _usuarios.Usuarios.Add(ana);
        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var grupo = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
        var limao = ItemEstoque.Criar(grupo.Id, fabrica.Id, "Limão", UnidadeCompra.Caixa, 28, 25.20m, 1);
        var rascunho = Contagem.Iniciar(ana.Id, Agora.AddDays(-2), OrigemContagem.Painel);
        rascunho.Registrar(limao, grupo, 1m);
        _contagens.Contagens.Add(rascunho);

        var passos = await _service.ObterAsync(Ct);

        Assert.False(passos.Atendentes);
        Assert.False(passos.ContagemConcluida);
    }
}
