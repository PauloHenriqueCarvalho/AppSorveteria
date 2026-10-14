using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Pedidos;
using GestaoSorveteria.Contracts.Pedidos;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Pedido sugerido, envio, conferência da entrega e histórico (RN-PF-02…08, RN-RC-01…06).</summary>
public class PedidoServiceTests
{
    // Segunda 19/10/2026, 12:00 em Brasília: dia do pedido da fábrica.
    private static readonly DateTime Agora = new(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc);

    private readonly PedidoFornecedorRepositoryFake _pedidos = new();
    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly ContagemRepositoryFake _contagens = new();
    private readonly AvisoRepositoryFake _avisos = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ConfiguracaoLojaRepositoryFake _configuracao = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Agora };
    private readonly PedidoService _service;

    private readonly Usuario _dona;
    private readonly Usuario _ana;
    private readonly Fornecedor _fabrica;
    private readonly GrupoEstoque _frutas;
    private readonly GrupoEstoque _massa;
    private readonly GrupoEstoque _descartaveis;
    private readonly ItemEstoque _limao;
    private readonly ItemEstoque _uva;
    private readonly ItemEstoque _flocos;
    private readonly ItemEstoque _copo;
    private readonly ItemEstoque _guardanapo;

    public PedidoServiceTests()
    {
        _dona = Usuario.Criar("Dona Laura", "laura", "hash", PerfilUsuario.Admin, Agora.AddDays(-60));
        _ana = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Agora.AddDays(-60));
        _usuarios.Usuarios.AddRange([_dona, _ana]);

        _fabrica = Fornecedor.Criar("Fábrica", "11987654321", [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Ambos, true);
        _frutas = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
        _massa = GrupoEstoque.Criar("Massa (balde)", ModoControle.Contagem, 2);
        _descartaveis = GrupoEstoque.Criar("Descartáveis", ModoControle.SoAviso, 3);
        _limao = ItemEstoque.Criar(_frutas.Id, _fabrica.Id, "Limão", UnidadeCompra.Caixa, 28, 25.20m, 1);
        _uva = ItemEstoque.Criar(_frutas.Id, _fabrica.Id, "Uva", UnidadeCompra.Caixa, 28, 25.20m, 2, estoqueAlvoEmbalagens: 2m);
        _flocos = ItemEstoque.Criar(_massa.Id, _fabrica.Id, "Flocos", UnidadeCompra.Balde, 1, 90m, 1);
        _copo = ItemEstoque.Criar(_descartaveis.Id, _fabrica.Id, "Copo 300 ml", UnidadeCompra.Pacote, 50, 12.90m, 1);
        _guardanapo = ItemEstoque.Criar(_descartaveis.Id, _fabrica.Id, "Guardanapo", UnidadeCompra.Pacote, 100, 8m, 2);
        _estoque.Fornecedores.Add(_fabrica);
        _estoque.Grupos.AddRange([_frutas, _massa, _descartaveis]);
        _estoque.Itens.AddRange([_limao, _uva, _flocos, _copo, _guardanapo]);

        var configuracao = ConfiguracaoLoja.Padrao(Agora);
        configuracao.Atualizar("Verão Tropical", null, Agora);
        _configuracao.Configuracao = configuracao;

        // Contagens de domingo à noite: limão 70 → 42 → 14 (saiu 28 por semana); flocos contado só uma vez.
        Contagem(Agora.AddDays(-15), (_limao, 2.5m));
        Contagem(Agora.AddDays(-8), (_limao, 1.5m));
        Contagem(Agora.AddDays(-1), (_limao, 0.5m), (_flocos, 3m));

        _avisos.Avisos.Add(Aviso.DoItem(_flocos.Id, 0, PrioridadeAviso.Urgente, null, _ana.Id, Agora.AddHours(-3)));
        _avisos.Avisos.Add(Aviso.DoItem(_guardanapo.Id, 1, PrioridadeAviso.Alta, null, _ana.Id, Agora.AddHours(-2)));

        _service = new PedidoService(_pedidos, _estoque, _contagens, _avisos, _usuarios, _configuracao, _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void Contagem(DateTime concluidaEm, params (ItemEstoque Item, decimal Caixas)[] linhas)
    {
        var contagem = GestaoSorveteria.Domain.Estoque.Contagem.Iniciar(_ana.Id, concluidaEm.AddMinutes(-30), OrigemContagem.App);
        foreach (var (item, caixas) in linhas)
        {
            contagem.Registrar(item, _estoque.Grupos.Single(g => g.Id == item.GrupoId), caixas);
        }

        contagem.Concluir(concluidaEm);
        _contagens.Contagens.Add(contagem);
    }

    private static LinhaPedidoDto Linha(PedidoDto pedido, ItemEstoque item) => pedido.Linhas.Single(l => l.ItemEstoqueId == item.Id);

    [Fact]
    public async Task Montar_ComContagensAlvoEAvisos_SugereCadaItem()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        Assert.Equal("Rascunho", pedido.Status);
        Assert.Equal(1, pedido.Numero);
        // Limão: 28 por semana × 9 dias (segunda → próxima segunda + 2) = 36 − 14 contados = 22 picolés → 1 caixa.
        Assert.Equal(1m, Linha(pedido, _limao).SugeridoEmbalagens);
        Assert.Equal(14, Linha(pedido, _limao).ContadoUnidades);
        // Uva: nunca contada, alvo de 2 caixas.
        Assert.Equal(2m, Linha(pedido, _uva).SugeridoEmbalagens);
        // Flocos: sem histórico nem alvo, mas com aviso urgente → 1 balde, marcado.
        var flocos = Linha(pedido, _flocos);
        Assert.Equal(1m, flocos.SugeridoEmbalagens);
        Assert.Equal("Aviso", flocos.Origem);
        Assert.Equal("Ana avisou (urgente): acabou", flocos.Observacao);
        // Descartáveis (só aviso): só o guardanapo, que tem aviso.
        Assert.DoesNotContain(pedido.Linhas, l => l.ItemEstoqueId == _copo.Id);
        Assert.Equal(1m, Linha(pedido, _guardanapo).PedidoEmbalagens);
        // Ordem da planilha e total calculado pela API.
        Assert.Equal(new[] { "Limão", "Uva", "Flocos", "Guardanapo" }, pedido.Linhas.Select(l => l.ItemNome).ToArray());
        Assert.Equal(173.60m, pedido.TotalPedido); // 25,20 + 2 × 25,20 + 90 + 8
        Assert.Equal(new DateOnly(2026, 10, 21), pedido.EntregaPrevista);
    }

    [Fact]
    public async Task Montar_DeNovo_RemontaOMesmoRascunhoMantendoOQueADonaMudou()
    {
        var primeiro = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.AlterarItensAsync(primeiro.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_limao.Id, 4m)]), Ct);

        var segundo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        Assert.Equal(primeiro.Id, segundo.Id);
        Assert.Single(_pedidos.Pedidos);
        Assert.Equal(4m, Linha(segundo, _limao).PedidoEmbalagens);
        Assert.True(Linha(segundo, _limao).AlteradoPelaDona);
        Assert.Equal(1m, Linha(segundo, _limao).SugeridoEmbalagens);
    }

    [Fact]
    public async Task AlterarItens_ItemForaDoPedido_AcrescentaEMeiaCaixaSemPermissaoLanca()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        var alterado = await _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_copo.Id, 2m)]), Ct);

        Assert.Equal("AdicionadoPelaDona", Linha(alterado, _copo).Origem);
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_limao.Id, 1.5m)]), Ct));
    }

    [Fact]
    public async Task Enviar_FornecedorComWhatsApp_DevolveAMensagemDoPedido()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        var enviado = await _service.EnviarAsync(pedido.Id, Ct);

        Assert.Equal("Enviado", enviado.Pedido.Status);
        Assert.Equal(
            "Pedido nº 1 — Verão Tropical\n" +
            "Para: Fábrica · segunda 19/10\n" +
            "Picolé de fruta: Limão 1 cx · Uva 2 cx\n" +
            "Massa (balde): Flocos 1 balde\n" +
            "Descartáveis: Guardanapo 1 pct\n" +
            "Total estimado: R$ 173,60",
            enviado.MensagemWhatsApp);
        Assert.StartsWith("https://wa.me/5511987654321?text=Pedido", enviado.LinkWhatsApp);
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_limao.Id, 3m)]), Ct));
    }

    [Fact]
    public async Task Enviar_FornecedorSoImpresso_SemMensagem()
    {
        _fabrica.Atualizar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, false);
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        var enviado = await _service.EnviarAsync(pedido.Id, Ct);

        Assert.Null(enviado.MensagemWhatsApp);
        Assert.False(enviado.Pedido.MostrarPrecos);
    }

    [Fact]
    public async Task Receber_ChegouTudoCerto_FechaOsAvisosDosItensQueChegaram()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);

        var recebido = await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest(null, null, null), _dona.Id, Ct);

        Assert.Equal("Recebido", recebido.Pedido.Status);
        Assert.Equal(100m, recebido.Pedido.PercentualAtendido);
        Assert.Equal(2, recebido.AvisosFechados);
        Assert.All(_avisos.Avisos, a => Assert.False(a.Aberto));
        Assert.Null(recebido.MensagemFornecedor);
    }

    [Fact]
    public async Task Receber_ComFaltaETroca_MensagemParaOFornecedorEOQueFaltouVoltaNoProximo()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);

        var recebido = await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest(
            [new LinhaRecebidaRequest(_uva.Id, 0m), new LinhaRecebidaRequest(_flocos.Id, 2m)],
            [new LinhaRecebidaRequest(_copo.Id, 1m)],
            null), _dona.Id, Ct);

        Assert.Equal(
            "Entrega do pedido nº 1 — Verão Tropical\n" +
            "Faltou: Uva 2 cx\n" +
            "Veio a mais: Flocos 1 balde\n" +
            "Veio sem pedir: Copo 300 ml 1 pct",
            recebido.MensagemFornecedor);
        Assert.Equal("Faltou", Linha(recebido.Pedido, _uva).Situacao);
        Assert.Equal(-50.40m, Linha(recebido.Pedido, _uva).DiferencaValor);
        Assert.Equal(60m, recebido.Pedido.PercentualAtendido); // atendido 3 de 5 embalagens pedidas

        // Na semana seguinte, a uva que faltou volta no pedido (RN-RC-04).
        _clock.UtcNow = Agora.AddDays(7);
        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        Assert.Equal(2, proximo.Numero);
        var uva = Linha(proximo, _uva);
        Assert.Equal("FaltouAnterior", uva.Origem);
        Assert.Equal(2m, uva.SugeridoEmbalagens);
        Assert.Equal("Faltou 2 cx no pedido nº 1", uva.Observacao);
    }

    [Fact]
    public async Task Montar_FaltouMaisQueAConta_PedeOQueFaltou()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_limao.Id, 3m)]), Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);
        await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest([new LinhaRecebidaRequest(_limao.Id, 0m)], null, null), _dona.Id, Ct);

        _clock.UtcNow = Agora.AddDays(7);
        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        // A conta daria 1 caixa (36 − 14); faltaram 3 da semana passada: vão as 3.
        var limao = Linha(proximo, _limao);
        Assert.Equal(3m, limao.SugeridoEmbalagens);
        Assert.Equal("FaltouAnterior", limao.Origem);
    }

    [Fact]
    public async Task Montar_FaltaJaPedidaDeNovo_NaoVoltaOutraVez()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);
        await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest([new LinhaRecebidaRequest(_uva.Id, 0m)], null, null), _dona.Id, Ct);
        _clock.UtcNow = Agora.AddDays(7);
        var segundo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        Assert.Equal("FaltouAnterior", Linha(segundo, _uva).Origem);
        await _service.EnviarAsync(segundo.Id, Ct);

        // Pedido 2 ainda a caminho: a uva que faltou já está nele.
        var terceiro = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        Assert.Equal("Sugestao", Linha(terceiro, _uva).Origem);
        Assert.Equal(0m, Linha(terceiro, _uva).SugeridoEmbalagens); // alvo 2 caixas, 2 a caminho
        Assert.Equal("A caminho: 2 cx no pedido nº 2", Linha(terceiro, _uva).Observacao);

        // Pedido 2 chegou certo: a falta do pedido 1 não volta.
        _clock.UtcNow = Agora.AddDays(9);
        await _service.ReceberAsync(segundo.Id, new ReceberPedidoRequest(null, null, null), _dona.Id, Ct);
        var remontado = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        Assert.Equal(terceiro.Id, remontado.Id);
        Assert.Equal("Sugestao", Linha(remontado, _uva).Origem);
        Assert.Null(Linha(remontado, _uva).Observacao);
        // Uva nunca contada: as 2 caixas que chegaram neste ciclo já cobrem o alvo de 2.
        Assert.Equal(0m, Linha(remontado, _uva).SugeridoEmbalagens);
    }

    [Fact]
    public async Task Montar_DoisPedidosEnviadosEOPrimeiroChegaComFalta_FaltaVolta()
    {
        var primeiro = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(primeiro.Id, Ct);
        // Pedido extra na quarta, antes de o primeiro chegar.
        _clock.UtcNow = Agora.AddDays(2);
        var extra = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.AlterarItensAsync(extra.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_copo.Id, 1m)]), Ct);
        await _service.EnviarAsync(extra.Id, Ct);
        // O primeiro chega na quinta sem a uva.
        _clock.UtcNow = Agora.AddDays(3);
        await _service.ReceberAsync(primeiro.Id, new ReceberPedidoRequest([new LinhaRecebidaRequest(_uva.Id, 0m)], null, null), _dona.Id, Ct);

        _clock.UtcNow = Agora.AddDays(7);
        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        Assert.Equal("FaltouAnterior", Linha(proximo, _uva).Origem);
        Assert.Equal(2m, Linha(proximo, _uva).SugeridoEmbalagens);
    }

    [Fact]
    public async Task Montar_ItemPuladoNasContagensSeguintes_DescontaASaidaEstimadaDesdeAContagem()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);
        await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest(null, null, null), _dona.Id, Ct);

        // Duas semanas sem contar o limão: 14 contados + 28 que chegaram − 60 que saíram (28 por semana × 15 dias) = 0.
        _clock.UtcNow = Agora.AddDays(14);
        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        var limao = Linha(proximo, _limao);
        Assert.Equal(2m, limao.SugeridoEmbalagens); // 36 para os próximos 9 dias
        Assert.Equal("Última contagem em 18/10", limao.Observacao);
    }

    [Fact]
    public async Task Montar_DeNovo_InsumoAcrescentadoPelaDonaAcompanhaOCustoDoCatalogo()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_copo.Id, 2m)]), Ct);
        _copo.AlterarCusto(14m);

        var remontado = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        var copo = Linha(remontado, _copo);
        Assert.Equal(14m, copo.CustoEmbalagem);
        Assert.Equal(2m, copo.PedidoEmbalagens);
        Assert.Equal("AdicionadoPelaDona", copo.Origem);
    }

    [Fact]
    public async Task Receber_FaltouComPedirDeNovoDesmarcado_NaoVoltaNoProximo()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);
        await _service.ReceberAsync(pedido.Id,
            new ReceberPedidoRequest([new LinhaRecebidaRequest(_uva.Id, 0m, PedirDeNovo: false)], null, null), _dona.Id, Ct);

        _clock.UtcNow = Agora.AddDays(7);
        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        Assert.Equal("Sugestao", Linha(proximo, _uva).Origem);
        Assert.Null(Linha(proximo, _uva).Observacao);
    }

    [Fact]
    public async Task Receber_ItemComAvisoVeioZero_AvisoFicaAbertoEVoltaComAFaltaNaObservacao()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddDays(2);

        var recebido = await _service.ReceberAsync(pedido.Id,
            new ReceberPedidoRequest([new LinhaRecebidaRequest(_flocos.Id, 0m)], null, null), _dona.Id, Ct);

        Assert.Equal(1, recebido.AvisosFechados); // só o guardanapo
        Assert.True(_avisos.Avisos.Single(a => a.ItemEstoqueId == _flocos.Id).Aberto);

        _clock.UtcNow = Agora.AddDays(7);
        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        var flocos = Linha(proximo, _flocos);
        Assert.Equal("Aviso", flocos.Origem);
        Assert.Equal(1m, flocos.SugeridoEmbalagens);
        Assert.Equal("Ana avisou (urgente): acabou · Faltou 1 balde no pedido nº 1", flocos.Observacao);
    }

    [Fact]
    public async Task Montar_EntregaDepoisDaUltimaContagem_SomaNoEstoque()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_limao.Id, 2m)]), Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        _clock.UtcNow = Agora.AddHours(2);
        await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest(null, null, null), _dona.Id, Ct);

        var proximo = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        // 14 contados ontem + 56 que chegaram hoje = 70, mais que os 36 dos próximos 9 dias.
        Assert.Equal(0m, Linha(proximo, _limao).SugeridoEmbalagens);
    }

    [Fact]
    public async Task Montar_PedidoEnviadoAindaNaoChegou_ContaOQueEstaACaminhoENaoRepeteOAviso()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.AlterarItensAsync(pedido.Id, new AlterarItensPedidoRequest([new LinhaAlteradaRequest(_limao.Id, 2m)]), Ct);
        await _service.EnviarAsync(pedido.Id, Ct);

        var outro = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);

        Assert.NotEqual(pedido.Id, outro.Id);
        var limao = Linha(outro, _limao);
        Assert.Equal(0m, limao.SugeridoEmbalagens); // 14 contados + 56 a caminho
        Assert.Equal("A caminho: 2 cx no pedido nº 1", limao.Observacao);
        // Flocos e guardanapo já vêm no pedido 1, enviado depois dos avisos: não entram de novo.
        Assert.Equal(0m, Linha(outro, _flocos).SugeridoEmbalagens);
        Assert.Equal("Sugestao", Linha(outro, _flocos).Origem);
        Assert.DoesNotContain(outro.Linhas, l => l.ItemEstoqueId == _guardanapo.Id);
    }

    [Fact]
    public async Task Montar_AutomaticoComPedidoDeHojeJaEnviado_DevolveEleEmVezDeMontarOutro()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, automatico: true, Ct);
        Assert.Equal("Rascunho", pedido.Status);
        await _service.EnviarAsync(pedido.Id, Ct);

        var deNovo = await _service.MontarAsync(_fabrica.Id, _dona.Id, automatico: true, Ct);

        Assert.Equal(pedido.Id, deNovo.Id);
        Assert.Equal("Enviado", deNovo.Status);
        Assert.Single(_pedidos.Pedidos);

        // Na mão (botão "Montar pedido"), a dona pode começar outro no mesmo dia.
        var manual = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        Assert.Equal(2, manual.Numero);
    }

    [Fact]
    public async Task Mensagens_ReabreOWhatsAppDoPedidoEDoQueVeioDiferente()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        var rascunho = await _service.MensagensAsync(pedido.Id, Ct);
        Assert.Null(rascunho.Pedido);

        await _service.EnviarAsync(pedido.Id, Ct);
        var enviado = await _service.MensagensAsync(pedido.Id, Ct);
        Assert.StartsWith("Pedido nº 1 — Verão Tropical", enviado.Pedido);
        Assert.StartsWith("https://wa.me/5511987654321?text=", enviado.LinkPedido);
        Assert.Null(enviado.Divergencias);

        await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest([new LinhaRecebidaRequest(_uva.Id, 1m)], null, null), _dona.Id, Ct);
        var recebido = await _service.MensagensAsync(pedido.Id, Ct);
        Assert.Equal("Entrega do pedido nº 1 — Verão Tropical\nFaltou: Uva 1 cx", recebido.Divergencias);
        Assert.NotNull(recebido.LinkDivergencias);
    }

    [Fact]
    public async Task Receber_NovoCusto_VaiParaOCatalogoEOPedidoFicaComOAntigo()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);

        var recebido = await _service.ReceberAsync(pedido.Id,
            new ReceberPedidoRequest(null, null, [new NovoCustoRequest(_limao.Id, 26.40m)]), _dona.Id, Ct);

        Assert.Equal(26.40m, _limao.CustoEmbalagem);
        Assert.Equal(25.20m, Linha(recebido.Pedido, _limao).CustoEmbalagem);
    }

    [Fact]
    public async Task Receber_NovoCustoDoSaborQueVeioSemPedir_EntraComOCustoNovo()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);

        var recebido = await _service.ReceberAsync(pedido.Id,
            new ReceberPedidoRequest(null, [new LinhaRecebidaRequest(_copo.Id, 1m)], [new NovoCustoRequest(_copo.Id, 13.50m)]), _dona.Id, Ct);

        Assert.Equal(13.50m, Linha(recebido.Pedido, _copo).CustoEmbalagem);
        Assert.Equal(13.50m, Linha(recebido.Pedido, _copo).DiferencaValor);
    }

    [Fact]
    public async Task Receber_NovoCustoDeItemQueVeioZeroOuNaoVeio_Lanca()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);

        await Assert.ThrowsAsync<DomainException>(() => _service.ReceberAsync(pedido.Id,
            new ReceberPedidoRequest([new LinhaRecebidaRequest(_uva.Id, 0m)], null, [new NovoCustoRequest(_uva.Id, 30m)]), _dona.Id, Ct));
        await Assert.ThrowsAsync<DomainException>(() => _service.ReceberAsync(pedido.Id,
            new ReceberPedidoRequest(null, null, [new NovoCustoRequest(_copo.Id, 13m)]), _dona.Id, Ct));
        Assert.Equal(StatusPedido.Enviado, _pedidos.Pedidos.Single().Status);
    }

    [Fact]
    public async Task Cancelar_PedidoEnviado_GuardaMotivoEQuemCancelou()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);

        var cancelado = await _service.CancelarAsync(pedido.Id, new CancelarPedidoRequest("A fábrica fechou esta semana"), _dona.Id, Ct);

        Assert.Equal("Cancelado", cancelado.Status);
        Assert.Equal("Dona Laura", cancelado.CanceladoPorNome);
        Assert.Equal("A fábrica fechou esta semana", cancelado.MotivoCancelamento);
    }

    [Fact]
    public async Task Historico_UmPedidoComFalta_PercentualMedioEItensQueMaisFaltam()
    {
        var pedido = await _service.MontarAsync(_fabrica.Id, _dona.Id, cancellationToken: Ct);
        await _service.EnviarAsync(pedido.Id, Ct);
        await _service.ReceberAsync(pedido.Id, new ReceberPedidoRequest([new LinhaRecebidaRequest(_uva.Id, 1m)], null, null), _dona.Id, Ct);

        var historico = await _service.HistoricoAsync(_fabrica.Id, Ct);

        Assert.Equal(1, historico.PedidosRecebidos);
        Assert.Equal(80m, historico.PercentualAtendidoMedio); // 4 de 5
        var falta = Assert.Single(historico.ItensQueMaisFaltam);
        Assert.Equal("Picolé de fruta Uva", falta.Descricao);
        Assert.Equal(1m, falta.EmbalagensQueFaltaram);
    }

    [Fact]
    public async Task Montar_FornecedorInexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() => _service.MontarAsync(Guid.NewGuid(), _dona.Id, cancellationToken: Ct));
    }
}
