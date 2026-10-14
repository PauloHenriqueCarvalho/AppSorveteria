using GestaoSorveteria.Application.Avisos;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Avisos "Está acabando" (RN-AV-01…08) e sincronização do tablet.</summary>
public class AvisoServiceTests
{
    // Quarta 14/10/2026, 15:42 em Brasília.
    private static readonly DateTime Agora = new(2026, 10, 14, 18, 42, 0, DateTimeKind.Utc);

    private readonly AvisoRepositoryFake _avisos = new();
    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly ConfiguracaoLojaRepositoryFake _configuracao = new();
    private readonly ComandasEmMemoria _comandas = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Agora };
    private readonly AvisoService _service;
    private readonly Usuario _ana;
    private readonly Usuario _dona;
    private readonly ItemEstoque _cocoPreto;
    private readonly ItemEstoque _flocos;

    public AvisoServiceTests()
    {
        _ana = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Agora.AddDays(-30));
        _dona = Usuario.Criar("Dona Laura", "laura", "hash", PerfilUsuario.Admin, Agora.AddDays(-30));
        _usuarios.Usuarios.AddRange([_ana, _dona]);

        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var leite = GrupoEstoque.Criar("Picolé ao leite", ModoControle.Contagem, 1);
        var potes = GrupoEstoque.Criar("Pote 1L", ModoControle.Contagem, 2);
        _cocoPreto = ItemEstoque.Criar(leite.Id, fabrica.Id, "Coco preto", UnidadeCompra.Caixa, 28, 25.20m, 1);
        _flocos = ItemEstoque.Criar(potes.Id, fabrica.Id, "Flocos", UnidadeCompra.Unidade, 1, 9m, 1);
        _estoque.Fornecedores.Add(fabrica);
        _estoque.Grupos.AddRange([leite, potes]);
        _estoque.Itens.AddRange([_cocoPreto, _flocos]);

        var configuracao = ConfiguracaoLoja.Padrao(Agora.AddDays(-1));
        configuracao.Atualizar("Verão Tropical", "(11) 98765-4321", Agora.AddDays(-1));
        _configuracao.Configuracao = configuracao;

        _service = new AvisoService(_avisos, _estoque, _usuarios, _configuracao, _comandas, _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Task<AvisoSalvoResponse> Avisar(ItemEstoque item, int quantidade, string prioridade, string? obs = null, Guid? por = null) =>
        _service.CriarAsync(new CriarAvisoRequest(null, item.Id, null, quantidade, prioridade, obs), por ?? _ana.Id, Ct);

    // ---------- criar e "ainda falta" ----------

    [Fact]
    public async Task Criar_UrgenteDeItem_DevolveAMensagemProntaParaOWhatsAppDaDona()
    {
        var resposta = await Avisar(_cocoPreto, 0, "Urgente", "Cliente pediu e não tinha.");

        Assert.True(resposta.EnviarAgora);
        Assert.False(resposta.JaExistia);
        Assert.Equal("Picolé ao leite Coco preto", resposta.Aviso.Descricao);
        Assert.Equal("Ana", resposta.Aviso.CriadoPorNome);
        Assert.Equal(
            "🔴 URGENTE — Verão Tropical\nPicolé ao leite Coco preto: ACABOU\nCliente pediu e não tinha.\nAna · 14/10 15:42",
            resposta.MensagemWhatsApp);
        Assert.StartsWith("https://wa.me/5511987654321?text=%F0%9F%94%B4%20URGENTE", resposta.LinkWhatsApp);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Criar_AltaOuMedia_NaoPedeWhatsAppNaHora()
    {
        var resposta = await Avisar(_flocos, 1, "Alta");

        Assert.False(resposta.EnviarAgora);
        Assert.Null(resposta.LinkWhatsApp);
    }

    [Fact]
    public async Task Criar_ItemComAvisoAberto_AtualizaOMesmoEFicaComAMaiorPrioridade()
    {
        var primeiro = await Avisar(_flocos, 2, "Alta");
        _clock.UtcNow = Agora.AddHours(1);

        var segundo = await Avisar(_flocos, 1, "Media", por: _dona.Id);

        Assert.True(segundo.JaExistia);
        Assert.False(segundo.EnviarAgora);
        Assert.Equal(primeiro.Aviso.Id, segundo.Aviso.Id);
        Assert.Equal(1, segundo.Aviso.QuantidadeAtual);
        Assert.Equal("Alta", segundo.Aviso.Prioridade);
        Assert.Equal("Dona Laura", segundo.Aviso.AtualizadoPorNome);
        Assert.Single(_avisos.Avisos);
    }

    [Fact]
    public async Task Criar_ItemComAvisoAbertoQueAgoraAcabou_ViraUrgenteEPedeWhatsApp()
    {
        await Avisar(_cocoPreto, 3, "Media");

        var resposta = await Avisar(_cocoPreto, 0, "Urgente");

        Assert.True(resposta.JaExistia);
        Assert.True(resposta.EnviarAgora);
        Assert.Contains("ACABOU", resposta.MensagemWhatsApp);
    }

    [Fact]
    public async Task Criar_TextoLivre_NaoJuntaComOutroTextoIgual()
    {
        await _service.CriarAsync(new CriarAvisoRequest(null, null, "Guardanapo", 0, "Alta", null), _ana.Id, Ct);
        await _service.CriarAsync(new CriarAvisoRequest(null, null, "Guardanapo", 0, "Alta", null), _ana.Id, Ct);

        Assert.Equal(2, _avisos.Avisos.Count);
    }

    [Fact]
    public async Task Criar_MesmoIdDeNovo_DevolveOJaGravadoEOLinkSoEnquantoNinguemAbriuOWhatsApp()
    {
        var id = Guid.NewGuid();
        await _service.CriarAsync(new CriarAvisoRequest(id, null, "Guardanapo", 0, "Urgente", null), _ana.Id, Ct);

        var reenvio = await _service.CriarAsync(new CriarAvisoRequest(id, null, "Guardanapo", 0, "Urgente", null), _ana.Id, Ct);
        await _service.MarcarWhatsAppAbertoAsync(id, Ct);
        var depoisDeAbrir = await _service.CriarAsync(new CriarAvisoRequest(id, null, "Guardanapo", 0, "Urgente", null), _ana.Id, Ct);

        Assert.Single(_avisos.Avisos);
        Assert.True(reenvio.JaExistia);
        Assert.True(reenvio.EnviarAgora); // a primeira resposta pode não ter chegado ao aparelho
        Assert.False(depoisDeAbrir.EnviarAgora);
    }

    [Fact]
    public async Task Criar_PeloPainelDepoisDeUmTabletComRelogioAdiantado_AplicaAQuantidadeInformada()
    {
        await Enviar(DoTablet(_flocos, 3, "Alta", criado: Agora.AddMinutes(-20), atualizado: Agora.AddMinutes(8)));

        var resposta = await Avisar(_flocos, 0, "Alta", por: _dona.Id);

        Assert.Equal(0, resposta.Aviso.QuantidadeAtual);
    }

    [Fact]
    public async Task Criar_ItemDesativado_Lanca()
    {
        _flocos.Desativar();

        var erro = await Assert.ThrowsAsync<DomainException>(() => Avisar(_flocos, 0, "Urgente"));

        Assert.Contains("desativado", erro.Message);
    }

    [Fact]
    public async Task Criar_PrioridadeDesconhecida_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() => Avisar(_flocos, 0, "Altíssima"));
    }

    [Fact]
    public async Task AindaFalta_BaixandoAPrioridade_Lanca()
    {
        var aviso = await Avisar(_flocos, 1, "Alta");

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.AindaFaltaAsync(aviso.Aviso.Id, new AindaFaltaRequest(1, "Media", null), _ana.Id, Ct));
    }

    [Fact]
    public async Task AindaFalta_SubindoParaUrgente_PedeWhatsApp()
    {
        var aviso = await Avisar(_flocos, 1, "Alta");
        _clock.UtcNow = Agora.AddMinutes(30);

        var resposta = await _service.AindaFaltaAsync(aviso.Aviso.Id, new AindaFaltaRequest(0, "Urgente", "Acabou agora"), _ana.Id, Ct);

        Assert.True(resposta.EnviarAgora);
        Assert.Contains("Pote 1L Flocos: ACABOU", resposta.MensagemWhatsApp);
        Assert.Contains("Ana · 14/10 16:12", resposta.MensagemWhatsApp);
    }

    [Fact]
    public async Task Fechar_PelaDona_FechaComMotivoEQuemFechou()
    {
        var aviso = await Avisar(_flocos, 0, "Urgente");

        var fechado = await _service.FecharAsync(aviso.Aviso.Id, new FecharAvisoRequest("Comprei no mercado"), _dona.Id, Ct);

        Assert.Equal("Fechado", fechado.Status);
        Assert.Equal("Dona Laura", fechado.FechadoPorNome);
        Assert.Equal("Comprei no mercado", fechado.MotivoFechamento);
    }

    [Fact]
    public async Task Fechar_Inexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.FecharAsync(Guid.NewGuid(), new FecharAvisoRequest("Comprei"), _dona.Id, Ct));
    }

    [Fact]
    public async Task Listar_SomenteAbertos_UrgentePrimeiroEFechadosDeFora()
    {
        await Avisar(_flocos, 2, "Media");
        var urgente = await Avisar(_cocoPreto, 0, "Urgente");
        var livre = await _service.CriarAsync(new CriarAvisoRequest(null, null, "Guardanapo", 0, "Alta", null), _ana.Id, Ct);
        await _service.FecharAsync(livre.Aviso.Id, new FecharAvisoRequest("Comprei"), _dona.Id, Ct);

        var abertos = await _service.ListarAsync(somenteAbertos: true, Ct);
        var todos = await _service.ListarAsync(somenteAbertos: false, Ct);

        Assert.Equal(new[] { urgente.Aviso.Id, abertos[1].Id }, abertos.Select(a => a.Id).ToArray());
        Assert.Equal("Media", abertos[1].Prioridade);
        Assert.Equal(3, todos.Count);
        Assert.Equal("Fechado", todos[2].Status);
    }

    [Fact]
    public async Task MarcarWhatsAppAberto_GuardaSoAPrimeiraVez()
    {
        var aviso = await Avisar(_cocoPreto, 0, "Urgente");
        await _service.MarcarWhatsAppAbertoAsync(aviso.Aviso.Id, Ct);
        _clock.UtcNow = Agora.AddMinutes(5);

        var dto = await _service.MarcarWhatsAppAbertoAsync(aviso.Aviso.Id, Ct);

        Assert.Equal(Agora, dto.WhatsAppAbertoEm);
    }

    // ---------- resumo do dia (RN-AV-07) ----------

    [Fact]
    public async Task ResumoDoDia_ComAvisosEVendas_MontaOTextoDoDocs10()
    {
        await Avisar(_cocoPreto, 0, "Urgente");
        await Avisar(_flocos, 1, "Alta");
        await _service.CriarAsync(new CriarAvisoRequest(null, null, "Copo 300 ml", 1, "Alta", null), _ana.Id, Ct);
        await _service.CriarAsync(new CriarAvisoRequest(null, null, "Massa Pistache", 1, "Media", null), _ana.Id, Ct);
        var resolvido = await _service.CriarAsync(new CriarAvisoRequest(null, null, "Casquinha", 0, "Urgente", null), _ana.Id, Ct);
        await _service.FecharAsync(resolvido.Aviso.Id, new FecharAvisoRequest("Comprei no mercado"), _dona.Id, Ct);
        Venda(Agora.AddHours(-3), 1000m);
        Venda(Agora.AddHours(-1), 284.50m);
        Venda(Agora.AddDays(-1), 99m); // ontem: fora

        var resumo = await _service.ResumoDoDiaAsync(null, Ct);

        Assert.Equal(new DateOnly(2026, 10, 14), resumo.Data);
        Assert.Equal(
            "Resumo de quarta 14/10 — Verão Tropical\n" +
            "🔴 Urgente (2): Picolé ao leite Coco preto — acabou · Casquinha — acabou (resolvido)\n" +
            "🟠 Alta (2): Pote 1L Flocos — 1 · Copo 300 ml — 1\n" +
            "🟡 Média (1): Massa Pistache — 1\n" +
            "Vendas do dia: R$ 1.284,50",
            resumo.Texto);
        Assert.Equal(2, resumo.Urgentes);
        Assert.StartsWith("https://wa.me/5511987654321?text=Resumo%20de%20quarta", resumo.LinkWhatsApp);
    }

    [Fact]
    public async Task ResumoDoDia_SemAvisosNemWhatsAppConfigurado_DizQueEstaTudoCertoELinkSemNumero()
    {
        _configuracao.Configuracao = null;

        var resumo = await _service.ResumoDoDiaAsync(null, Ct);

        Assert.Equal("Resumo de quarta 14/10 — Sorveteria\n✅ Nenhum aviso de estoque aberto.\nVendas do dia: R$ 0,00", resumo.Texto);
        Assert.StartsWith("https://wa.me/?text=", resumo.LinkWhatsApp);
    }

    [Fact]
    public async Task ResumoDoDia_UrgenteAindaAbertoDeOntem_EntraDeNovo()
    {
        _clock.UtcNow = Agora.AddDays(-1);
        await Avisar(_cocoPreto, 0, "Urgente");
        _clock.UtcNow = Agora;

        var resumo = await _service.ResumoDoDiaAsync(null, Ct);

        Assert.Equal(1, resumo.Urgentes);
    }

    // ---------- configuração ----------

    [Fact]
    public async Task SalvarConfiguracao_PrimeiraVez_CriaEGuardaSoOsDigitos()
    {
        _configuracao.Configuracao = null;

        var dto = await _service.SalvarConfiguracaoAsync(new SalvarConfiguracaoLojaRequest("Verão Tropical", "+55 (11) 98765-4321"), Ct);

        Assert.Equal("5511987654321", dto.WhatsAppDona);
        Assert.Equal(ConfiguracaoLoja.IdUnico, _configuracao.Configuracao!.Id);
    }

    // ---------- sincronização do tablet ----------

    private AvisoSyncDto DoTablet(
        ItemEstoque? item, int quantidade, string prioridade, DateTime? criado = null, DateTime? atualizado = null, Guid? id = null, string? texto = null) =>
        new(id ?? Guid.NewGuid(), item?.Id, texto, quantidade, prioridade, null, _ana.Id, criado ?? Agora.AddMinutes(-10), null,
            atualizado ?? criado ?? Agora.AddMinutes(-10), null);

    private Task<SyncAvisosResponse> Enviar(params AvisoSyncDto[] avisos) => _service.ReceberSyncAsync(new SyncAvisosRequest(avisos), Ct);

    [Fact]
    public async Task Sync_AvisoNovo_CriaComOIdDoTablet()
    {
        var dto = DoTablet(_cocoPreto, 0, "Urgente");

        var resultado = Assert.Single((await Enviar(dto)).Resultados);

        Assert.Equal(StatusSync.Aceita, resultado.Status);
        Assert.Equal(dto.Id, resultado.AvisoId);
        Assert.True(resultado.Aberto);
        Assert.Equal(dto.CriadoEm, Assert.Single(_avisos.Avisos).CriadoEm);
    }

    [Fact]
    public async Task Sync_ReenvioIgual_JaRecebida()
    {
        var dto = DoTablet(_cocoPreto, 0, "Urgente");
        await Enviar(dto);

        var resultado = Assert.Single((await Enviar(dto)).Resultados);

        Assert.Equal(StatusSync.JaRecebida, resultado.Status);
        Assert.Single(_avisos.Avisos);
    }

    [Fact]
    public async Task Sync_ItemJaComAvisoAbertoNoServidor_JuntaNeleEDevolveOIdDoServidor()
    {
        var doPainel = await Avisar(_flocos, 2, "Alta", por: _dona.Id);
        var dto = DoTablet(_flocos, 0, "Media", criado: Agora.AddMinutes(5));
        _clock.UtcNow = Agora.AddMinutes(10);

        var resultado = Assert.Single((await Enviar(dto)).Resultados);

        Assert.Equal(StatusSync.Aceita, resultado.Status);
        Assert.Equal(doPainel.Aviso.Id, resultado.AvisoId);
        var aviso = Assert.Single(_avisos.Avisos);
        Assert.Equal(0, aviso.QuantidadeAtual); // informação mais nova
        Assert.Equal(PrioridadeAviso.Alta, aviso.Prioridade); // prioridade nunca baixa
    }

    [Fact]
    public async Task Sync_AtualizacaoMaisAntigaQueADoServidor_NaoVoltaAQuantidadeMasSobeAPrioridade()
    {
        var id = Guid.NewGuid();
        await Enviar(DoTablet(_flocos, 1, "Media", criado: Agora.AddMinutes(-30), atualizado: Agora.AddMinutes(-5), id: id));

        await Enviar(DoTablet(_flocos, 3, "Alta", criado: Agora.AddMinutes(-30), atualizado: Agora.AddMinutes(-20), id: id));

        var aviso = Assert.Single(_avisos.Avisos);
        Assert.Equal(1, aviso.QuantidadeAtual);
        Assert.Equal(PrioridadeAviso.Alta, aviso.Prioridade);
    }

    [Fact]
    public async Task Sync_AvisoQueADonaJaFechou_JaRecebidaEFechado()
    {
        var dto = DoTablet(_cocoPreto, 0, "Urgente");
        await Enviar(dto);
        await _service.FecharAsync(dto.Id, new FecharAvisoRequest("Chegou"), _dona.Id, Ct);

        var resultado = Assert.Single((await Enviar(dto with { QuantidadeAtual = 0, AtualizadoEm = Agora })).Resultados);

        Assert.Equal(StatusSync.JaRecebida, resultado.Status);
        Assert.False(resultado.Aberto);
    }

    [Fact]
    public async Task Sync_ProblemaNumAviso_RejeitaSoEle()
    {
        var bom = DoTablet(_cocoPreto, 0, "Urgente");
        var semUsuario = DoTablet(_flocos, 0, "Urgente") with { CriadoPorUsuarioId = Guid.NewGuid() };
        var dataLocal = DoTablet(null, 0, "Alta", texto: "Guardanapo") with
        {
            CriadoEm = DateTime.SpecifyKind(Agora, DateTimeKind.Local),
            AtualizadoEm = DateTime.SpecifyKind(Agora, DateTimeKind.Local),
        };
        var itemDesconhecido = new AvisoSyncDto(Guid.NewGuid(), Guid.NewGuid(), null, 0, "Alta", null, _ana.Id, Agora, null, Agora, null);

        var resultados = (await Enviar(bom, semUsuario, dataLocal, itemDesconhecido)).Resultados;

        Assert.Equal(
            new[] { StatusSync.Aceita, StatusSync.Rejeitada, StatusSync.Rejeitada, StatusSync.Rejeitada },
            resultados.Select(r => r.Status).ToArray());
        Assert.Contains("Usuário", resultados[1].Motivo);
        Assert.Contains("UTC", resultados[2].Motivo);
        Assert.Contains("catálogo", resultados[3].Motivo);
        Assert.Single(_avisos.Avisos);
    }

    [Fact]
    public async Task Sync_AvisoJuntadoAOutroQueDepoisFoiFechado_ReenvioNaoReabre()
    {
        var doPainel = await Avisar(_flocos, 2, "Alta", por: _dona.Id);
        var doTablet = DoTablet(_flocos, 1, "Alta", criado: Agora.AddMinutes(-5));
        await Enviar(doTablet); // juntado no aviso do painel; a resposta "se perdeu"
        _clock.UtcNow = Agora.AddMinutes(30);
        await _service.FecharAsync(doPainel.Aviso.Id, new FecharAvisoRequest("Chegou na entrega"), _dona.Id, Ct);

        var resultado = Assert.Single((await Enviar(doTablet)).Resultados);

        Assert.Equal(StatusSync.JaRecebida, resultado.Status);
        Assert.Equal(doPainel.Aviso.Id, resultado.AvisoId);
        Assert.False(resultado.Aberto);
        Assert.DoesNotContain(_avisos.Avisos, a => a.Aberto);
    }

    [Fact]
    public async Task Sync_AvisoOfflineAnteriorAoFechamentoDoItem_NaoReabreMasOPosteriorCriaOutro()
    {
        var doPainel = await Avisar(_cocoPreto, 0, "Urgente", por: _dona.Id);
        _clock.UtcNow = Agora.AddHours(2);
        await _service.FecharAsync(doPainel.Aviso.Id, new FecharAvisoRequest("Comprei no mercado"), _dona.Id, Ct);

        var antigo = Assert.Single((await Enviar(DoTablet(_cocoPreto, 0, "Urgente", criado: Agora.AddHours(1)))).Resultados);
        var novo = Assert.Single((await Enviar(DoTablet(_cocoPreto, 0, "Urgente", criado: Agora.AddHours(2).AddMinutes(1)))).Resultados);

        Assert.Equal(StatusSync.JaRecebida, antigo.Status);
        Assert.False(antigo.Aberto);
        Assert.Equal(StatusSync.Aceita, novo.Status);
        Assert.True(novo.Aberto);
        Assert.Equal(2, _avisos.Avisos.Count);
    }

    [Fact]
    public async Task Sync_AindaFaltaFeitoOfflineDepoisQueADonaFechou_RejeitaComOMotivo()
    {
        var dto = DoTablet(_cocoPreto, 2, "Alta");
        await Enviar(dto);
        await _service.FecharAsync(dto.Id, new FecharAvisoRequest("Chegou"), _dona.Id, Ct);
        _clock.UtcNow = Agora.AddMinutes(30);

        var resultado = Assert.Single((await Enviar(dto with { QuantidadeAtual = 0, Prioridade = "Urgente", AtualizadoEm = Agora.AddMinutes(20) })).Resultados);

        Assert.Equal(StatusSync.Rejeitada, resultado.Status);
        Assert.Contains("A dona fechou este aviso às 14/10 15:42", resultado.Motivo);
    }

    [Fact]
    public async Task Sync_DataDeAtualizacaoNoFuturo_Rejeita()
    {
        var resultado = Assert.Single((await Enviar(DoTablet(_flocos, 1, "Alta", atualizado: Agora.AddYears(1)))).Resultados);

        Assert.Equal(StatusSync.Rejeitada, resultado.Status);
        Assert.Contains("relógio", resultado.Motivo);
        Assert.Empty(_avisos.Avisos);
    }

    [Fact]
    public async Task Sync_MesmoIdParaOutroItem_Rejeita()
    {
        var dto = DoTablet(_flocos, 1, "Alta");
        await Enviar(dto);

        var resultado = Assert.Single((await Enviar(dto with { ItemEstoqueId = _cocoPreto.Id })).Resultados);

        Assert.Equal(StatusSync.Rejeitada, resultado.Status);
        Assert.Equal(_flocos.Id, Assert.Single(_avisos.Avisos).ItemEstoqueId);
    }

    private void Venda(DateTime fechadaEm, decimal valor)
    {
        var caixa = Caixa.Abrir(_ana.Id, 100m, fechadaEm.AddHours(-2));
        var comanda = Comanda.Abrir(caixa, _ana.Id, _comandas.Comandas.Count + 1, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        comanda.AdicionarItemLivre("Venda avulsa", valor);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, valor)], fechadaEm);
        _comandas.Comandas.Add(comanda);
    }
}
