using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Estoque;

/// <summary>
/// Catálogo de estoque da Fase 2 (RN-ES-01/02, RN-PF-01): fornecedores, grupos, itens e a importação da planilha da dona.
/// Só Admin chama (RN-RC-07, garantido no controller). Nada é apagado: fornecedor e item são desativados.
/// </summary>
public sealed class CatalogoEstoqueService
{
    /// <summary>Docs/10 §3, dúvida 1: fábrica pede uma vez por semana e entrega em 2 dias. A dona ajusta no painel.</summary>
    public const int PrazoPadraoDias = 2;

    /// <summary>Docs/10 §3, dúvida 7: mínimo de 2 unidades para cada pote (na importação e no item novo sem mínimo informado).</summary>
    public const int MinimoPadraoPotes = 2;

    /// <summary>Dia de pedido do fornecedor que nasce pela importação, até a dona escolher o dela (docs/10 §3, dúvida 1).</summary>
    public const DayOfWeek DiaPedidoPadrao = DayOfWeek.Monday;

    public const int LinhasMaximasImportacao = 2000;

    private static readonly string[] ColunasObrigatorias =
        ["categoria", "sabor", "unidade_compra", "unidades_por_embalagem", "custo_embalagem", "fornecedor"];

    private readonly IEstoqueRepository _estoque;
    private readonly IProdutoRepository _produtos;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public CatalogoEstoqueService(IEstoqueRepository estoque, IProdutoRepository produtos, IUnitOfWork uow, IClock clock)
    {
        _estoque = estoque;
        _produtos = produtos;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Tudo de uma vez: o catálogo é pequeno (centenas de itens) e o painel e o app montam as telas a partir dele.</summary>
    public async Task<CatalogoEstoqueResponse> ObterCatalogoAsync(CancellationToken cancellationToken = default)
    {
        var fornecedores = await _estoque.ListarFornecedoresAsync(cancellationToken);
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        var itens = await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken);

        var grupoPorId = grupos.ToDictionary(g => g.Id);
        var fornecedorPorId = fornecedores.ToDictionary(f => f.Id);

        return new CatalogoEstoqueResponse(
            fornecedores.OrderBy(f => f.Nome, StringComparer.CurrentCultureIgnoreCase).Select(f => f.ToDto()).ToList(),
            grupos.Select(g => g.ToDto()).ToList(),
            itens
                .Select(i => i.ToDto(grupoPorId.GetValueOrDefault(i.GrupoId), fornecedorPorId.GetValueOrDefault(i.FornecedorId)))
                .ToList());
    }

    /// <summary>Para o app do atendente: grupos e itens sem custo nem fornecedor ("Está acabando" e contagem).</summary>
    public async Task<CatalogoEstoqueAppResponse> ObterCatalogoAppAsync(CancellationToken cancellationToken = default)
    {
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        var itens = await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken);
        var fornecedores = await _estoque.ListarFornecedoresAsync(cancellationToken);

        // RN-ES-05: véspera do dia de pedido de quem fornece algum item ativo que entra na contagem.
        var gruposContados = grupos.Where(g => g.EntraNaContagem).Select(g => g.Id).ToHashSet();
        var fornecedoresContados = itens.Where(i => i.Ativo && gruposContados.Contains(i.GrupoId)).Select(i => i.FornecedorId).ToHashSet();
        var diasDeContagem = fornecedores
            .Where(f => f.Ativo && fornecedoresContados.Contains(f.Id))
            .SelectMany(f => f.DiasPedido)
            .Select(d => (DayOfWeek)(((int)d + 6) % 7))
            .Distinct()
            .Order()
            .Select(d => d.ToString())
            .ToList();

