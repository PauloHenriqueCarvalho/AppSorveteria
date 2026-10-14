using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Catálogo de estoque da Fase 2 (RN-ES-01/02, RN-PF-01) e importação da planilha da dona.</summary>
public class CatalogoEstoqueServiceTests
{
    private const string Cabecalho = "categoria,sabor,unidade_compra,unidades_por_embalagem,custo_embalagem,fornecedor,origem_na_planilha,observacao";

    private readonly EstoqueRepositoryFake _estoque = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly CatalogoEstoqueService _service;

    public CatalogoEstoqueServiceTests()
    {
        _service = new CatalogoEstoqueService(_estoque, _uow);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ImportarCatalogoRequest Csv(params string[] linhas) => new(string.Join("\n", linhas));

    private Fornecedor Fabrica(bool ativo = true)
    {
        var fabrica = Fornecedor.Criar("Fábrica", null, [DayOfWeek.Monday], 2, false, FormaEnvioPedido.Impresso, true);
        if (!ativo)
        {
            fabrica.Desativar();
        }

        _estoque.Fornecedores.Add(fabrica);
        return fabrica;
    }

    private GrupoEstoque Grupo(string nome, ModoControle modo = ModoControle.Contagem, int ordem = 1)
    {
        var grupo = GrupoEstoque.Criar(nome, modo, ordem);
        _estoque.Grupos.Add(grupo);
        return grupo;
    }

    private static SalvarItemEstoqueRequest Item(GrupoEstoque grupo, Fornecedor fornecedor, string nome = "Limão", decimal custo = 25.20m) =>
        new(grupo.Id, fornecedor.Id, nome, "Caixa", 28, custo, Ordem: 1, EstoqueAlvoEmbalagens: null, MinimoUnidades: null);

    private static SalvarFornecedorRequest FornecedorRequest(string nome = "Descartáveis Silva", params string[] dias) =>
        new(nome, "(11) 98765-4321", dias.Length == 0 ? ["Thursday"] : dias, 3, false, "WhatsApp", false);

    // ---------- Importação da planilha (RN-ES-10) ----------

    [Fact]
    public async Task Importar_CatalogoDaPlanilha_CriaOs123ItensNos11Grupos()
    {
        var resultado = await _service.ImportarAsync(new ImportarCatalogoRequest(LerCatalogoInicial()), Ct);

        Assert.Empty(resultado.Erros);
        Assert.Empty(resultado.Avisos);
        Assert.False(resultado.Simulacao);
        Assert.Equal(new[] { "Fornecedor principal" }, resultado.FornecedoresNovos.ToArray());
        Assert.Equal(11, resultado.GruposNovos.Count);
        Assert.Equal(123, resultado.ItensNovos);
        Assert.Equal(123, _estoque.Itens.Count);
        Assert.Equal(1, _uow.Confirmacoes);

        // Potes começam na contagem: a venda com sabor (saldo pela venda) só chega na Sprint 7.
        Assert.All(_estoque.Grupos, g => Assert.Equal(ModoControle.Contagem, g.ModoControle));

        // Ordem da planilha: o primeiro grupo e o primeiro sabor dela vêm primeiro.
        var primeiroGrupo = _estoque.Grupos.OrderBy(g => g.Ordem).First();
        Assert.Equal("Picolé de fruta", primeiroGrupo.Nome);
        Assert.Equal("Limão", _estoque.Itens.Where(i => i.GrupoId == primeiroGrupo.Id).OrderBy(i => i.Ordem).First().Nome);

        // Docs/10 §3: mínimo de 2 nos potes; picolé sem mínimo. Fábrica sem meia caixa, pedido impresso.
        var potes1L = _estoque.Grupos.Single(g => g.Nome == "Pote 1L");
        Assert.All(_estoque.Itens.Where(i => i.GrupoId == potes1L.Id), i => Assert.Equal(2, i.MinimoUnidades));
        Assert.All(_estoque.Itens.Where(i => i.GrupoId == primeiroGrupo.Id), i => Assert.Null(i.MinimoUnidades));
        var fabrica = Assert.Single(_estoque.Fornecedores);
        Assert.False(fabrica.AceitaMeiaCaixa);
        Assert.Equal(FormaEnvioPedido.Impresso, fabrica.Envio);
        Assert.Equal(CatalogoEstoqueService.PrazoPadraoDias, fabrica.PrazoEntregaDias);
        Assert.True(fabrica.PedeEm(CatalogoEstoqueService.DiaPedidoPadrao));

        var limao = _estoque.Itens.Single(i => i.GrupoId == primeiroGrupo.Id && i.Nome == "Limão");
        Assert.Equal(UnidadeCompra.Caixa, limao.Unidade);
        Assert.Equal(28, limao.UnidadesPorEmbalagem);
        Assert.Equal(25.20m, limao.CustoEmbalagem);
    }

    [Fact]
    public async Task Importar_MesmoArquivoDuasVezes_NaoDuplicaNada()
    {
        var conteudo = new ImportarCatalogoRequest(LerCatalogoInicial());
        await _service.ImportarAsync(conteudo, Ct);

        var segunda = await _service.ImportarAsync(conteudo, Ct);

        Assert.Empty(segunda.FornecedoresNovos);
        Assert.Empty(segunda.GruposNovos);
        Assert.Equal(0, segunda.ItensNovos);
        Assert.Equal(123, segunda.ItensJaCadastrados);
        Assert.Empty(segunda.Avisos);
        Assert.Equal(123, _estoque.Itens.Count);
        Assert.Equal(1, _uow.Confirmacoes); // nada novo: nem abre transação
    }

    [Fact]
    public async Task Importar_Simulando_MostraOQueSeriaCriadoSemGravar()
    {
        var resultado = await _service.ImportarAsync(new ImportarCatalogoRequest(LerCatalogoInicial(), Simular: true), Ct);

        Assert.True(resultado.Simulacao);
        Assert.Equal(123, resultado.ItensNovos);
        Assert.Equal(11, resultado.GruposNovos.Count);
        Assert.Empty(_estoque.Itens);
        Assert.Empty(_estoque.Grupos);
        Assert.Empty(_estoque.Fornecedores);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Importar_ItemJaCadastradoComOutroCusto_MantemOCatalogoEAvisaADiferenca()
    {
        await _service.ImportarAsync(Csv(Cabecalho, "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,"), Ct);
        var limao = Assert.Single(_estoque.Itens);
        limao.Atualizar(limao.FornecedorId, "Limão", UnidadeCompra.Caixa, 28, 26.40m, ordem: 7, estoqueAlvoEmbalagens: 3m, minimoUnidades: 10);
        limao.Desativar();

        var resultado = await _service.ImportarAsync(Csv(Cabecalho, "picolé de FRUTA,limão,Caixa,24,25.20,Fábrica,,"), Ct);

        Assert.Equal(1, resultado.ItensJaCadastrados);
        Assert.Equal(0, resultado.ItensNovos);
        var aviso = Assert.Single(resultado.Avisos);
        Assert.Equal(2, aviso.Linha);
        Assert.Contains("mantido", aviso.Mensagem);
        Assert.Contains("custo", aviso.Mensagem);
        Assert.Contains("caixa com 24", aviso.Mensagem);
        // O que a dona ajustou no painel fica como está (RN-RC-05: o custo corrigido pela nota não volta).
        Assert.Equal("Limão", limao.Nome);
        Assert.Equal(26.40m, limao.CustoEmbalagem);
        Assert.Equal(28, limao.UnidadesPorEmbalagem);
        Assert.Equal(7, limao.Ordem);
        Assert.Equal(3m, limao.EstoqueAlvoEmbalagens);
        Assert.Equal(10, limao.MinimoUnidades);
        Assert.False(limao.Ativo);
    }

    [Fact]
    public async Task Importar_FornecedorRenomeadoNoPainel_NaoCriaOutroNemTrocaOsItens()
    {
        var conteudo = new ImportarCatalogoRequest(LerCatalogoInicial());
        await _service.ImportarAsync(conteudo, Ct);
        var fabrica = Assert.Single(_estoque.Fornecedores);
        fabrica.Atualizar("Fábrica dos pais", null, [DayOfWeek.Tuesday], 2, false, FormaEnvioPedido.Impresso, true);

        var resultado = await _service.ImportarAsync(conteudo, Ct);

        Assert.Empty(resultado.FornecedoresNovos);
        Assert.Single(_estoque.Fornecedores);
        Assert.All(_estoque.Itens, i => Assert.Equal(fabrica.Id, i.FornecedorId));
        Assert.Equal(123, resultado.Avisos.Count);
        Assert.Contains("Fábrica dos pais", resultado.Avisos[0].Mensagem);
    }

    [Fact]
    public async Task Importar_GrupoRenomeadoNoPainel_APreviaMostraOGrupoQueSeriaCriado()
    {
        await _service.ImportarAsync(Csv(Cabecalho, "Massa (balde),Flocos,Balde,1,89.90,Fábrica,,"), Ct);
        _estoque.Grupos.Single().Renomear("Massas");

        var previa = await _service.ImportarAsync(Csv(Cabecalho, "Massa (balde),Flocos,Balde,1,89.90,Fábrica,,") with { Simular = true }, Ct);

        Assert.Equal(new[] { "Massa (balde)" }, previa.GruposNovos.ToArray());
        Assert.Single(_estoque.Grupos);
        Assert.Single(_estoque.Itens);
    }

    [Fact]
    public async Task Importar_ArquivoSalvoNoExcelBrasileiro_LePontoEVirgulaEVirgulaDecimal()
    {
        var resultado = await _service.ImportarAsync(new ImportarCatalogoRequest(
            "\uFEFFcategoria;sabor;unidade_compra;unidades_por_embalagem;custo_embalagem;fornecedor;origem_na_planilha\r\n" +
            "Picolé ao leite;Chocolate;caixa;28;R$ 1.025,20;Fábrica;\"PICOLÉS!B2; custo\"\r\n" +
            "Massa (balde);Flocos;Balde;1;89,9;Fábrica;\r\n" +
            "Massa (balde);Napolitano;Balde;1;R$ 1.200;Fábrica;\r\n"), Ct);

        Assert.Empty(resultado.Erros);
        Assert.Equal(3, resultado.ItensNovos);
        Assert.Equal(1025.20m, _estoque.Itens.Single(i => i.Nome == "Chocolate").CustoEmbalagem);
        Assert.Equal(89.90m, _estoque.Itens.Single(i => i.Nome == "Flocos").CustoEmbalagem);
        Assert.Equal(1200m, _estoque.Itens.Single(i => i.Nome == "Napolitano").CustoEmbalagem);
    }

    [Theory]
    [InlineData("25.20", "25.20")]
    [InlineData("25,2", "25.20")]
    [InlineData("1.200", "1200")]
    [InlineData("R$ 1.200,00", "1200")]
    [InlineData("1.234.567,89", "1234567.89")]
    [InlineData("30", "30")]
    public void LerDecimal_FormatosAceitos_LeOValorCerto(string texto, string esperado)
    {
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), LeitorCsv.LerDecimal(texto));
    }

