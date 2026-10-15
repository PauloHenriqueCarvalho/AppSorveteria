using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Fase 2 — catálogo, contagem e avisos (docs/02 seção 6, docs/10).</summary>
public class EstoqueTests
{
    private static readonly DateTime Agora = Cenario.Agora;

    private static Fornecedor Fabrica() =>
        Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);

    private static GrupoEstoque Picoles() => GrupoEstoque.Criar("Picolé ao leite", ModoControle.Contagem, 1);

    private static ItemEstoque Picole(GrupoEstoque grupo, Fornecedor fornecedor) =>
        ItemEstoque.Criar(grupo.Id, fornecedor.Id, "Chocolate", UnidadeCompra.Caixa, 28, 25.20m, 1);

    // ------------------------------------------------------------ fornecedor (RN-PF-01)

    [Fact]
    public void Fornecedor_WhatsAppComMascara_GuardaSoDigitos()
    {
        var fornecedor = Fornecedor.Criar("  Descartáveis Silva ", "(11) 98765-4321", [DayOfWeek.Thursday, DayOfWeek.Monday, DayOfWeek.Monday],
            1, false, FormaEnvioPedido.WhatsApp, false);

        Assert.Equal("Descartáveis Silva", fornecedor.Nome);
        Assert.Equal("11987654321", fornecedor.WhatsApp);
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Thursday }, fornecedor.DiasPedido);
        Assert.True(fornecedor.PedeEm(DayOfWeek.Thursday));
        Assert.False(fornecedor.PedeEm(DayOfWeek.Friday));
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("11 98765 4321 999")]
    public void Fornecedor_WhatsAppInvalido_Lanca(string numero)
    {
        Assert.Throws<DomainException>(() => Fornecedor.Criar("Fábrica", numero, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true));
    }

    [Fact]
    public void Fornecedor_EnvioPorWhatsAppSemNumero_Lanca()
    {
        var erro = Assert.Throws<DomainException>(() => Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Ambos, true));
        Assert.Contains("WhatsApp", erro.Message);
    }

    [Fact]
    public void Fornecedor_SemDiaDePedidoOuPrazoForaDaFaixa_Lanca()
    {
        Assert.Throws<DomainException>(() => Fornecedor.Criar("Fábrica", null, [], 2, false, FormaEnvioPedido.Impresso, true));
        Assert.Throws<DomainException>(() => Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], -1, false, FormaEnvioPedido.Impresso, true));
        Assert.Throws<DomainException>(() => Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 31, false, FormaEnvioPedido.Impresso, true));
    }

    // ------------------------------------------------------------ item de estoque (RN-ES-01/03)

    [Fact]
    public void ItemEstoque_MeiaCaixa_ViraUnidadesEVolta()
    {
        var item = Picole(Picoles(), Fabrica());

        Assert.Equal(70, item.ParaUnidades(2.5m)); // 2,5 × 28
        Assert.Equal(0, item.ParaUnidades(0m));
        Assert.Equal(2.5m, item.EmEmbalagens(70));
        Assert.Equal(0.5m, item.PassoEmbalagens);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(-1)]
    public void ItemEstoque_CaixaForaDoPassoDeMeia_Lanca(double caixas)
    {
        var item = Picole(Picoles(), Fabrica());
        Assert.Throws<DomainException>(() => item.ParaUnidades((decimal)caixas));
    }

    [Fact]
    public void ItemEstoque_MeiaCaixaComUnidadesImpares_Lanca()
    {
        var item = ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), "Copo", UnidadeCompra.Caixa, 25, 10m, 1);
        Assert.Equal(25, item.ParaUnidades(1m));
        Assert.Throws<DomainException>(() => item.ParaUnidades(0.5m)); // 12,5 copos
    }

    [Fact]
    public void ItemEstoque_BaldeSoInteiro()
    {
        var balde = ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), "Pistache", UnidadeCompra.Balde, 1, 90m, 1);
        Assert.Equal(2, balde.ParaUnidades(2m));
        Assert.Throws<DomainException>(() => balde.ParaUnidades(1.5m));
    }

    [Fact]
    public void ItemEstoque_RegrasDeCadastro()
    {
        // Só caixa e pacote têm mais de uma unidade por embalagem; custo com 2 casas; mínimo ≥ 0.
        Assert.Throws<DomainException>(() => ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), "Pistache", UnidadeCompra.Balde, 2, 90m, 1));
        Assert.Throws<DomainException>(() => ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), "Chocolate", UnidadeCompra.Caixa, 28, 25.205m, 1));
        Assert.Throws<DomainException>(() => ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), "Morango", UnidadeCompra.Unidade, 1, 9m, 1, minimoUnidades: -1));
        Assert.Throws<DomainException>(() => ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), "Chocolate", UnidadeCompra.Caixa, 28, 25.20m, 1, estoqueAlvoEmbalagens: 2.3m));

        var pote = ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), " Morango ", UnidadeCompra.Unidade, 1, 9m, 3, estoqueAlvoEmbalagens: 8m, minimoUnidades: 2);
        Assert.Equal(("Morango", "morango", 8m, 2), (pote.Nome, pote.NomeNormalizado, pote.EstoqueAlvoEmbalagens!.Value, pote.MinimoUnidades!.Value));
    }

    [Fact]
    public void ItemEstoque_AtualizarComAlvoQueNaoServeParaANovaUnidade_NaoMudaNada()
    {
        var fabrica = Fabrica();
        var item = ItemEstoque.Criar(Picoles().Id, fabrica.Id, "Skimo", UnidadeCompra.Caixa, 24, 30m, 1, estoqueAlvoEmbalagens: 2.5m);

        // 2,5 caixas viraria 2,5 unidades: o erro não pode deixar unidade, custo e fornecedor já trocados.
        Assert.Throws<DomainException>(() =>
            item.Atualizar(Guid.NewGuid(), "Skimo novo", UnidadeCompra.Unidade, 1, 2.50m, 9, 2.5m, null));

        Assert.Equal("Skimo", item.Nome);
        Assert.Equal(UnidadeCompra.Caixa, item.Unidade);
        Assert.Equal(24, item.UnidadesPorEmbalagem);
        Assert.Equal(30m, item.CustoEmbalagem);
        Assert.Equal(fabrica.Id, item.FornecedorId);
        Assert.Equal(1, item.Ordem);
    }

    [Fact]
    public void Fornecedor_AtualizarComErro_NaoMudaNada()
    {
        var fabrica = Fabrica();

        Assert.Throws<DomainException>(() =>
            fabrica.Atualizar("Fábrica dos pais", null, [DayOfWeek.Friday], 3, true, FormaEnvioPedido.WhatsApp, false));

        Assert.Equal("Fábrica", fabrica.Nome);
        Assert.Equal(new[] { DayOfWeek.Monday }, fabrica.DiasPedido);
        Assert.Equal(2, fabrica.PrazoEntregaDias);
    }

    [Fact]
    public void NomeEstoque_AcentoDecompostoEMaiusculas_ViraAMesmaChave()
    {
        var composto = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
        var decomposto = GrupoEstoque.Criar(" PICOLE\u0301 DE FRUTA ", ModoControle.Contagem, 2);

        Assert.Equal(composto.NomeNormalizado, decomposto.NomeNormalizado);
        Assert.Equal("PICOLÉ DE FRUTA", decomposto.Nome); // exibido na forma composta, sem espaços nas pontas
    }

    // ------------------------------------------------------------ contagem (RN-ES-05/06/07/08)

    [Fact]
    public void Contagem_RegistrarDuasVezes_CorrigeAMesmaLinha()
    {
        var grupo = Picoles();
        var item = Picole(grupo, Fabrica());
        var contagem = Contagem.Iniciar(Cenario.Atendente, Agora, OrigemContagem.App);

        contagem.Registrar(item, grupo, 2m);
        contagem.Registrar(item, grupo, 1.5m);

        Assert.Equal(42, Assert.Single(contagem.Itens).ContadoUnidades); // 1,5 × 28
    }

    [Fact]
    public void Contagem_NaoTemNenhumGravaZeroENuloVoltaANaoContado()
    {
        var grupo = Picoles();
        var item = Picole(grupo, Fabrica());
        var contagem = Contagem.Iniciar(Cenario.Atendente, Agora, OrigemContagem.App);

        contagem.Registrar(item, grupo, 0m);
        Assert.Equal(0, contagem.Itens.Single().ContadoUnidades);

        contagem.Registrar(item, grupo, null);
        Assert.Null(contagem.Itens.Single().ContadoUnidades); // RN-ES-06: não contado ≠ 0
    }

    [Fact]
    public void Contagem_GrupoSoAvisoOuItemDeOutroGrupo_Lanca()
    {
        var fornecedor = Fabrica();
        var insumos = GrupoEstoque.Criar("Descartáveis", ModoControle.SoAviso, 9);
        var copo = ItemEstoque.Criar(insumos.Id, fornecedor.Id, "Copo 300 ml", UnidadeCompra.Pacote, 50, 12m, 1);
        var picoles = Picoles();
        var contagem = Contagem.Iniciar(Cenario.Atendente, Agora, OrigemContagem.Painel);

        Assert.Throws<DomainException>(() => contagem.Registrar(copo, insumos, 1m));
        Assert.Throws<DomainException>(() => contagem.Registrar(copo, picoles, 1m));
    }

    [Fact]
    public void Contagem_ConcluirSemNadaContado_Lanca()
    {
        var grupo = Picoles();
        var contagem = Contagem.Iniciar(Cenario.Atendente, Agora, OrigemContagem.App);
        contagem.Registrar(Picole(grupo, Fabrica()), grupo, null);

        Assert.Throws<DomainException>(() => contagem.Concluir(Agora.AddMinutes(10)));
        Assert.False(contagem.Concluida);
    }

    [Fact]
    public void Contagem_Concluida_NaoMudaMais()
    {
        var grupo = Picoles();
        var item = Picole(grupo, Fabrica());
        var contagem = Contagem.Iniciar(Cenario.Atendente, Agora, OrigemContagem.App);
        contagem.Registrar(item, grupo, 2m);

        contagem.Concluir(Agora.AddMinutes(10));

        Assert.True(contagem.Concluida);
        Assert.Throws<DomainException>(() => contagem.Registrar(item, grupo, 3m));
        Assert.Throws<DomainException>(() => contagem.Concluir(Agora.AddMinutes(20)));
    }

    [Fact]
    public void Contagem_Pote_MostraDiferencaComOSaldoDoSistema()
    {
        var potes = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 6);
        var morango = ItemEstoque.Criar(potes.Id, Guid.NewGuid(), "Morango", UnidadeCompra.Unidade, 1, 9m, 1);
        var contagem = Contagem.Iniciar(Cenario.Dona, Agora, OrigemContagem.Painel);

        var linha = contagem.Registrar(morango, potes, 3m, saldoSistemaUnidades: 6);

        Assert.Equal(-3, linha.Diferenca); // RN-ES-08: faltam 3 em relação ao sistema
    }

    [Theory]
    [InlineData(70, 0, 42, 28)] // 2,5 cx → 1,5 cx: saíram 28 picolés
    [InlineData(14, 56, 42, 28)] // chegaram 2 caixas no meio da semana
    [InlineData(10, 0, 10, 0)]
    public void SaidaDaSemana_ContagemAnteriorMaisRecebidoMenosAtual(int anterior, int recebido, int atual, int esperado)
    {
        Assert.Equal(esperado, CalculoEstoque.SaidaDaSemana(anterior, recebido, atual));
    }

    [Fact]
    public void SaidaDaSemana_Negativa_FicaParaConferir()
    {
        Assert.Null(CalculoEstoque.SaidaDaSemana(28, 0, 42)); // RN-ES-07: contou mais do que tinha
    }

    // ------------------------------------------------------------ avisos (RN-AV-01/03/05/08)

    [Fact]
    public void Aviso_AindaFalta_SobePrioridadeEAvisaQueVirouUrgente()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 3, PrioridadeAviso.Media, null, Cenario.Atendente, Agora);

        var virouUrgente = aviso.AindaFalta(0, PrioridadeAviso.Urgente, "cliente pediu", Cenario.Atendente, Agora.AddHours(1));

        Assert.True(virouUrgente);
        Assert.Equal((0, PrioridadeAviso.Urgente, "cliente pediu"), (aviso.QuantidadeAtual, aviso.Prioridade, aviso.Observacao));
        Assert.Equal(Agora, aviso.CriadoEm);
        Assert.Equal(Agora.AddHours(1), aviso.AtualizadoEm);
    }

    [Fact]
    public void Aviso_AindaFaltaMantendoUrgente_NaoAvisaDeNovo()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 0, PrioridadeAviso.Urgente, "acabou", Cenario.Atendente, Agora);

        Assert.False(aviso.AindaFalta(0, PrioridadeAviso.Urgente, null, Cenario.Atendente, Agora.AddHours(1)));
        Assert.Equal("acabou", aviso.Observacao); // sem observação nova, mantém a anterior
    }

    [Fact]
    public void Aviso_AindaFaltaBaixandoPrioridade_Lanca()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 1, PrioridadeAviso.Alta, null, Cenario.Atendente, Agora);
        Assert.Throws<DomainException>(() => aviso.AindaFalta(1, PrioridadeAviso.Media, null, Cenario.Atendente, Agora));
    }

    [Fact]
    public void Aviso_FecharPelaDona_ExigeMotivoENaoAceitaAindaFaltaDepois()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 1, PrioridadeAviso.Alta, null, Cenario.Atendente, Agora);

        Assert.Throws<DomainException>(() => aviso.Fechar("ok", Cenario.Dona, Agora));
        aviso.Fechar("Comprei no mercado", Cenario.Dona, Agora.AddHours(2));

        Assert.False(aviso.Aberto);
        Assert.Equal(("Comprei no mercado", Cenario.Dona), (aviso.MotivoFechamento, aviso.FechadoPorUsuarioId!.Value));
        Assert.Throws<DomainException>(() => aviso.AindaFalta(0, PrioridadeAviso.Urgente, null, Cenario.Atendente, Agora.AddHours(3)));
    }

    [Fact]
    public void Aviso_FecharPelaEntrega_EhIdempotente()
    {
        var pedido = Guid.NewGuid();
        var aviso = Aviso.DoItem(Guid.NewGuid(), 0, PrioridadeAviso.Urgente, null, null, Agora); // criado pelo sistema

        aviso.FecharPelaEntrega(pedido, Agora.AddDays(2));
        aviso.FecharPelaEntrega(Guid.NewGuid(), Agora.AddDays(3));

        Assert.True(aviso.CriadoPeloSistema);
        Assert.Equal((pedido, Agora.AddDays(2)), (aviso.FechadoPeloPedidoId!.Value, aviso.FechadoEm!.Value));
    }

    [Fact]
    public void Aviso_TextoLivre_ExigeTextoEQuemAvisou()
    {
        Assert.Throws<DomainException>(() => Aviso.DeTextoLivre(" ", 0, PrioridadeAviso.Media, null, Cenario.Atendente, Agora));
        Assert.Throws<DomainException>(() => Aviso.DeTextoLivre("Tampa do pote 2L", 0, PrioridadeAviso.Media, null, Guid.Empty, Agora));

        var aviso = Aviso.DeTextoLivre(" Tampa do pote 2L ", 5, PrioridadeAviso.Media, null, Cenario.Atendente, Agora);
        Assert.Equal(("Tampa do pote 2L", (Guid?)null), (aviso.TextoLivre, aviso.ItemEstoqueId));
    }

    [Fact]
    public void Aviso_QuantidadeNegativaOuDataLocal_Lanca()
    {
        Assert.Throws<DomainException>(() => Aviso.DoItem(Guid.NewGuid(), -1, PrioridadeAviso.Media, null, Cenario.Atendente, Agora));
        Assert.Throws<DomainException>(() => Aviso.DoItem(Guid.NewGuid(), 1, PrioridadeAviso.Media, null, Cenario.Atendente, DateTime.Now));
    }

    [Fact]
    public void Aviso_WhatsApp_GuardaSoAPrimeiraAbertura()
    {
        var aviso = Aviso.DoItem(Guid.NewGuid(), 0, PrioridadeAviso.Urgente, null, Cenario.Atendente, Agora);

        aviso.MarcarWhatsAppAberto(Agora.AddMinutes(1));
        aviso.MarcarWhatsAppAberto(Agora.AddMinutes(5));

        Assert.Equal(Agora.AddMinutes(1), aviso.WhatsAppAbertoEm);
    }
}
