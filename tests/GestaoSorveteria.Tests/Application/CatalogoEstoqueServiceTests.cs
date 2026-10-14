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

    // ---------- Importação da planilha ----------

    [Fact]
    public async Task Importar_CatalogoDaPlanilha_CriaOs123ItensNos11GruposComOsModosCertos()
    {
        var resultado = await _service.ImportarAsync(new ImportarCatalogoRequest(LerCatalogoInicial()), Ct);

        Assert.Empty(resultado.Erros);
        Assert.Equal(1, resultado.FornecedoresCriados);
        Assert.Equal(11, resultado.GruposCriados);
        Assert.Equal(123, resultado.ItensCriados);
        Assert.Equal(1, _uow.Confirmacoes);

        // RN-ES-02: potes em teste de saldo pela venda; o resto na contagem semanal.
        Assert.Equal(ModoControle.Contagem, _estoque.Grupos.Single(g => g.Nome == "Picolé de fruta").ModoControle);
        Assert.Equal(ModoControle.Contagem, _estoque.Grupos.Single(g => g.Nome == "Massa (balde)").ModoControle);
        Assert.Equal(ModoControle.SaldoPelaVenda, _estoque.Grupos.Single(g => g.Nome == "Pote 1L").ModoControle);
        Assert.Equal(ModoControle.SaldoPelaVenda, _estoque.Grupos.Single(g => g.Nome == "Pote de açaí").ModoControle);

        // Ordem da planilha: o primeiro grupo e o primeiro sabor dela vêm primeiro.
        var primeiroGrupo = _estoque.Grupos.OrderBy(g => g.Ordem).First();
        Assert.Equal("Picolé de fruta", primeiroGrupo.Nome);
        Assert.Equal("Limão", _estoque.Itens.Where(i => i.GrupoId == primeiroGrupo.Id).OrderBy(i => i.Ordem).First().Nome);

        // Docs/10 §3: mínimo de 2 nos potes; picolé sem mínimo. Fábrica sem meia caixa, pedido impresso.
        Assert.All(_estoque.Itens.Where(i => i.GrupoId == _estoque.Grupos.Single(g => g.Nome == "Pote 1L").Id),
            i => Assert.Equal(2, i.MinimoUnidades));
        Assert.All(_estoque.Itens.Where(i => i.GrupoId == primeiroGrupo.Id), i => Assert.Null(i.MinimoUnidades));
        var fabrica = Assert.Single(_estoque.Fornecedores);
        Assert.Equal("Fornecedor principal", fabrica.Nome);
        Assert.False(fabrica.AceitaMeiaCaixa);
        Assert.Equal(FormaEnvioPedido.Impresso, fabrica.Envio);
        Assert.Equal(CatalogoEstoqueService.PrazoPadraoDias, fabrica.PrazoEntregaDias);

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

        Assert.Equal(0, segunda.FornecedoresCriados);
        Assert.Equal(0, segunda.GruposCriados);
        Assert.Equal(0, segunda.ItensCriados);
        Assert.Equal(0, segunda.ItensAtualizados);
        Assert.Equal(123, segunda.ItensSemMudanca);
        Assert.Equal(123, _estoque.Itens.Count);
        Assert.Equal(1, _uow.Confirmacoes); // nada mudou: nem abre transação
    }

    [Fact]
    public async Task Importar_CustoMudouNaPlanilha_AtualizaEMantemOQueADonaAjustouNoPainel()
    {
        await _service.ImportarAsync(Csv(Cabecalho, "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,"), Ct);
        var limao = Assert.Single(_estoque.Itens);
        limao.Atualizar(limao.FornecedorId, limao.Nome, limao.Unidade, 28, 25.20m, ordem: 7, estoqueAlvoEmbalagens: 3m, minimoUnidades: 10);
        limao.Desativar();

        var resultado = await _service.ImportarAsync(Csv(Cabecalho, "picolé de FRUTA,limão,Caixa,28,26.40,Fábrica,,"), Ct);

        Assert.Equal(1, resultado.ItensAtualizados);
        Assert.Equal(0, resultado.GruposCriados);
        Assert.Equal(26.40m, limao.CustoEmbalagem);
        Assert.Equal("limão", limao.Nome);
        Assert.Equal(7, limao.Ordem);
        Assert.Equal(3m, limao.EstoqueAlvoEmbalagens);
        Assert.Equal(10, limao.MinimoUnidades);
        Assert.False(limao.Ativo); // reimportar não reativa o que a dona desativou
    }

    [Fact]
    public async Task Importar_ArquivoSalvoNoExcelBrasileiro_LePontoEVirgulaEVirgulaDecimal()
    {
        var resultado = await _service.ImportarAsync(new ImportarCatalogoRequest(
            "﻿categoria;sabor;unidade_compra;unidades_por_embalagem;custo_embalagem;fornecedor;origem_na_planilha\r\n" +
            "Picolé ao leite;Chocolate;caixa;28;R$ 1.025,20;Fábrica;\"PICOLÉS!B2; custo\"\r\n" +
            "Massa (balde);Flocos;Balde;1;89,9;Fábrica;\r\n"), Ct);

        Assert.Empty(resultado.Erros);
        Assert.Equal(2, resultado.ItensCriados);
        Assert.Equal(1025.20m, _estoque.Itens.Single(i => i.Nome == "Chocolate").CustoEmbalagem);
        Assert.Equal(89.90m, _estoque.Itens.Single(i => i.Nome == "Flocos").CustoEmbalagem);
    }

    [Fact]
    public async Task Importar_LinhasComProblema_FicamDeForaComOMotivoEAsOutrasEntram()
    {
        var resultado = await _service.ImportarAsync(Csv(
            Cabecalho,
            "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,,",
            "Grupo fantasma,Uva,Saco,1,10.00,Fornecedor fantasma,,",
            "Picolé de fruta,Morango,Caixa,vinte,25.20,Fábrica,,",
            "Picolé de fruta,LIMÃO,Caixa,28,25.20,Fábrica,,",
            "Picolé de fruta,Maracujá,Caixa,28,abc,Fábrica,,",
            "Picolé de fruta,Coco,Balde,28,25.20,Fábrica,,",
            "Picolé de fruta,Uva,Caixa,28,25.20,Fábrica,,"), Ct);

        Assert.Equal(2, resultado.ItensCriados);
        Assert.Equal(new[] { 3, 4, 5, 6, 7 }, resultado.Erros.Select(e => e.Linha).ToArray());
        Assert.Contains("Unidade de compra", resultado.Erros[0].Motivo);
        Assert.Contains("inteiro", resultado.Erros[1].Motivo);
        Assert.Contains("repete a linha 2", resultado.Erros[2].Motivo);
        Assert.Contains("Custo da embalagem", resultado.Erros[3].Motivo);
        Assert.Contains("caixa e pacote", resultado.Erros[4].Motivo);

        // A linha errada não deixa grupo nem fornecedor criado pela metade.
        Assert.DoesNotContain(_estoque.Grupos, g => g.Nome == "Grupo fantasma");
        Assert.DoesNotContain(_estoque.Fornecedores, f => f.Nome == "Fornecedor fantasma");
        Assert.Equal(new[] { "Limão", "Uva" }, _estoque.Itens.Select(i => i.Nome).ToArray());
    }

    [Fact]
    public async Task Importar_QuebraDeLinhaDentroDeAspas_ContaALinhaComoOExcel()
    {
        var resultado = await _service.ImportarAsync(Csv(
            Cabecalho,
            "Picolé de fruta,Limão,Caixa,28,25.20,Fábrica,\"observação",
            "em duas linhas\",",
            "Picolé de fruta,Uva,Saco,28,25.20,Fábrica,,"), Ct);

        Assert.Equal(1, resultado.ItensCriados);
        Assert.Equal(4, Assert.Single(resultado.Erros).Linha);
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

    [Fact]
    public async Task Importar_SoCabecalho_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.ImportarAsync(Csv(Cabecalho, "", ""), Ct));
    }

    [Fact]
    public async Task Importar_FornecedorDesativado_RecusaALinha()
    {
        Fabrica(ativo: false);

        var resultado = await _service.ImportarAsync(Csv(Cabecalho, "Picolé de fruta,Limão,Caixa,28,25.20,fábrica,,"), Ct);

        Assert.Contains("desativado", Assert.Single(resultado.Erros).Motivo);
        Assert.Empty(_estoque.Itens);
    }

    [Fact]
    public async Task Importar_GrupoQueJaExiste_MantemOModoQueADonaEscolheu()
    {
        // A dona voltou os potes para a contagem (RN-ES-02); reimportar não desfaz.
        Grupo("Pote 1L", ModoControle.Contagem);

        await _service.ImportarAsync(Csv(Cabecalho, "Pote 1L,Flocos,Unidade,1,18.00,Fábrica,,"), Ct);

        Assert.Equal(ModoControle.Contagem, Assert.Single(_estoque.Grupos).ModoControle);
        Assert.Null(Assert.Single(_estoque.Itens).MinimoUnidades); // mínimo padrão é só para grupo de saldo pela venda
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
    public async Task DesativarItem_MarcaInativoSemApagar()
    {
        var criado = await _service.CriarItemAsync(Item(Grupo("Picolé de fruta"), Fabrica()), Ct);

        var dto = await _service.DesativarItemAsync(criado.Id, Ct);

        Assert.False(dto.Ativo);
        Assert.Equal("Picolé de fruta", dto.GrupoNome);
        Assert.Single(_estoque.Itens);
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
    public async Task AtualizarGrupo_PotesVoltamParaContagem()
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
    public async Task ObterCatalogo_ItensVemComNomeDoGrupoEDoFornecedorNaOrdemDaPlanilha()
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