    [Theory]
    [InlineData("1,000.00")]
    [InlineData("12.5.3")]
    [InlineData("1.2345")]
    [InlineData("-5")]
    [InlineData("abc")]
    [InlineData("")]
    public void LerDecimal_ValorAmbiguoOuInvalido_DevolveNulo(string texto)
    {
        Assert.Null(LeitorCsv.LerDecimal(texto));
    }

    [Fact]
    public async Task Importar_LinhasComProblema_FicamDeForaComOMotivoEAsOutrasEntram()
    {
        var resultado = await _service.ImportarAsync(Csv(
            Cabecalho,
            "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,",
            "Grupo fantasma,Uva,Saco,1,10.00,Fornecedor fantasma,,",
            "Picolé de fruta,Morango,Caixa,1.000,25.20,Fábrica,,",
            "Picolé de fruta,LIMÃO,Caixa,28,25.20,Fábrica,,",
            "Picolé de fruta,Maracujá,Caixa,28,\"1,000.00\",Fábrica,,",
            "Picolé de fruta,Coco,Balde,28,25.20,Fornecedor fantasma,,",
            "Picolé de fruta,Uva,Caixa,28,25.20,Fábrica,,"), Ct);

        Assert.Equal(2, resultado.ItensNovos);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, resultado.Erros.Select(e => e.Linha).ToArray());
        Assert.Contains("Unidade de compra", resultado.Erros[0].Mensagem);
        Assert.Contains("inteiro", resultado.Erros[1].Mensagem);
        Assert.Contains("repete a linha 2", resultado.Erros[2].Mensagem);
        Assert.Contains("Custo da embalagem", resultado.Erros[3].Mensagem);
        Assert.Contains("caixa e pacote", resultado.Erros[4].Mensagem);

