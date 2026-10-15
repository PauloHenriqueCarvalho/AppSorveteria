using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Avisos;
using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Comandas;
using GestaoSorveteria.Application.Contagens;
using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Application.Pedidos;
using GestaoSorveteria.Application.Produtos;
using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Application.Sync;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;
using GestaoSorveteria.Infrastructure.Persistence;
using GestaoSorveteria.Tests.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GestaoSorveteria.Tests.Integracao;

/// <summary>
/// Os casos de uso do painel e do sync contra o PostgreSQL: confere que as migrações sobem do zero e que as consultas do
/// EF Core (filtros, projeções, índices únicos) traduzem para SQL e devolvem o mesmo que os testes com fakes.
/// Cada teste usa o seu mês e os seus nomes, porque o banco é um só para a classe.
/// </summary>
public sealed class ConsultasNoPostgresTests(BancoPostgres banco) : IClassFixture<BancoPostgres>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Hora de Brasília (UTC−3) num dia de 2026.</summary>
    private static DateTime Em(int mes, int dia, int hora, int minuto = 0) =>
        new DateTime(2026, mes, dia, 0, 0, 0, DateTimeKind.Utc).AddHours(hora + 3).AddMinutes(minuto);

    /// <summary>Os serviços da aplicação montados sobre os repositórios de verdade de um escopo.</summary>
    private sealed class Servicos(IServiceProvider provedor, IClock clock)
    {
        private T R<T>() where T : notnull => provedor.GetRequiredService<T>();

        public IUnitOfWork Uow => R<IUnitOfWork>();

        public SaldoEstoqueService Saldos =>
            new(R<IMovimentoEstoqueRepository>(), R<IContagemRepository>(), R<IEstoqueRepository>(), R<IAvisoRepository>(), clock);

        public SyncService Sync =>
            new(R<ICaixaRepository>(), R<IComandaRepository>(), R<IUsuarioRepository>(), R<IProdutoRepository>(), R<IEstoqueRepository>(), Saldos, Uow, clock);

        public CaixaConsultaService Caixas => new(R<ICaixaRepository>(), R<IComandaRepository>(), R<IUsuarioRepository>(), clock);

        public ComandaConsultaService Vendas => new(R<IComandaRepository>(), R<IUsuarioRepository>(), clock);

        public RelatorioService Relatorio => new(R<IComandaRepository>(), R<IProdutoRepository>(), Caixas, clock);

        public RelatorioPeriodoService Periodo => new(R<IComandaRepository>(), R<IProdutoRepository>(), R<IEstoqueRepository>(), clock);

        public ProdutoService Produtos => new(R<IProdutoRepository>(), R<IEstoqueRepository>(), Uow, clock);

        public ContagemService Contagens =>
            new(R<IContagemRepository>(), R<IEstoqueRepository>(), R<IUsuarioRepository>(), Saldos, Uow, clock);

        public AvisoService Avisos =>
            new(R<IAvisoRepository>(), R<IEstoqueRepository>(), R<IUsuarioRepository>(), R<IConfiguracaoLojaRepository>(), R<IComandaRepository>(), Saldos, Uow, clock);

        public PedidoService Pedidos =>
            new(R<IPedidoFornecedorRepository>(), R<IEstoqueRepository>(), R<IContagemRepository>(), R<IAvisoRepository>(), R<IUsuarioRepository>(),
                R<IConfiguracaoLojaRepository>(), Saldos, Uow, clock);

        public T Repositorio<T>() where T : notnull => R<T>();
    }

    private async Task<T> NoBanco<T>(DateTime agoraUtc, Func<Servicos, Task<T>> acao)
    {
        await using var escopo = banco.NovoEscopo();
        return await acao(new Servicos(escopo.ServiceProvider, new ClockFake { UtcNow = agoraUtc }));
    }

    private Task NoBanco(DateTime agoraUtc, Func<Servicos, Task> acao) =>
        NoBanco(agoraUtc, async s =>
        {
            await acao(s);
            return true;
        });

    private static ComandaSyncDto Venda(
        Guid caixaId, Guid usuarioId, int numero, DateTime fechadaEm, ItemComandaSyncDto[] itens, params PagamentoSyncDto[] pagamentos) =>
        new(Guid.NewGuid(), caixaId, usuarioId, numero, "Balcao", "Fechada", itens.Sum(i => i.Subtotal), fechadaEm.AddMinutes(-3), itens, pagamentos,
            FechadaEm: fechadaEm);

    private static ItemComandaSyncDto Item(Produto produto, int quantidade, ItemEstoque? sabor = null) =>
        new(Guid.NewGuid(), produto.Id, produto.Nome, quantidade, produto.Preco, quantidade * produto.Preco, sabor?.Id);

    private static PagamentoSyncDto Pix(decimal valor) => new(Guid.NewGuid(), "Pix", valor, valor, 0m);

    private static PagamentoSyncDto Dinheiro(decimal valor, decimal recebido) => new(Guid.NewGuid(), "Dinheiro", valor, recebido, recebido - valor);

    private static void TudoAceito(SyncResponse resposta) =>
        Assert.All(resposta.Resultados, r => Assert.True(r.Status == StatusSync.Aceita, $"{r.Status}: {r.Motivo}"));

    private static async Task Enviar(Servicos s, params ComandaSyncDto[] vendas) =>
        TudoAceito(await s.Sync.ReceberComandasAsync(new SyncComandasRequest(vendas), Ct));

    private static async Task EnviarCaixa(Servicos s, CaixaSyncDto caixa) =>
        TudoAceito(await s.Sync.ReceberCaixasAsync(new SyncCaixasRequest([caixa]), Ct));

    [Fact]
    public async Task Migracoes_BancoNovo_AplicamTodasSemPendencia()
    {
        await using var escopo = banco.NovoEscopo();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(Ct));
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync(Ct));
    }

    [Fact]
    public async Task VendasERelatorios_FiltrosRankingECusto_NoPostgres()
    {
        // Março: terça 10/03/2026.
        var ana = Usuario.Criar("Ana Março", "ana_marco", "hash", PerfilUsuario.Atendente, Em(3, 1, 9));
        var bia = Usuario.Criar("Bia Março", "bia_marco", "hash", PerfilUsuario.Atendente, Em(3, 1, 9));
        var fabrica = Fornecedor.Criar("Fábrica Março", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var leite = GrupoEstoque.Criar("Picolé ao leite Março", ModoControle.Contagem, 1);
        var coco = ItemEstoque.Criar(leite.Id, fabrica.Id, "Coco", UnidadeCompra.Caixa, 28, 25.20m, 1);
        var picole = Produto.Criar("Picolé Março", "Picolés", 5m, false, 1, Em(3, 1, 9));
        picole.LigarAoEstoque(leite.Id, 1, Em(3, 1, 9));
        var shake = Produto.Criar("Milk-shake Março", "Bebidas", 18m, false, 2, Em(3, 1, 9));
        shake.InformarCusto(4.50m);
        await NoBanco(Em(3, 1, 10), async s =>
        {
            await s.Repositorio<IUsuarioRepository>().AdicionarAsync(ana, Ct);
            await s.Repositorio<IUsuarioRepository>().AdicionarAsync(bia, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarFornecedorAsync(fabrica, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarGrupoAsync(leite, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarItemAsync(coco, Ct);
            await s.Repositorio<IProdutoRepository>().AdicionarAsync(picole, Ct);
            await s.Repositorio<IProdutoRepository>().AdicionarAsync(shake, Ct);
            await s.Uow.SaveChangesAsync(Ct);
        });

        var caixaId = Guid.NewGuid();
        var avulsa = new ItemComandaSyncDto(Guid.NewGuid(), null, "Venda avulsa", 1, 5m, 5m);
        await NoBanco(Em(3, 10, 20), async s =>
        {
            await EnviarCaixa(s, new CaixaSyncDto(caixaId, ana.Id, Em(3, 10, 9), 100m, []));
            await Enviar(s,
                Venda(caixaId, ana.Id, 1, Em(3, 10, 10), [Item(picole, 3)], Pix(15m)),
                Venda(caixaId, bia.Id, 2, Em(3, 10, 11), [Item(shake, 1)], Dinheiro(18m, 20m)),
                Venda(caixaId, ana.Id, 3, Em(3, 10, 12), [Item(picole, 2), avulsa], Pix(7m), Dinheiro(8m, 8m)));
            // Fecha o caixa: só um aberto por vez no banco inteiro (RN-CX-01), e os outros testes abrem o deles.
            await EnviarCaixa(s, new CaixaSyncDto(caixaId, ana.Id, Em(3, 10, 9), 100m, [],
                new FechamentoCaixaSyncDto(ana.Id, Em(3, 10, 19), 126m, 26m, 126m, 0m)));
        });

        var dia = new DateOnly(2026, 3, 10);
        await NoBanco(Em(3, 10, 20), async s =>
        {
            // Filtros do painel (FiltroVendas): atendente + forma de pagamento com pagamento dividido.
            var daAnaEmDinheiro = await s.Vendas.ListarAsync(dia, dia, null, ana.Id, "Dinheiro", 1, 50, Ct);
            Assert.Equal(3, Assert.Single(daAnaEmDinheiro.Itens).Numero);
            Assert.Equal(3, (await s.Vendas.ListarAsync(dia, dia, "Fechada", null, null, 1, 50, Ct)).TotalItens);

            var resumo = await s.Relatorio.ResumoDoDiaAsync(dia, Ct);
            Assert.Equal((48m, 3), (resumo.TotalVendido, resumo.QuantidadeComandas));
            Assert.Equal(("Picolé Março", 25m), (resumo.MaisVendidos[0].Descricao, resumo.MaisVendidos[0].Total));

            var semana = await s.Periodo.ObterAsync(new DateOnly(2026, 3, 9), new DateOnly(2026, 3, 15), "semana", Ct);
            Assert.Equal(48m, semana.TotalVendido);
            Assert.Equal(48m, Assert.Single(semana.PorTrecho).Total);
            Assert.Equal(0m, semana.Anterior.TotalVendido);
            // Custo: picolé 25,20 ÷ 28 = 0,90 × 5 = 4,50; milk-shake 4,50 informado; a venda avulsa fica sem custo.
            Assert.Equal((43m, 9m, 34m, 5m),
                (semana.Margem.FaturamentoComCusto, semana.Margem.CustoEstimado, semana.Margem.MargemEstimada, semana.Margem.FaturamentoSemCusto));

            var custos = (await s.Produtos.ListarCustosAsync(Ct)).ToDictionary(c => c.ProdutoId);
            Assert.Equal((0.90m, "Estoque"), (custos[picole.Id].CustoEstimado, custos[picole.Id].Origem));
            Assert.Equal((4.50m, "Informado"), (custos[shake.Id].CustoEstimado, custos[shake.Id].Origem));
        });
    }

    [Fact]
    public async Task Potes_SaldoContagemResumoEPedido_NoPostgres()
    {
        // Abril: domingo 05/04 contagem; segunda 06/04 vendas e nova contagem à noite.
        var dona = Usuario.Criar("Dona Abril", "dona_abril", "hash", PerfilUsuario.Admin, Em(4, 1, 9));
        var carla = Usuario.Criar("Carla Abril", "carla_abril", "hash", PerfilUsuario.Atendente, Em(4, 1, 9));
        var fabrica = Fornecedor.Criar("Fábrica Abril", null, [DayOfWeek.Tuesday], 1, false, FormaEnvioPedido.Impresso, true);
        var potes = GrupoEstoque.Criar("Pote Abril", ModoControle.SaldoPelaVenda, 1);
        var flocos = ItemEstoque.Criar(potes.Id, fabrica.Id, "Flocos", UnidadeCompra.Unidade, 1, 14m, 1, minimoUnidades: 1);
        var chocolate = ItemEstoque.Criar(potes.Id, fabrica.Id, "Chocolate", UnidadeCompra.Unidade, 1, 15m, 2);
        var pote = Produto.Criar("Pote Abril", "Potes", 28m, false, 1, Em(4, 1, 9));
        pote.LigarAoEstoque(potes.Id, 1, Em(4, 1, 9));
        var primeira = Contagem.Iniciar(carla.Id, Em(4, 5, 21), OrigemContagem.App);
        primeira.Registrar(flocos, potes, 5m);
        primeira.Registrar(chocolate, potes, 3m);
        primeira.Concluir(Em(4, 5, 22));
        await NoBanco(Em(4, 5, 23), async s =>
        {
            await s.Repositorio<IUsuarioRepository>().AdicionarAsync(dona, Ct);
            await s.Repositorio<IUsuarioRepository>().AdicionarAsync(carla, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarFornecedorAsync(fabrica, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarGrupoAsync(potes, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarItemAsync(flocos, Ct);
            await s.Repositorio<IEstoqueRepository>().AdicionarItemAsync(chocolate, Ct);
            await s.Repositorio<IProdutoRepository>().AdicionarAsync(pote, Ct);
            await s.Repositorio<IContagemRepository>().AdicionarAsync(primeira, Ct);
            await s.Uow.SaveChangesAsync(Ct);
        });

        var caixaId = Guid.NewGuid();
        await NoBanco(Em(4, 6, 20), async s =>
        {
            await EnviarCaixa(s, new CaixaSyncDto(caixaId, carla.Id, Em(4, 6, 9), 50m, []));
            await Enviar(s,
                Venda(caixaId, carla.Id, 1, Em(4, 6, 10), [Item(pote, 2, flocos)], Pix(56m)),
                Venda(caixaId, carla.Id, 2, Em(4, 6, 11), [Item(pote, 1, chocolate)], Pix(28m)));
            await EnviarCaixa(s, new CaixaSyncDto(caixaId, carla.Id, Em(4, 6, 9), 50m, [],
                new FechamentoCaixaSyncDto(carla.Id, Em(4, 6, 19), 50m, 0m, 50m, 0m)));
        });

        // Contagem da noite: Flocos 1 (o sistema esperava 3), Chocolate 2 (bate).
        var segunda = Contagem.Iniciar(carla.Id, Em(4, 6, 20), OrigemContagem.App);
        segunda.Registrar(flocos, potes, 1m, contadoEmUtc: Em(4, 6, 20, 10));
        segunda.Registrar(chocolate, potes, 2m, contadoEmUtc: Em(4, 6, 20, 12));
        segunda.Concluir(Em(4, 6, 20, 30));
        await NoBanco(Em(4, 6, 21), async s =>
        {
            await s.Repositorio<IContagemRepository>().AdicionarAsync(segunda, Ct);
            await s.Uow.SaveChangesAsync(Ct);
        });

        var pedido = await NoBanco(Em(4, 7, 9), async s =>
        {
            var saldos = (await s.Saldos.ListarAsync(Ct)).Where(x => x.GrupoId == potes.Id).ToDictionary(x => x.ItemNome);
            Assert.Equal((1, 2), (saldos["Flocos"].SaldoUnidades, saldos["Flocos"].VendidosNaSemana));
            Assert.Equal(2, saldos["Chocolate"].SaldoUnidades);

            var contagem = await s.Contagens.ObterAsync(segunda.Id, Ct);
            Assert.Equal(3, contagem.Linhas.Single(l => l.ItemEstoqueId == flocos.Id).SaldoSistemaUnidades);

            var resumo = await s.Avisos.ResumoDoDiaAsync(new DateOnly(2026, 4, 7), Ct);
            Assert.Contains("Potes na contagem de segunda 06/04 (1): Pote Abril Flocos: sistema 3, contado 1", resumo.Texto);

            var periodo = await s.Periodo.ObterAsync(new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 6), null, Ct);
            Assert.Equal(new[] { ("Pote Abril Flocos", 2), ("Pote Abril Chocolate", 1) }, periodo.Sabores.Select(x => (x.Descricao, x.Unidades)));
            Assert.Equal(84m - 28m - 15m, periodo.Margem.MargemEstimada); // 84 vendidos − (2 × 14 + 15)

            return await s.Pedidos.MontarAsync(fabrica.Id, dona.Id, cancellationToken: Ct);
        });

        // O rascunho do pedido saiu das consultas de contagens, recebimentos, avisos e movimentos.
        Assert.Equal("Rascunho", pedido.Status);
        Assert.Contains(pedido.Linhas, l => l.ItemEstoqueId == flocos.Id);
    }
}