        return new CatalogoEstoqueAppResponse(
            grupos.Select(g => g.ToDto()).ToList(),
            itens.Select(i => new ItemEstoqueAppDto(i.Id, i.GrupoId, i.Nome, i.Unidade.ToString(), i.UnidadesPorEmbalagem, i.Ordem, i.Ativo)).ToList(),
            diasDeContagem);
    }

    // ---------- Fornecedores (RN-PF-01) ----------

    public async Task<FornecedorDto> CriarFornecedorAsync(SalvarFornecedorRequest request, CancellationToken cancellationToken = default)
    {
        await GarantirNomeDeFornecedorLivreAsync(request.Nome, ignorarId: null, cancellationToken);

        var fornecedor = Fornecedor.Criar(
            request.Nome,
            request.WhatsApp,
            LerDias(request.DiasPedido),
            request.PrazoEntregaDias,
            request.AceitaMeiaCaixa,
            TextoEnum.Converter<FormaEnvioPedido>(request.Envio, "Forma de envio"),
            request.MostrarPrecos);

        await _estoque.AdicionarFornecedorAsync(fornecedor, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return fornecedor.ToDto();
    }

    public async Task<FornecedorDto> AtualizarFornecedorAsync(Guid id, SalvarFornecedorRequest request, CancellationToken cancellationToken = default)
    {
        var fornecedor = await CarregarFornecedorAsync(id, cancellationToken);
        await GarantirNomeDeFornecedorLivreAsync(request.Nome, ignorarId: id, cancellationToken);

        fornecedor.Atualizar(
            request.Nome,
            request.WhatsApp,
            LerDias(request.DiasPedido),
            request.PrazoEntregaDias,
            request.AceitaMeiaCaixa,
            TextoEnum.Converter<FormaEnvioPedido>(request.Envio, "Forma de envio"),
            request.MostrarPrecos);

        await _uow.SaveChangesAsync(cancellationToken);
        return fornecedor.ToDto();
    }

    public async Task<FornecedorDto> AtivarFornecedorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fornecedor = await CarregarFornecedorAsync(id, cancellationToken);
        fornecedor.Ativar();
        await _uow.SaveChangesAsync(cancellationToken);
        return fornecedor.ToDto();
    }

    /// <summary>
    /// Fornecedor com itens ativos não é desativado: os itens sumiriam do pedido sem ninguém perceber.
    /// A dona troca o fornecedor desses itens (ou desativa os itens) antes.
    /// </summary>
    public async Task<FornecedorDto> DesativarFornecedorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fornecedor = await CarregarFornecedorAsync(id, cancellationToken);
        var ativos = (await _estoque.ListarItensAsync(somenteAtivos: true, cancellationToken)).Count(i => i.FornecedorId == id);
        if (ativos > 0)
        {
            throw new DomainException(
                $"{fornecedor.Nome} ainda fornece {ativos} {(ativos == 1 ? "item ativo" : "itens ativos")}. Troque o fornecedor deles ou desative os itens antes.");
        }

        fornecedor.Desativar();
        await _uow.SaveChangesAsync(cancellationToken);
        return fornecedor.ToDto();
    }

    // ---------- Grupos (RN-ES-02) ----------

    public async Task<GrupoEstoqueDto> CriarGrupoAsync(SalvarGrupoEstoqueRequest request, CancellationToken cancellationToken = default)
    {
        await GarantirNomeDeGrupoLivreAsync(request.Nome, ignorarId: null, cancellationToken);

        var grupo = GrupoEstoque.Criar(request.Nome, TextoEnum.Converter<ModoControle>(request.ModoControle, "Modo de controle"), request.Ordem);
        await _estoque.AdicionarGrupoAsync(grupo, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return grupo.ToDto();
    }

    /// <summary>RN-ES-02: a dona pode trocar o modo (ex.: potes voltam para contagem se o teste não der certo).</summary>
    public async Task<GrupoEstoqueDto> AtualizarGrupoAsync(Guid id, SalvarGrupoEstoqueRequest request, CancellationToken cancellationToken = default)
    {
        var grupo = await _estoque.ObterGrupoAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Grupo de estoque", id);
        await GarantirNomeDeGrupoLivreAsync(request.Nome, ignorarId: id, cancellationToken);

        var modo = TextoEnum.Converter<ModoControle>(request.ModoControle, "Modo de controle");
        if (modo != grupo.ModoControle)
        {
            // RN-PR-06: o app decide se pede sabor pelo modo do grupo — os produtos ligados descem de novo para ele.
            var ligados = await _produtos.ListarDoGrupoEstoqueAsync(grupo.Id, cancellationToken);
            Exigir.Que(modo != ModoControle.SoAviso || ligados.Count == 0,
                $"Há produto de venda ligado a {grupo.Nome} ({string.Join(", ", ligados.Select(p => p.Nome))}): desligue no cadastro do produto antes de deixar o grupo só com aviso.");
            foreach (var produto in ligados)
            {
                produto.GrupoDeEstoqueMudou(_clock.UtcNow);
            }
        }

        grupo.Renomear(request.Nome);
        grupo.AlterarModo(modo);
        grupo.AlterarOrdem(request.Ordem);
        await _uow.SaveChangesAsync(cancellationToken);
        return grupo.ToDto();
    }

    // ---------- Itens (RN-ES-01) ----------

    public async Task<ItemEstoqueDto> CriarItemAsync(SalvarItemEstoqueRequest request, CancellationToken cancellationToken = default)
    {
        var (grupo, fornecedor) = await CarregarGrupoEFornecedorAsync(request, fornecedorAtual: null, cancellationToken);
        await GarantirNomeDeItemLivreAsync(grupo, request.Nome, ignorarId: null, cancellationToken);

        var item = ItemEstoque.Criar(
            grupo.Id,
            fornecedor.Id,
            request.Nome,
            TextoEnum.Converter<UnidadeCompra>(request.Unidade, "Unidade de compra"),
            request.UnidadesPorEmbalagem,
            request.CustoEmbalagem,
            request.Ordem,
            request.EstoqueAlvoEmbalagens,
            request.MinimoUnidades ?? (EhGrupoDePotes(grupo) ? MinimoPadraoPotes : null));

        await _estoque.AdicionarItemAsync(item, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return item.ToDto(grupo, fornecedor);
    }

    public async Task<ItemEstoqueDto> AtualizarItemAsync(Guid id, SalvarItemEstoqueRequest request, CancellationToken cancellationToken = default)
    {
        var item = await CarregarItemAsync(id, cancellationToken);
        var (grupo, fornecedor) = await CarregarGrupoEFornecedorAsync(request, fornecedorAtual: item.FornecedorId, cancellationToken);
        await GarantirNomeDeItemLivreAsync(grupo, request.Nome, ignorarId: id, cancellationToken);

        item.MoverParaGrupo(grupo.Id);
        item.Atualizar(
            fornecedor.Id,
            request.Nome,
            TextoEnum.Converter<UnidadeCompra>(request.Unidade, "Unidade de compra"),
            request.UnidadesPorEmbalagem,
            request.CustoEmbalagem,
            request.Ordem,
            request.EstoqueAlvoEmbalagens,
            request.MinimoUnidades);

        await _uow.SaveChangesAsync(cancellationToken);
        return item.ToDto(grupo, fornecedor);
    }

    public Task<ItemEstoqueDto> AtivarItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        AlterarSituacaoDoItemAsync(id, ativo: true, cancellationToken);

    /// <summary>Item que a fábrica parou de fazer sai da contagem e do pedido, mas o histórico continua (nada é apagado).</summary>
    public Task<ItemEstoqueDto> DesativarItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        AlterarSituacaoDoItemAsync(id, ativo: false, cancellationToken);

    // ---------- Importação da planilha (docs/fase2/catalogo-inicial.csv, RN-ES-10) ----------

    /// <summary>
    /// RN-ES-10. Importa o catálogo no formato de <c>docs/fase2/catalogo-inicial.csv</c> criando só o que falta:
    /// fornecedor e grupo são achados pelo nome, item por grupo + sabor (RN-ES-01). O que já está no catálogo nunca é alterado
    /// (o painel é quem manda depois da primeira carga); a diferença vira aviso na linha. Reimportar não duplica.
    /// Fornecedor novo nasce com os padrões de docs/10 §3; grupo novo nasce em <see cref="ModoControle.Contagem"/>
    /// (os potes passam para saldo pela venda quando a venda com sabor chegar, Sprint 7). Linha com problema fica de fora
    /// com o motivo e não deixa nada criado pela metade. Com <see cref="ImportarCatalogoRequest.Simular"/>, nada é gravado.
    /// </summary>
    public async Task<ImportarCatalogoResponse> ImportarAsync(ImportarCatalogoRequest request, CancellationToken cancellationToken = default)
    {
        var linhas = LeitorCsv.Ler(request.Conteudo);
        var coluna = MapearColunas(linhas[0]);
        var dados = linhas.Skip(1).ToList();
        Exigir.Que(dados.Count > 0, "O arquivo só tem o cabeçalho; nenhum item para importar.");
        Exigir.Que(dados.Count <= LinhasMaximasImportacao,
            $"O arquivo tem {dados.Count} linhas; o máximo é {LinhasMaximasImportacao} por importação.");

        var fornecedores = (await _estoque.ListarFornecedoresAsync(cancellationToken)).ToDictionary(f => f.NomeNormalizado);
        var grupos = (await _estoque.ListarGruposAsync(cancellationToken)).ToDictionary(g => g.NomeNormalizado);
        var itens = (await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken))
            .ToDictionary(i => (i.GrupoId, i.NomeNormalizado));
        var fornecedorPorId = fornecedores.Values.ToDictionary(f => f.Id);

        var proximaOrdemGrupo = grupos.Count == 0 ? 1 : grupos.Values.Max(g => g.Ordem) + 1;
        var proximaOrdemItem = itens.Values.GroupBy(i => i.GrupoId).ToDictionary(g => g.Key, g => g.Max(i => i.Ordem) + 1);
        var linhaDoItem = new Dictionary<(string Grupo, string Item), int>();

        var fornecedoresNovos = new List<Fornecedor>();
        var gruposNovos = new List<GrupoEstoque>();
        var itensNovos = new List<ItemEstoque>();
        var jaCadastrados = 0;
        var avisos = new List<LinhaImportacaoDto>();
        var erros = new List<LinhaImportacaoDto>();

        foreach (var linha in dados)
        {
            try
            {
                var categoria = Exigir.Texto(Campo(linha, coluna, "categoria"), "Categoria", 2, 40);
                var sabor = Exigir.Texto(Campo(linha, coluna, "sabor"), "Sabor", 1, 80);
                var nomeFornecedor = Exigir.Texto(Campo(linha, coluna, "fornecedor"), "Fornecedor", 2, 80);
                var unidade = TextoEnum.Converter<UnidadeCompra>(Campo(linha, coluna, "unidade_compra"), "Unidade de compra");
                var porEmbalagem = LerInteiro(Campo(linha, coluna, "unidades_por_embalagem"), "Unidades por embalagem");
                var custo = LerDinheiro(Campo(linha, coluna, "custo_embalagem"));

                var chave = (NomeEstoque.Normalizar(categoria), NomeEstoque.Normalizar(sabor));
                if (linhaDoItem.TryGetValue(chave, out var primeira))
                {
                    throw new DomainException($"{sabor} ({categoria}) repete a linha {primeira}.");
                }

                grupos.TryGetValue(chave.Item1, out var grupo);
                fornecedores.TryGetValue(NomeEstoque.Normalizar(nomeFornecedor), out var fornecedor);

                if (grupo is not null && itens.TryGetValue((grupo.Id, chave.Item2), out var existente))
                {
                    // Já cadastrado: o catálogo manda. Só avisa o que a planilha traz de diferente.
                    var diferencas = Diferencas(existente, unidade, porEmbalagem, custo, nomeFornecedor, fornecedorPorId);
                    if (diferencas.Count > 0)
                    {
                        avisos.Add(new LinhaImportacaoDto(linha.Numero,
                            $"{existente.Nome} ({grupo.Nome}) já está no catálogo e foi mantido. Na planilha: {string.Join("; ", diferencas)}."));
                    }

                    jaCadastrados++;
                    linhaDoItem[chave] = linha.Numero;
                    continue;
                }

                Exigir.Que(fornecedor is null || fornecedor.Ativo,
                    $"{fornecedor?.Nome} está desativado. Ative o fornecedor no painel ou troque o fornecedor na planilha.");

                // Monta fornecedor, grupo e item sem registrar nada; só registra se a linha inteira for válida.
                var fornecedorNovo = fornecedor is null ? FornecedorPadrao(nomeFornecedor) : null;
                var grupoNovo = grupo is null ? GrupoEstoque.Criar(categoria, ModoControle.Contagem, proximaOrdemGrupo) : null;
                var grupoDaLinha = grupo ?? grupoNovo!;
                var ordem = proximaOrdemItem.GetValueOrDefault(grupoDaLinha.Id, 1);
                var item = ItemEstoque.Criar(
                    grupoDaLinha.Id,
                    (fornecedor ?? fornecedorNovo!).Id,
                    sabor,
                    unidade,
                    porEmbalagem,
                    custo,
                    ordem,
                    minimoUnidades: EhGrupoDePotes(grupoDaLinha) ? MinimoPadraoPotes : null);

                if (fornecedorNovo is not null)
                {
                    fornecedoresNovos.Add(fornecedorNovo);
                    fornecedores[fornecedorNovo.NomeNormalizado] = fornecedorNovo;
                    fornecedorPorId[fornecedorNovo.Id] = fornecedorNovo;
                }

                if (grupoNovo is not null)
                {
                    gruposNovos.Add(grupoNovo);
                    grupos[grupoNovo.NomeNormalizado] = grupoNovo;
                    proximaOrdemGrupo++;
                }

                proximaOrdemItem[grupoDaLinha.Id] = ordem + 1;
                itensNovos.Add(item);
                itens[(grupoDaLinha.Id, item.NomeNormalizado)] = item;
                linhaDoItem[chave] = linha.Numero;
            }
            catch (DomainException ex)
            {
                erros.Add(new LinhaImportacaoDto(linha.Numero, ex.Message));
            }
        }

        if (!request.Simular && itensNovos.Count > 0)
        {
            foreach (var fornecedor in fornecedoresNovos)
            {
                await _estoque.AdicionarFornecedorAsync(fornecedor, cancellationToken);
            }

            foreach (var grupo in gruposNovos)
            {
                await _estoque.AdicionarGrupoAsync(grupo, cancellationToken);
            }

            foreach (var item in itensNovos)
            {
                await _estoque.AdicionarItemAsync(item, cancellationToken);
            }

            await _uow.SaveChangesAsync(cancellationToken);
        }

        return new ImportarCatalogoResponse(
            request.Simular,
            fornecedoresNovos.Select(f => f.Nome).ToList(),
            gruposNovos.Select(g => g.Nome).ToList(),
            itensNovos.Count,
            jaCadastrados,
            avisos,
            erros);
    }

    /// <summary>O que a planilha traz diferente do item já cadastrado, em texto para a dona.</summary>
    private static List<string> Diferencas(
        ItemEstoque item, UnidadeCompra unidade, int porEmbalagem, decimal custo, string nomeFornecedor,
        Dictionary<Guid, Fornecedor> fornecedorPorId)
    {
        var diferencas = new List<string>();
        if (item.Unidade != unidade || item.UnidadesPorEmbalagem != porEmbalagem)
        {
            diferencas.Add($"embalagem {Embalagem(unidade, porEmbalagem)} (catálogo: {Embalagem(item.Unidade, item.UnidadesPorEmbalagem)})");
        }

        if (item.CustoEmbalagem != custo)
        {
            diferencas.Add($"custo R$ {custo:N2} (catálogo: R$ {item.CustoEmbalagem:N2})");
        }

        var atual = fornecedorPorId.GetValueOrDefault(item.FornecedorId);
        if (atual is not null && atual.NomeNormalizado != NomeEstoque.Normalizar(nomeFornecedor))
        {
            diferencas.Add($"fornecedor {nomeFornecedor} (catálogo: {atual.Nome})");
        }

        return diferencas;
    }

    private static string Embalagem(UnidadeCompra unidade, int porEmbalagem) =>
        porEmbalagem == 1 ? unidade.ToString().ToLowerInvariant() : $"{unidade.ToString().ToLowerInvariant()} com {porEmbalagem}";

    /// <summary>Fornecedor que aparece pela primeira vez na planilha. Padrões de docs/10 §3; a dona confere no painel.</summary>
    private static Fornecedor FornecedorPadrao(string nome) =>
        Fornecedor.Criar(
            nome,
            whatsApp: null,
            diasPedido: [DiaPedidoPadrao],
            prazoEntregaDias: PrazoPadraoDias,
            aceitaMeiaCaixa: false,
            envio: FormaEnvioPedido.Impresso,
            mostrarPrecos: true);

    /// <summary>
    /// Potes ganham o mínimo padrão (docs/10 §3, dúvida 7). Enquanto a venda com sabor não chega (Sprint 7) os potes ficam na
    /// contagem, então o grupo é reconhecido também pelo nome ("Pote 1L", "Pote de açaí").
    /// </summary>
    private static bool EhGrupoDePotes(GrupoEstoque grupo) =>
        grupo.ModoControle == ModoControle.SaldoPelaVenda || grupo.NomeNormalizado.StartsWith("pote", StringComparison.Ordinal);

    private static Dictionary<string, int> MapearColunas(LeitorCsv.Linha cabecalho)
    {
        var coluna = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < cabecalho.Campos.Count; i++)
        {
            coluna.TryAdd(cabecalho.Campos[i], i);
        }

        var faltando = ColunasObrigatorias.Where(c => !coluna.ContainsKey(c)).ToList();
        Exigir.Que(faltando.Count == 0,
            $"Faltam colunas no cabeçalho: {string.Join(", ", faltando)}. Use o modelo docs/fase2/catalogo-inicial.csv.");
        return coluna;
    }

    private static string Campo(LeitorCsv.Linha linha, Dictionary<string, int> coluna, string nome) =>
        coluna[nome] < linha.Campos.Count ? linha.Campos[coluna[nome]] : string.Empty;

    private static int LerInteiro(string texto, string campo) =>
        LeitorCsv.LerInteiro(texto) ?? throw new DomainException($"{campo} deve ser um número inteiro, sem ponto nem vírgula (veio \"{texto}\").");

    private static decimal LerDinheiro(string texto) =>
        LeitorCsv.LerDecimal(texto)
        ?? throw new DomainException($"Custo da embalagem inválido: \"{texto}\". Use vírgula nos centavos, ex.: 25,20 ou 1.200,00.");

    private static List<DayOfWeek> LerDias(IReadOnlyList<string>? dias) =>
        (dias ?? []).Select(d => TextoEnum.Converter<DayOfWeek>(d, "Dia da semana")).ToList();

    private async Task<ItemEstoqueDto> AlterarSituacaoDoItemAsync(Guid id, bool ativo, CancellationToken cancellationToken)
    {
        var item = await CarregarItemAsync(id, cancellationToken);
        var fornecedor = await _estoque.ObterFornecedorAsync(item.FornecedorId, cancellationToken);
        if (ativo)
        {
            // Mesmo motivo do DesativarFornecedorAsync: item ativo de fornecedor desativado sumiria do pedido sem aviso.
            Exigir.Que(fornecedor is null || fornecedor.Ativo,
                $"{fornecedor?.Nome} está desativado. Ative o fornecedor ou troque o fornecedor do item antes.");
            item.Ativar();
        }
        else
        {
            item.Desativar();
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return item.ToDto(await _estoque.ObterGrupoAsync(item.GrupoId, cancellationToken), fornecedor);
    }

    private async Task<Fornecedor> CarregarFornecedorAsync(Guid id, CancellationToken cancellationToken) =>
        await _estoque.ObterFornecedorAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Fornecedor", id);

    private async Task<ItemEstoque> CarregarItemAsync(Guid id, CancellationToken cancellationToken) =>
        await _estoque.ObterItemAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Item de estoque", id);

    /// <summary>
    /// Grupo e fornecedor precisam existir. Fornecedor desativado não recebe item novo; o item que já era dele pode ser editado
    /// sem trocar de fornecedor (ex.: corrigir o custo antes de mudar de fábrica).
    /// </summary>
    private async Task<(GrupoEstoque Grupo, Fornecedor Fornecedor)> CarregarGrupoEFornecedorAsync(
        SalvarItemEstoqueRequest request, Guid? fornecedorAtual, CancellationToken cancellationToken)
    {
        var grupo = await _estoque.ObterGrupoAsync(request.GrupoId, cancellationToken)
            ?? throw new DomainException("Grupo de estoque não encontrado. Escolha um grupo da lista.");
        var fornecedor = await _estoque.ObterFornecedorAsync(request.FornecedorId, cancellationToken)
            ?? throw new DomainException("Fornecedor não encontrado. Escolha um fornecedor da lista.");
        Exigir.Que(fornecedor.Ativo || fornecedor.Id == fornecedorAtual,
            $"{fornecedor.Nome} está desativado. Ative o fornecedor antes de ligar itens a ele.");
        return (grupo, fornecedor);
    }

    private async Task GarantirNomeDeFornecedorLivreAsync(string? nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        var normalizado = NomeEstoque.Normalizar(nome);
        var fornecedores = await _estoque.ListarFornecedoresAsync(cancellationToken);
        Exigir.Que(!fornecedores.Any(f => f.NomeNormalizado == normalizado && f.Id != ignorarId), "Já existe um fornecedor com esse nome.");
    }

    private async Task GarantirNomeDeGrupoLivreAsync(string? nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        var normalizado = NomeEstoque.Normalizar(nome);
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        Exigir.Que(!grupos.Any(g => g.NomeNormalizado == normalizado && g.Id != ignorarId), "Já existe um grupo com esse nome.");
    }

    private async Task GarantirNomeDeItemLivreAsync(GrupoEstoque grupo, string? nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        // RN-ES-01. O índice único (grupo_id, nome_normalizado) segura a corrida (vira 409); aqui é a mensagem amigável.
        var normalizado = NomeEstoque.Normalizar(nome);
        if (await _estoque.ExisteItemComNomeAsync(grupo.Id, normalizado, ignorarId, cancellationToken))
        {
            throw new DomainException($"Já existe {nome?.Trim()} em {grupo.Nome}.");
        }
    }
}