        // A linha errada não deixa grupo nem fornecedor criado pela metade.
        Assert.DoesNotContain(_estoque.Grupos, g => g.Nome == "Grupo fantasma");
        Assert.DoesNotContain(_estoque.Fornecedores, f => f.Nome == "Fornecedor fantasma");
        Assert.DoesNotContain(resultado.FornecedoresNovos, f => f == "Fornecedor fantasma");
        Assert.Equal(new[] { "Limão", "Uva" }, _estoque.Itens.Select(i => i.Nome).ToArray());
    }

    [Fact]
    public async Task Importar_RepeteUmaLinhaRecusada_ImportaARepeticao()
    {
        var resultado = await _service.ImportarAsync(Csv(
            Cabecalho,
            "Picolé de fruta,Limão,Caixa,28,abc,Fábrica,,",
            "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,"), Ct);

        Assert.Equal(2, Assert.Single(resultado.Erros).Linha);
        Assert.Equal(1, resultado.ItensNovos);
    }

    [Fact]
    public async Task Importar_QuebraDeLinhaDentroDeAspas_ContaALinhaComoOExcel()
    {
        var resultado = await _service.ImportarAsync(Csv(
            Cabecalho,
            "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,\"observação",
            "em duas linhas\",",
            "",
            "Picolé de fruta,Uva,Saco,28,25.20,Fábrica,,"), Ct);

        Assert.Equal(1, resultado.ItensNovos);
        Assert.Equal(4, Assert.Single(resultado.Erros).Linha); // cabeçalho 1, Limão 2, linha em branco 3
    }

    [Fact]
    public async Task Importar_AspasNoMeioDoNome_SaoUmCaractereComum()
    {
        var resultado = await _service.ImportarAsync(Csv(
            Cabecalho,
            "Copos,Copo 5\" grande,Pacote,50,12.00,Descartáveis,,",
            "Copos,Copo 6,Pacote,50,13.00,Descartáveis,,",
            "Copos,Copo 7\" largo,Pacote,50,14.00,Descartáveis,,"), Ct);

        Assert.Empty(resultado.Erros);
        Assert.Equal(new[] { "Copo 5\" grande", "Copo 6", "Copo 7\" largo" }, _estoque.Itens.Select(i => i.Nome).ToArray());
    }

    [Fact]
    public async Task Importar_ArquivoQueNaoEUtf8_PedeParaSalvarComoCsvUtf8()
    {
        var erro = await Assert.ThrowsAsync<DomainException>(() =>
            _service.ImportarAsync(Csv(Cabecalho, "Picol\uFFFD de fruta,Lim\uFFFDo,Caixa,28,25.20,F\uFFFDbrica,,"), Ct));

        Assert.Contains("UTF-8", erro.Message);
        Assert.Empty(_estoque.Itens);
    }

    [Fact]
    public async Task Importar_AcentosDecompostos_ReconheceOMesmoNome()
    {
        await _service.ImportarAsync(Csv(Cabecalho, "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,"), Ct);

        // "e" + acento agudo combinado (como alguns programas salvam) em vez de "é".
        var resultado = await _service.ImportarAsync(
            Csv(Cabecalho, "Picole\u0301 de fruta,Lima\u0303o,Caixa,28,25.20,Fa\u0301brica,,"), Ct);

        Assert.Equal(1, resultado.ItensJaCadastrados);
        Assert.Empty(resultado.GruposNovos);
        Assert.Single(_estoque.Itens);
    }

    [Fact]
    public async Task Importar_SemColunaObrigatoria_RecusaOArquivoInteiro()
    {
        var erro = await Assert.ThrowsAsync<DomainException>(() =>
            _service.ImportarAsync(Csv("categoria,sabor,fornecedor", "Picolé de fruta,Limão,Fábrica"), Ct));

        Assert.Contains("unidade_compra", erro.Message);
        Assert.Empty(_estoque.Itens);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Theory]
    [InlineData("categoria,sabor,unidade_compra,unidades_por_embalagem,custo_embalagem,fornecedor\n\n")]
    [InlineData(",,,\n,,,")]
    [InlineData("   ")]
    public async Task Importar_SemItens_Lanca(string conteudo)
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.ImportarAsync(new ImportarCatalogoRequest(conteudo), Ct));
    }

    [Fact]
    public async Task Importar_FornecedorDesativado_RecusaALinha()
    {
        Fabrica(ativo: false);

        var resultado = await _service.ImportarAsync(Csv(Cabecalho, "Picolé de fruta,Limão,Caixa,28,25.20,fábrica,,"), Ct);

        Assert.Contains("desativado", Assert.Single(resultado.Erros).Mensagem);
        Assert.Empty(_estoque.Itens);
        Assert.Empty(_estoque.Grupos);
    }

    [Fact]
    public async Task Importar_GrupoQueJaExiste_MantemOModoQueADonaEscolheu()
    {
        Grupo("Pote 1L", ModoControle.SaldoPelaVenda);

        var resultado = await _service.ImportarAsync(Csv(Cabecalho, "Pote 1L,Flocos,Unidade,1,18.00,Fábrica,,"), Ct);

        Assert.Empty(resultado.GruposNovos);
        Assert.Equal(ModoControle.SaldoPelaVenda, Assert.Single(_estoque.Grupos).ModoControle);
        Assert.Equal(CatalogoEstoqueService.MinimoPadraoPotes, Assert.Single(_estoque.Itens).MinimoUnidades);
    }

    // ---------- Itens ----------

    [Fact]
    public async Task CriarItem_MesmoNomeNoMesmoGrupo_Lanca()
    {
        var fabrica = Fabrica();
        var frutas = Grupo("Picolé de fruta");
        await _service.CriarItemAsync(Item(frutas, fabrica), Ct);

        var erro = await Assert.ThrowsAsync<DomainException>(() => _service.CriarItemAsync(Item(frutas, fabrica, " LIMÃO "), Ct));

        Assert.Contains("Picolé de fruta", erro.Message);
    }

    [Fact]
    public async Task CriarItem_MesmoNomeEmOutroGrupo_Aceita()
    {
        var fabrica = Fabrica();
        await _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), fabrica, "Morango"), Ct);

        var dto = await _service.CriarItemAsync(Item(Grupo("Pote 1L", ModoControle.SaldoPelaVenda, 2), fabrica, "Morango") with
        {
            Unidade = "Unidade",
            UnidadesPorEmbalagem = 1,
        }, Ct);

        Assert.Equal("Pote 1L", dto.GrupoNome);
        Assert.Equal("Fábrica", dto.FornecedorNome);
        Assert.Equal(2, _estoque.Itens.Count);
    }

    [Fact]
    public async Task CriarItem_FornecedorDesativado_Lanca()
    {
        var erro = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), Fabrica(ativo: false)), Ct));

        Assert.Contains("desativado", erro.Message);
    }

    [Fact]
    public async Task CriarItem_GrupoInexistente_Lanca()
    {
        var request = Item(Grupo("Picolé de fruta"), Fabrica()) with { GrupoId = Guid.NewGuid() };

        await Assert.ThrowsAsync<DomainException>(() => _service.CriarItemAsync(request, Ct));
    }

    [Fact]
    public async Task AtualizarItem_FornecedorAtualDesativado_AindaDeixaCorrigirOCusto()
    {
        var fabrica = Fabrica();
        var grupo = Grupo("Picolé de fruta");
        var criado = await _service.CriarItemAsync(Item(grupo, fabrica), Ct);
        fabrica.Desativar();

        var dto = await _service.AtualizarItemAsync(criado.Id, Item(grupo, fabrica, custo: 27m), Ct);

        Assert.Equal(27m, dto.CustoEmbalagem);
    }

    [Fact]
    public async Task AtualizarItem_OutroGrupo_MoveOItem()
    {
        var fabrica = Fabrica();
        var frutas = Grupo("Picolé de fruta");
        var leite = Grupo("Picolé ao leite", ordem: 2);
        var criado = await _service.CriarItemAsync(Item(frutas, fabrica, "Coco"), Ct);

        var dto = await _service.AtualizarItemAsync(criado.Id, Item(leite, fabrica, "Coco"), Ct);

        Assert.Equal(leite.Id, dto.GrupoId);
        Assert.Equal("Picolé ao leite", dto.GrupoNome);
        Assert.Equal(leite.Id, Assert.Single(_estoque.Itens).GrupoId);
    }

    [Fact]
    public async Task AtualizarItem_Inexistente_LancaNaoEncontrado()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.AtualizarItemAsync(Guid.NewGuid(), Item(Grupo("Picolé de fruta"), Fabrica()), Ct));
    }

    [Fact]
    public async Task DesativarItem_ItemAtivo_MarcaInativoSemApagar()
    {
        var criado = await _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), Fabrica()), Ct);

        var dto = await _service.DesativarItemAsync(criado.Id, Ct);

        Assert.False(dto.Ativo);
        Assert.Equal("Picolé de fruta", dto.GrupoNome);
        Assert.Single(_estoque.Itens);
    }

    [Fact]
    public async Task AtivarItem_FornecedorDesativado_Lanca()
    {
        var fabrica = Fabrica();
        var item = await _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), fabrica), Ct);
        await _service.DesativarItemAsync(item.Id, Ct);
        fabrica.Desativar();

        var erro = await Assert.ThrowsAsync<DomainException>(() => _service.AtivarItemAsync(item.Id, Ct));

        Assert.Contains("desativado", erro.Message);
        Assert.False(Assert.Single(_estoque.Itens).Ativo);
    }

    [Fact]
    public async Task CriarItem_GrupoDePotesSemMinimo_UsaOMinimoPadrao()
    {
        var fabrica = Fabrica();
        var request = Item(Grupo("Pote 2L"), fabrica, "Flocos") with { Unidade = "Unidade", UnidadesPorEmbalagem = 1 };

        var dto = await _service.CriarItemAsync(request, Ct);
        var comMinimo = await _service.CriarItemAsync(request with { Nome = "Ninho", MinimoUnidades = 5 }, Ct);
        var picole = await _service.CriarItemAsync(Item(Grupo("Picolé de fruta", ordem: 2), fabrica), Ct);

        Assert.Equal(CatalogoEstoqueService.MinimoPadraoPotes, dto.MinimoUnidades);
        Assert.Equal(5, comMinimo.MinimoUnidades);
        Assert.Null(picole.MinimoUnidades);
    }

    // ---------- Fornecedores ----------

    [Fact]
    public async Task CriarFornecedor_DiasEmTexto_GuardaOrdenadosEOWhatsAppSoComDigitos()
    {
        var dto = await _service.CriarFornecedorAsync(FornecedorRequest("Descartáveis Silva", "Thursday", "monday", "Thursday"), Ct);

        Assert.Equal(new[] { "Monday", "Thursday" }, dto.DiasPedido.ToArray());
        Assert.Equal("11987654321", dto.WhatsApp);
        Assert.Equal("WhatsApp", dto.Envio);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task CriarFornecedor_DiaDesconhecido_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.CriarFornecedorAsync(FornecedorRequest("Silva", "Segunda"), Ct));
        Assert.Empty(_estoque.Fornecedores);
    }

    [Fact]
    public async Task CriarFornecedor_NomeRepetido_Lanca()
    {
        Fabrica();

        await Assert.ThrowsAsync<DomainException>(() => _service.CriarFornecedorAsync(FornecedorRequest(" FÁBRICA "), Ct));
    }

    [Fact]
    public async Task DesativarFornecedor_ComItensAtivos_PedeParaTrocarOsItensAntes()
    {
        var fabrica = Fabrica();
        await _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), fabrica), Ct);

        var erro = await Assert.ThrowsAsync<DomainException>(() => _service.DesativarFornecedorAsync(fabrica.Id, Ct));

        Assert.Contains("1 item ativo", erro.Message);
        Assert.True(fabrica.Ativo);
    }

    [Fact]
    public async Task DesativarFornecedor_SoComItensInativos_Desativa()
    {
        var fabrica = Fabrica();
        var item = await _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), fabrica), Ct);
        await _service.DesativarItemAsync(item.Id, Ct);

        var dto = await _service.DesativarFornecedorAsync(fabrica.Id, Ct);

        Assert.False(dto.Ativo);
    }

    // ---------- Grupos ----------

    [Fact]
    public async Task AtualizarGrupo_PotesDeSaldoParaContagem_TrocaOModo()
    {
        var potes = Grupo("Pote 1L", ModoControle.SaldoPelaVenda);

        var dto = await _service.AtualizarGrupoAsync(potes.Id, new SalvarGrupoEstoqueRequest("Pote 1L", "Contagem", 3), Ct);

        Assert.Equal("Contagem", dto.ModoControle);
        Assert.Equal(3, dto.Ordem);
    }

    [Fact]
    public async Task CriarGrupo_ModoDesconhecido_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarGrupoAsync(new SalvarGrupoEstoqueRequest("Copos", "Automatico", 1), Ct));
    }

    [Fact]
    public async Task CriarGrupo_NomeRepetido_Lanca()
    {
        Grupo("Copos");

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarGrupoAsync(new SalvarGrupoEstoqueRequest("copos", "SoAviso", 2), Ct));
    }

    // ---------- Catálogo ----------

    [Fact]
    public async Task ObterCatalogo_CatalogoImportado_TrazNomesDoGrupoEDoFornecedorNaOrdemDaPlanilha()
    {
        await _service.ImportarAsync(Csv(
            Cabecalho,
            "Picolé ao leite,Chocolate,Caixa,28,25.20,Fábrica,,",
            "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,",
            "Picolé ao leite,Ninho,Caixa,28,25.20,Fábrica,,"), Ct);

        var catalogo = await _service.ObterCatalogoAsync(Ct);

        Assert.Equal(new[] { "Picolé ao leite", "Picolé de fruta" }, catalogo.Grupos.Select(g => g.Nome).ToArray());
        Assert.Equal(new[] { "Chocolate", "Ninho", "Limão" }, catalogo.Itens.Select(i => i.Nome).ToArray());
        Assert.All(catalogo.Itens, i => Assert.Equal("Fábrica", i.FornecedorNome));
        Assert.Equal("Contagem", catalogo.Grupos[0].ModoControle);
    }

    [Fact]
    public async Task ObterCatalogoApp_CatalogoImportado_TrazGruposEItensSemCusto()
    {
        await _service.ImportarAsync(Csv(Cabecalho, "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,"), Ct);

        var catalogo = await _service.ObterCatalogoAppAsync(Ct);

        var item = Assert.Single(catalogo.Itens);
        Assert.Equal("Limão", item.Nome);
        Assert.Equal("Caixa", item.Unidade);
        Assert.Equal(28, item.UnidadesPorEmbalagem);
        Assert.Equal(item.GrupoId, Assert.Single(catalogo.Grupos).Id);
        // Fornecedor criado pela importação pede na segunda: a contagem é no domingo (RN-ES-05).
        Assert.Equal(new[] { "Sunday" }, catalogo.DiasDeContagem.ToArray());
        // O DTO do app não tem propriedade de custo nem de fornecedor.
        Assert.DoesNotContain(typeof(GestaoSorveteria.Contracts.Estoque.ItemEstoqueAppDto).GetProperties(),
            p => p.Name.Contains("Custo") || p.Name.Contains("Fornecedor"));
    }

    /// <summary>O CSV de verdade (docs/fase2): sobe as pastas a partir do binário ou da pasta atual até achar o repositório.</summary>
    private static string LerCatalogoInicial()
    {
        foreach (var inicio in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var pasta = new DirectoryInfo(inicio); pasta is not null; pasta = pasta.Parent)
            {
                var caminho = Path.Combine(pasta.FullName, "docs", "fase2", "catalogo-inicial.csv");
                if (File.Exists(caminho))
                {
                    return File.ReadAllText(caminho);
                }
            }
        }

        throw new FileNotFoundException("docs/fase2/catalogo-inicial.csv não encontrado a partir da pasta do teste.");
    }
}
