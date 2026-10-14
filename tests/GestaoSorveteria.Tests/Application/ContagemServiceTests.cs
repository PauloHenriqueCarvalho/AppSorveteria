using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Contagens;
using GestaoSorveteria.Contracts.Contagens;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Contagem semanal (RN-ES-05/06) pelo painel e pelo tablet.</summary>
public class ContagemServiceTests
{
    // Domingo 18/10/2026, 20:00 em Brasília (véspera do pedido de segunda).
    private static readonly DateTime Agora = new(2026, 10, 18, 23, 0, 0, DateTimeKind.Utc);

    private readonly ContagemRepositoryFake _contagens = new();
    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Agora };
    private readonly ContagemService _service;
    private readonly Usuario _ana;
    private readonly ItemEstoque _limao;
    private readonly ItemEstoque _uva;
    private readonly ItemEstoque _flocos;
    private readonly ItemEstoque _copo;

    public ContagemServiceTests()
    {
        _ana = Usuario.Criar("Ana", "ana", "hash", PerfilUsuario.Atendente, Agora.AddDays(-30));
        _usuarios.Usuarios.Add(_ana);

        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        var frutas = GrupoEstoque.Criar("Picolé de fruta", ModoControle.Contagem, 1);
        var massa = GrupoEstoque.Criar("Massa (balde)", ModoControle.Contagem, 2);
        var copos = GrupoEstoque.Criar("Copos", ModoControle.SoAviso, 3);
        _limao = ItemEstoque.Criar(frutas.Id, fabrica.Id, "Limão", UnidadeCompra.Caixa, 28, 25.20m, 1);
        _uva = ItemEstoque.Criar(frutas.Id, fabrica.Id, "Uva", UnidadeCompra.Caixa, 28, 25.20m, 2);
        _flocos = ItemEstoque.Criar(massa.Id, fabrica.Id, "Flocos", UnidadeCompra.Balde, 1, 90m, 1);
        _copo = ItemEstoque.Criar(copos.Id, fabrica.Id, "Copo 300 ml", UnidadeCompra.Pacote, 50, 12.90m, 1);
        _estoque.Fornecedores.Add(fabrica);
        _estoque.Grupos.AddRange([frutas, massa, copos]);
        _estoque.Itens.AddRange([_uva, _limao, _flocos, _copo]);

        _service = new ContagemService(_contagens, _estoque, _usuarios, SaldosFake.Novo(_clock, _estoque, _contagens), _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private Contagem ContagemConcluida(DateTime em, params (ItemEstoque Item, decimal Embalagens)[] linhas)
    {
        var contagem = Contagem.Iniciar(_ana.Id, em.AddMinutes(-20), OrigemContagem.App);
        foreach (var (item, embalagens) in linhas)
        {
            contagem.Registrar(item, _estoque.Grupos.Single(g => g.Id == item.GrupoId), embalagens);
        }

        contagem.Concluir(em);
        _contagens.Contagens.Add(contagem);
        return contagem;
    }

    // ---------- painel ----------

    [Fact]
    public async Task Modelo_ItensDaContagemNaOrdemDaPlanilhaVaziosEComAAnteriorDeReferencia()
    {
        ContagemConcluida(Agora.AddDays(-7), (_limao, 2.5m));

        var modelo = await _service.ModeloAsync(_ana.Id, Ct);

        Assert.Equal(new[] { "Limão", "Uva", "Flocos" }, modelo.Linhas.Select(l => l.ItemNome).ToArray()); // copos (só aviso) fora
        var limao = modelo.Linhas[0];
        Assert.Null(limao.ContadoUnidades); // RN-ES-06: o campo começa vazio
        Assert.Equal(70, limao.AnteriorUnidades);
        Assert.Equal(2.5m, limao.AnteriorEmbalagens);
        Assert.Equal(0.5m, limao.PassoEmbalagens);
        Assert.Equal(1m, modelo.Linhas[2].PassoEmbalagens);
        Assert.DoesNotContain(_contagens.Contagens, c => !c.Concluida); // nada gravado
    }

    [Fact]
    public async Task Salvar_ParcialEDepoisConcluida_GuardaNaMenorUnidade()
    {
        var id = Guid.NewGuid();
        await _service.SalvarAsync(new SalvarContagemRequest(id, [new LinhaContagemRequest(_limao.Id, 2.5m)], Concluir: false), _ana.Id, Ct);
        _clock.UtcNow = Agora.AddMinutes(15);

        var concluida = await _service.SalvarAsync(
            new SalvarContagemRequest(id, [new LinhaContagemRequest(_uva.Id, 0m), new LinhaContagemRequest(_flocos.Id, 3m)], Concluir: true),
            _ana.Id, Ct);

        Assert.Equal(Agora.AddMinutes(15), concluida.ConcluidaEm);
        Assert.Equal("Painel", concluida.Origem);
        Assert.Equal(70, concluida.Linhas.Single(l => l.ItemNome == "Limão").ContadoUnidades);
        Assert.Equal(0, concluida.Linhas.Single(l => l.ItemNome == "Uva").ContadoUnidades); // "Não tem nenhum" = 0
        Assert.Equal(3m, concluida.Linhas.Single(l => l.ItemNome == "Flocos").ContadoEmbalagens);
        Assert.Single(_contagens.Contagens);
        Assert.Equal(2, _uow.Confirmacoes);
        // Cada item guarda quando foi contado (base do saldo dos potes, RN-ES-06/08).
        var linhas = _contagens.Contagens.Single().Itens;
        Assert.Equal(Agora, linhas.Single(i => i.ItemEstoqueId == _limao.Id).ContadoEm);
        Assert.Equal(Agora.AddMinutes(15), linhas.Single(i => i.ItemEstoqueId == _flocos.Id).ContadoEm);
    }

    [Fact]
    public async Task Salvar_ContagemJaConcluida_Lanca()
    {
        var concluida = ContagemConcluida(Agora.AddHours(-1), (_limao, 1m));

        var erro = await Assert.ThrowsAsync<DomainException>(() =>
            _service.SalvarAsync(new SalvarContagemRequest(concluida.Id, [new LinhaContagemRequest(_limao.Id, 2m)], false), _ana.Id, Ct));

        Assert.Contains("concluída", erro.Message);
    }

    [Fact]
    public async Task Salvar_ItemDeGrupoSoAviso_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.SalvarAsync(new SalvarContagemRequest(null, [new LinhaContagemRequest(_copo.Id, 1m)], false), _ana.Id, Ct));
    }

    [Fact]
    public async Task Salvar_MeiaUnidadeDeBalde_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.SalvarAsync(new SalvarContagemRequest(null, [new LinhaContagemRequest(_flocos.Id, 1.5m)], false), _ana.Id, Ct));
    }

    [Fact]
    public async Task Listar_MostraAbertaEConcluidasRecentesComOsContados()
    {
        ContagemConcluida(Agora.AddDays(-7), (_limao, 2m), (_uva, 1m));
        ContagemConcluida(Agora.AddDays(-90), (_limao, 2m)); // fora dos 60 dias
        await _service.SalvarAsync(new SalvarContagemRequest(null, [new LinhaContagemRequest(_limao.Id, 1m)], false), _ana.Id, Ct);

        var lista = await _service.ListarAsync(Ct);

        Assert.Equal(2, lista.Count);
        Assert.Null(lista[0].ConcluidaEm); // a aberta (mais recente) primeiro
        Assert.Equal(2, lista[1].ItensContados);
        Assert.Equal("Ana", lista[1].ContadoPorNome);
    }

    [Fact]
    public async Task Obter_Inexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() => _service.ObterAsync(Guid.NewGuid(), Ct));
    }

    // ---------- tablet ----------

    private ContagemSyncDto DoTablet(Guid? id = null, DateTime? concluida = null, params (ItemEstoque Item, decimal? Embalagens)[] linhas) =>
        new(id ?? Guid.NewGuid(), _ana.Id, Agora.AddMinutes(-30), concluida,
            linhas.Select(l => new LinhaContagemSyncDto(Guid.NewGuid(), l.Item.Id, l.Embalagens)).ToList());

    private Task<SyncResponse> Enviar(params ContagemSyncDto[] contagens) => _service.ReceberSyncAsync(new SyncContagensRequest(contagens), Ct);

    [Fact]
    public async Task Sync_ContagemConcluidaNoTablet_CriaComOrigemApp()
    {
        var dto = DoTablet(concluida: Agora.AddMinutes(-2), linhas: [(_limao, 2.5m), (_uva, null)]);

        var resultado = Assert.Single((await Enviar(dto)).Resultados);

        Assert.Equal(StatusSync.Aceita, resultado.Status);
        var contagem = Assert.Single(_contagens.Contagens);
        Assert.Equal(OrigemContagem.App, contagem.Origem);
        Assert.Equal(Agora.AddMinutes(-2), contagem.ConcluidaEm);
        Assert.Null(contagem.Itens.Single(i => i.ItemEstoqueId == _uva.Id).ContadoUnidades); // não contado continua não contado
    }

    [Fact]
    public async Task Modelo_PoteComSaldoPelaVenda_MostraOSaldoQueOSistemaEsperava()
    {
        var potes = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 4);
        var morango = ItemEstoque.Criar(potes.Id, _limao.FornecedorId, "Morango", UnidadeCompra.Unidade, 1, 14m, 1);
        _estoque.Grupos.Add(potes);
        _estoque.Itens.Add(morango);
        var anterior = Contagem.Iniciar(_ana.Id, Agora.AddDays(-7), OrigemContagem.App);
        anterior.Registrar(morango, potes, 6m);
        anterior.Concluir(Agora.AddDays(-7).AddMinutes(10));
        _contagens.Contagens.Add(anterior);

        var nova = await _service.ModeloAsync(_ana.Id, Ct);

        Assert.Equal(6, nova.Linhas.Single(l => l.ItemEstoqueId == morango.Id).SaldoSistemaUnidades);
        Assert.Null(nova.Linhas.Single(l => l.ItemEstoqueId == _limao.Id).SaldoSistemaUnidades); // picolé vai pela contagem
    }

    [Fact]
    public async Task Modelo_PoteNuncaContado_NaoTemSaldoDoSistema()
    {
        var potes = GrupoEstoque.Criar("Pote 1L", ModoControle.SaldoPelaVenda, 4);
        var morango = ItemEstoque.Criar(potes.Id, _limao.FornecedorId, "Morango", UnidadeCompra.Unidade, 1, 14m, 1);
        _estoque.Grupos.Add(potes);
        _estoque.Itens.Add(morango);

        var nova = await _service.ModeloAsync(_ana.Id, Ct);

        // RN-ES-08: o saldo só vale depois da primeira contagem; sem ela, nada de "sistema 0".
        Assert.Null(nova.Linhas.Single(l => l.ItemEstoqueId == morango.Id).SaldoSistemaUnidades);
    }

    [Fact]
    public async Task Sync_HoraDoItemForaDaContagem_FicaDentroDela()
    {
        var dto = new ContagemSyncDto(Guid.NewGuid(), _ana.Id, Agora.AddMinutes(-30), Agora.AddMinutes(-2),
        [
            new LinhaContagemSyncDto(Guid.NewGuid(), _limao.Id, 2.5m, Agora.AddMinutes(-10)),
            new LinhaContagemSyncDto(Guid.NewGuid(), _uva.Id, 1m, Agora.AddHours(-5)), // relógio do tablet atrasado
            new LinhaContagemSyncDto(Guid.NewGuid(), _flocos.Id, 3m),
        ]);

        Assert.Equal(StatusSync.Aceita, Assert.Single((await Enviar(dto)).Resultados).Status);

        var linhas = _contagens.Contagens.Single().Itens;
        Assert.Equal(Agora.AddMinutes(-10), linhas.Single(i => i.ItemEstoqueId == _limao.Id).ContadoEm);
        Assert.Equal(Agora.AddMinutes(-30), linhas.Single(i => i.ItemEstoqueId == _uva.Id).ContadoEm);
        Assert.Null(linhas.Single(i => i.ItemEstoqueId == _flocos.Id).ContadoEm); // sem hora: vale a conclusão
    }

    [Fact]
    public async Task Sync_ParcialDepoisCompleta_AceitaENaoDuplica()
    {
        var id = Guid.NewGuid();
        await Enviar(DoTablet(id, linhas: [(_limao, 2m)]));

        var segunda = Assert.Single((await Enviar(DoTablet(id, Agora.AddMinutes(-1), (_limao, 2m), (_uva, 1m)))).Resultados);
        var reenvio = Assert.Single((await Enviar(DoTablet(id, Agora.AddMinutes(-1), (_limao, 2m), (_uva, 1m)))).Resultados);

        Assert.Equal(StatusSync.Aceita, segunda.Status);
        Assert.Equal(StatusSync.JaRecebida, reenvio.Status);
        var contagem = Assert.Single(_contagens.Contagens);
        Assert.True(contagem.Concluida);
        Assert.Equal(2, contagem.Itens.Count);
    }

    [Fact]
    public async Task Sync_MudancaNumaContagemJaConcluida_Rejeita()
    {
        var id = Guid.NewGuid();
        await Enviar(DoTablet(id, Agora.AddMinutes(-1), (_limao, 2m)));

        var resultado = Assert.Single((await Enviar(DoTablet(id, Agora.AddMinutes(-1), (_limao, 3m)))).Resultados);

        Assert.Equal(StatusSync.Rejeitada, resultado.Status);
        Assert.Contains("nova contagem", resultado.Motivo);
        Assert.Equal(56, _contagens.Contagens.Single().Itens.Single().ContadoUnidades);
    }

    [Fact]
    public async Task Sync_ProblemaNumaContagem_RejeitaSoEla()
    {
        var boa = DoTablet(linhas: [(_limao, 1m)]);
        var itemDesconhecido = new ContagemSyncDto(Guid.NewGuid(), _ana.Id, Agora, null, [new LinhaContagemSyncDto(Guid.NewGuid(), Guid.NewGuid(), 1m)]);
        var semUsuario = DoTablet(linhas: [(_uva, 1m)]) with { UsuarioId = Guid.NewGuid() };
        var futura = DoTablet(linhas: [(_uva, 1m)]) with { IniciadaEm = Agora.AddDays(2) };
        var repetida = DoTablet(linhas: [(_uva, 1m), (_uva, 2m)]);

        var resultados = (await Enviar(boa, itemDesconhecido, semUsuario, futura, repetida)).Resultados;

        Assert.Equal(
            new[] { StatusSync.Aceita, StatusSync.Rejeitada, StatusSync.Rejeitada, StatusSync.Rejeitada, StatusSync.Rejeitada },
            resultados.Select(r => r.Status).ToArray());
        Assert.Contains("catálogo", resultados[1].Motivo);
        Assert.Contains("Usuário", resultados[2].Motivo);
        Assert.Contains("relógio", resultados[3].Motivo);
        Assert.Contains("duas vezes", resultados[4].Motivo);
        Assert.Single(_contagens.Contagens);
    }
}
