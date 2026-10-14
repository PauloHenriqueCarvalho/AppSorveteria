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

    /// <summary>Docs/10 §3, dúvida 7: mínimo de 2 unidades para cada pote.</summary>
    public const int MinimoPadraoPotes = 2;

    public const int LinhasMaximasImportacao = 2000;

    private static readonly string[] ColunasObrigatorias =
        ["categoria", "sabor", "unidade_compra", "unidades_por_embalagem", "custo_embalagem", "fornecedor"];

    private readonly IEstoqueRepository _estoque;
    private readonly IUnitOfWork _uow;

    public CatalogoEstoqueService(IEstoqueRepository estoque, IUnitOfWork uow)
    {
        _estoque = estoque;
        _uow = uow;
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

        grupo.Renomear(request.Nome);
        grupo.AlterarModo(TextoEnum.Converter<ModoControle>(request.ModoControle, "Modo de controle"));
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
            request.MinimoUnidades);

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

    // ---------- Importação da planilha (docs/fase2/catalogo-inicial.csv) ----------

    /// <summary>
    /// Importa o catálogo no formato de <c>docs/fase2/catalogo-inicial.csv</c>. Pode ser repetida sem duplicar nada:
    /// grupo e fornecedor são achados pelo nome; item, por grupo + sabor (RN-ES-01). Item que já existe recebe unidade,
    /// embalagem, custo e fornecedor da planilha e mantém o que a dona ajustou no painel (ordem, alvo, mínimo, ativo).
    /// Grupo novo: "Pote…" vira <see cref="ModoControle.SaldoPelaVenda"/> (teste dos potes), o resto <see cref="ModoControle.Contagem"/>.
    /// Linha com problema fica de fora com o motivo; as outras entram.
    /// </summary>
    public async Task<ImportarCatalogoResponse> ImportarAsync(ImportarCatalogoRequest request, CancellationToken cancellationToken = default)
    {
        var linhas = LeitorCsv.Ler(request.Conteudo);
        var cabecalho = linhas[0];
        var coluna = MapearColunas(cabecalho);
        var dados = linhas.Skip(1).ToList();
        Exigir.Que(dados.Count > 0, "O arquivo só tem o cabeçalho; nenhum item para importar.");
        Exigir.Que(dados.Count <= LinhasMaximasImportacao,
            $"O arquivo tem {dados.Count} linhas; o máximo é {LinhasMaximasImportacao} por importação.");

        var fornecedores = (await _estoque.ListarFornecedoresAsync(cancellationToken)).ToDictionary(f => f.NomeNormalizado);
        var grupos = (await _estoque.ListarGruposAsync(cancellationToken)).ToDictionary(g => g.NomeNormalizado);
        var itens = (await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken))
            .ToDictionary(i => (i.GrupoId, i.NomeNormalizado));

        var proximaOrdemGrupo = grupos.Count == 0 ? 1 : grupos.Values.Max(g => g.Ordem) + 1;
        var proximaOrdemItem = itens.Values
            .GroupBy(i => i.GrupoId)
            .ToDictionary(g => g.Key, g => g.Max(i => i.Ordem) + 1);
        var vistosNoArquivo = new Dictionary<(string Grupo, string Item), int>();

        int fornecedoresCriados = 0, gruposCriados = 0, criados = 0, atualizados = 0, semMudanca = 0;
        var erros = new List<ErroImportacaoDto>();

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

                var chaveArquivo = (categoria.ToLowerInvariant(), sabor.ToLowerInvariant());
                if (vistosNoArquivo.TryGetValue(chaveArquivo, out var primeira))
                {
                    throw new DomainException($"{sabor} ({categoria}) repete a linha {primeira}.");
                }

                // Valida o item antes de criar fornecedor ou grupo: linha errada não deixa cadastro pela metade.
                ItemEstoque.Criar(Guid.NewGuid(), Guid.NewGuid(), sabor, unidade, porEmbalagem, custo, 0);
                vistosNoArquivo[chaveArquivo] = linha.Numero;

                if (!fornecedores.TryGetValue(nomeFornecedor.ToLowerInvariant(), out var fornecedor))
                {
                    fornecedor = FornecedorPadrao(nomeFornecedor);
                    await _estoque.AdicionarFornecedorAsync(fornecedor, cancellationToken);
                    fornecedores[fornecedor.NomeNormalizado] = fornecedor;
                    fornecedoresCriados++;
                }
                else if (!fornecedor.Ativo)
                {
                    throw new DomainException($"{fornecedor.Nome} está desativado. Ative o fornecedor no painel e importe de novo.");
                }

                if (!grupos.TryGetValue(categoria.ToLowerInvariant(), out var grupo))
                {
                    grupo = GrupoEstoque.Criar(categoria, ModoPadrao(categoria), proximaOrdemGrupo++);
                    await _estoque.AdicionarGrupoAsync(grupo, cancellationToken);
                    grupos[grupo.NomeNormalizado] = grupo;
                    gruposCriados++;
                }

                if (itens.TryGetValue((grupo.Id, sabor.ToLowerInvariant()), out var existente))
                {
                    var mudou = existente.Nome != sabor || existente.FornecedorId != fornecedor.Id || existente.Unidade != unidade
                        || existente.UnidadesPorEmbalagem != porEmbalagem || existente.CustoEmbalagem != custo;
                    if (mudou)
                    {
                        existente.Atualizar(fornecedor.Id, sabor, unidade, porEmbalagem, custo, existente.Ordem,
                            existente.EstoqueAlvoEmbalagens, existente.MinimoUnidades);
                        atualizados++;
                    }
                    else
                    {
                        semMudanca++;
                    }

                    continue;
                }

                var ordem = proximaOrdemItem.GetValueOrDefault(grupo.Id, 1);
                proximaOrdemItem[grupo.Id] = ordem + 1;
                var item = ItemEstoque.Criar(
                    grupo.Id,
                    fornecedor.Id,
                    sabor,
                    unidade,
                    porEmbalagem,
                    custo,
                    ordem,
                    minimoUnidades: grupo.ModoControle == ModoControle.SaldoPelaVenda ? MinimoPadraoPotes : null);
                await _estoque.AdicionarItemAsync(item, cancellationToken);
                itens[(grupo.Id, item.NomeNormalizado)] = item;
                criados++;
            }
            catch (DomainException ex)
            {
                erros.Add(new ErroImportacaoDto(linha.Numero, ex.Message));
            }
        }

        if (fornecedoresCriados + gruposCriados + criados + atualizados > 0)
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return new ImportarCatalogoResponse(fornecedoresCriados, gruposCriados, criados, atualizados, semMudanca, erros);
    }

    /// <summary>Fornecedor que aparece pela primeira vez na planilha. Padrões de docs/10 §3; a dona confere no painel.</summary>
    private static Fornecedor FornecedorPadrao(string nome) =>
        Fornecedor.Criar(
            nome,
            whatsApp: null,
            diasPedido: [DayOfWeek.Monday],
            prazoEntregaDias: PrazoPadraoDias,
            aceitaMeiaCaixa: false,
            envio: FormaEnvioPedido.Impresso,
            mostrarPrecos: true);

    private static ModoControle ModoPadrao(string categoria) =>
        categoria.StartsWith("pote", StringComparison.OrdinalIgnoreCase) ? ModoControle.SaldoPelaVenda : ModoControle.Contagem;

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

    private static int LerInteiro(string texto, string campo)
    {
        var valor = LeitorCsv.LerDecimal(texto);
        if (valor is null || valor != decimal.Truncate(valor.Value) || valor < int.MinValue || valor > int.MaxValue)
        {
            throw new DomainException($"{campo} deve ser um número inteiro (veio \"{texto}\").");
        }

        return (int)valor.Value;
    }

    private static decimal LerDinheiro(string texto) =>
        LeitorCsv.LerDecimal(texto) ?? throw new DomainException($"Custo da embalagem inválido: \"{texto}\". Use, por exemplo, 25,20.");

    private static List<DayOfWeek> LerDias(IReadOnlyList<string>? dias) =>
        (dias ?? []).Select(d => TextoEnum.Converter<DayOfWeek>(d, "Dia da semana")).ToList();

    private async Task<ItemEstoqueDto> AlterarSituacaoDoItemAsync(Guid id, bool ativo, CancellationToken cancellationToken)
    {
        var item = await CarregarItemAsync(id, cancellationToken);
        if (ativo)
        {
            item.Ativar();
        }
        else
        {
            item.Desativar();
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return item.ToDto(
            await _estoque.ObterGrupoAsync(item.GrupoId, cancellationToken),
            await _estoque.ObterFornecedorAsync(item.FornecedorId, cancellationToken));
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
        var normalizado = (nome ?? string.Empty).Trim().ToLowerInvariant();
        var fornecedores = await _estoque.ListarFornecedoresAsync(cancellationToken);
        Exigir.Que(!fornecedores.Any(f => f.NomeNormalizado == normalizado && f.Id != ignorarId), "Já existe um fornecedor com esse nome.");
    }

    private async Task GarantirNomeDeGrupoLivreAsync(string? nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        var normalizado = (nome ?? string.Empty).Trim().ToLowerInvariant();
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        Exigir.Que(!grupos.Any(g => g.NomeNormalizado == normalizado && g.Id != ignorarId), "Já existe um grupo com esse nome.");
    }

    private async Task GarantirNomeDeItemLivreAsync(GrupoEstoque grupo, string? nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        // RN-ES-01. O índice único (grupo_id, nome_normalizado) segura a corrida (vira 409); aqui é a mensagem amigável.
        var normalizado = (nome ?? string.Empty).Trim().ToLowerInvariant();
        if (await _estoque.ExisteItemComNomeAsync(grupo.Id, normalizado, ignorarId, cancellationToken))
        {
            throw new DomainException($"Já existe {nome?.Trim()} em {grupo.Nome}.");
        }
    }
}
