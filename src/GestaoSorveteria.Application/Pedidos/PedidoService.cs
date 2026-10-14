using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Pedidos;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Pedidos;

/// <summary>
/// Pedido ao fornecedor e conferência da entrega (docs/10 §2.4–2.5): sugestão (RN-PF-02/03, RN-RC-04), rascunho com +/−,
/// envio (folha impressa e/ou WhatsApp), cancelamento, recebimento (fecha avisos, novo custo) e histórico (RN-RC-06).
/// Só Admin (RN-RC-07, garantido no controller).
/// </summary>
public sealed class PedidoService
{
    /// <summary>Janela das contagens usadas na média da saída semanal (cinco semanas dão quatro intervalos, com folga).</summary>
    public const int DiasDeHistorico = 42;

    /// <summary>A lista mostra os pedidos dos últimos dias, além dos que ainda não foram recebidos.</summary>
    public const int DiasNaLista = 90;

    /// <summary>
    /// Contagem mais velha que isso sem média de saída não serve de estoque (não dá para descontar o que saiu desde ela):
    /// o item é tratado como sem contagem e a linha mostra a data da última.
    /// </summary>
    public static readonly TimeSpan ContagemRecente = TimeSpan.FromDays(14);

    private readonly IPedidoFornecedorRepository _pedidos;
    private readonly IEstoqueRepository _estoque;
    private readonly IContagemRepository _contagens;
    private readonly IAvisoRepository _avisos;
    private readonly IUsuarioRepository _usuarios;
    private readonly IConfiguracaoLojaRepository _configuracao;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public PedidoService(
        IPedidoFornecedorRepository pedidos,
        IEstoqueRepository estoque,
        IContagemRepository contagens,
        IAvisoRepository avisos,
        IUsuarioRepository usuarios,
        IConfiguracaoLojaRepository configuracao,
        IUnitOfWork uow,
        IClock clock)
    {
        _pedidos = pedidos;
        _estoque = estoque;
        _contagens = contagens;
        _avisos = avisos;
        _usuarios = usuarios;
        _configuracao = configuracao;
        _uow = uow;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PedidoResumoDto>> ListarAsync(Guid? fornecedorId, CancellationToken cancellationToken = default)
    {
        var pedidos = await _pedidos.ListarAsync(fornecedorId, _clock.UtcNow.AddDays(-DiasNaLista), cancellationToken);
        var fornecedores = (await _estoque.ListarFornecedoresAsync(cancellationToken)).ToDictionary(f => f.Id, f => f.Nome);
        return pedidos
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => new PedidoResumoDto(
                p.Id, p.Numero, p.FornecedorId, fornecedores.GetValueOrDefault(p.FornecedorId, "Fornecedor removido"), p.Status.ToString(),
                p.CriadoEm, p.EnviadoEm, p.RecebidoEm, p.CanceladoEm, p.TotalPedido, p.TotalRecebido, p.PercentualAtendido,
                p.Itens.Count(i => i.PedidoEmbalagens > 0)))
            .ToList();
    }

    public async Task<PedidoDto> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        await MontarDtoAsync(await CarregarAsync(id, cancellationToken), cancellationToken);

    /// <summary>
    /// RN-PF-04: monta (ou remonta) o rascunho do fornecedor com a sugestão de agora; remontar mantém o que a dona alterou.
    /// <paramref name="automatico"/> (o painel chama sozinho ao abrir no dia do pedido): se já há pedido enviado ou recebido
    /// do fornecedor feito hoje, devolve esse em vez de começar outro — evita pedido em dobro.
    /// </summary>
    public async Task<PedidoDto> MontarAsync(Guid fornecedorId, Guid usuarioId, bool automatico = false, CancellationToken cancellationToken = default)
    {
        var fornecedor = await _estoque.ObterFornecedorAsync(fornecedorId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Fornecedor", fornecedorId);
        var agora = _clock.UtcNow;

        if (automatico)
        {
            var hoje = DiaComercial.DataDe(agora);
            var deHoje = (await _pedidos.ListarAsync(fornecedorId, agora.AddDays(-DiasNaLista), cancellationToken))
                .Where(p => p.Status is StatusPedido.Enviado or StatusPedido.Recebido && DiaComercial.DataDe(p.EnviadoEm!.Value) == hoje)
                .MaxBy(p => p.EnviadoEm);
            if (deHoje is not null && await _pedidos.ObterRascunhoAsync(fornecedorId, cancellationToken) is null)
            {
                return await MontarDtoAsync(deHoje, cancellationToken);
            }
        }

        var pedido = await _pedidos.ObterRascunhoAsync(fornecedorId, cancellationToken);
        var sugestao = await SugerirAsync(fornecedor, pedido, cancellationToken);
        if (pedido is null)
        {
            pedido = PedidoFornecedor.Montar(fornecedor, await _pedidos.ProximoNumeroAsync(cancellationToken), usuarioId, agora, sugestao);
            await _pedidos.AdicionarAsync(pedido, cancellationToken);
        }
        else
        {
            pedido.Remontar(sugestao, agora);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return await MontarDtoAsync(pedido, cancellationToken);
    }

    /// <summary>+/− da dona no rascunho; item que não está no pedido é acrescentado (RN-PF-04).</summary>
    public async Task<PedidoDto> AlterarItensAsync(Guid id, AlterarItensPedidoRequest request, CancellationToken cancellationToken = default)
    {
        var pedido = await CarregarAsync(id, cancellationToken);
        var fornecedor = await _estoque.ObterFornecedorAsync(pedido.FornecedorId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Fornecedor", pedido.FornecedorId);
        var agora = _clock.UtcNow;

        foreach (var linha in request.Linhas ?? [])
        {
            if (pedido.Linha(linha.ItemEstoqueId) is not null)
            {
                pedido.AlterarQuantidade(linha.ItemEstoqueId, linha.Embalagens, agora);
                continue;
            }

            var item = await _estoque.ObterItemAsync(linha.ItemEstoqueId, cancellationToken)
                ?? throw new DomainException("Item do catálogo não encontrado.");
            pedido.AdicionarItem(item, Passo(item, fornecedor), linha.Embalagens, agora);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return await MontarDtoAsync(pedido, cancellationToken);
    }

    /// <summary>RN-PF-06: confirma; devolve a mensagem do WhatsApp quando o fornecedor recebe por WhatsApp (ou ambos).</summary>
    public async Task<EnviarPedidoResponse> EnviarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pedido = await CarregarAsync(id, cancellationToken);
        var fornecedor = await _estoque.ObterFornecedorAsync(pedido.FornecedorId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Fornecedor", pedido.FornecedorId);
        pedido.Enviar(_clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        var dto = await MontarDtoAsync(pedido, cancellationToken);
        if (fornecedor.Envio == FormaEnvioPedido.Impresso)
        {
            return new EnviarPedidoResponse(dto, null, null);
        }

        var mensagem = MensagemDoPedido(await ConfiguracaoAsync(cancellationToken), fornecedor, pedido, dto);
        return new EnviarPedidoResponse(dto, mensagem, MensagensWhatsApp.Link(fornecedor.WhatsApp, mensagem));
    }

    /// <summary>
    /// Mensagens do WhatsApp para reabrir depois do envio (o pedido — como no envio, só para fornecedor que recebe por
    /// WhatsApp) e da conferência (o que veio diferente).
    /// </summary>
    public async Task<MensagensPedidoDto> MensagensAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var pedido = await CarregarAsync(id, cancellationToken);
        if (pedido.Status is not (StatusPedido.Enviado or StatusPedido.Recebido))
        {
            return new MensagensPedidoDto(null, null, null, null);
        }

        var fornecedor = await _estoque.ObterFornecedorAsync(pedido.FornecedorId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Fornecedor", pedido.FornecedorId);
        var dto = await MontarDtoAsync(pedido, cancellationToken);
        var configuracao = await ConfiguracaoAsync(cancellationToken);
        var doPedido = fornecedor.Envio == FormaEnvioPedido.Impresso ? null : MensagemDoPedido(configuracao, fornecedor, pedido, dto);
        var divergencias = pedido.Status == StatusPedido.Recebido ? MensagemDasDivergencias(configuracao, pedido, dto) : null;
        return new MensagensPedidoDto(
            doPedido,
            doPedido is null ? null : MensagensWhatsApp.Link(fornecedor.WhatsApp, doPedido),
            divergencias,
            divergencias is null ? null : MensagensWhatsApp.Link(fornecedor.WhatsApp, divergencias));
    }

    /// <summary>RN-PF-08: cancela rascunho ou enviado, com motivo e quem cancelou.</summary>
    public async Task<PedidoDto> CancelarAsync(Guid id, CancelarPedidoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var pedido = await CarregarAsync(id, cancellationToken);
        pedido.Cancelar(request.Motivo, usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return await MontarDtoAsync(pedido, cancellationToken);
    }

    /// <summary>
    /// RN-RC-01…05: confere a entrega. Fecha os avisos abertos dos itens que chegaram (RN-AV-05a), grava o custo novo da nota
    /// no catálogo (o pedido mantém o antigo) e devolve a mensagem para o fornecedor quando veio diferente.
    /// </summary>
    public async Task<ReceberPedidoResponse> ReceberAsync(
        Guid id, ReceberPedidoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var pedido = await CarregarAsync(id, cancellationToken);
        var fornecedor = await _estoque.ObterFornecedorAsync(pedido.FornecedorId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Fornecedor", pedido.FornecedorId);
        var agora = _clock.UtcNow;

        // RN-RC-05: custo novo da nota vai para o catálogo antes, para o sabor trocado (não pedido) já entrar com ele.
        // A linha do que foi pedido mantém o custo do pedido (snapshot). Só vale para o que chegou de fato (veio mais que 0).
        foreach (var novo in request.NovosCustos ?? [])
        {
            Exigir.Que(QuantoVeio(novo.ItemEstoqueId) > 0, "Custo novo de item que não veio nesta entrega.");
            var item = await _estoque.ObterItemAsync(novo.ItemEstoqueId, cancellationToken)
                ?? throw new DomainException("Item do catálogo não encontrado.");
            item.AlterarCusto(novo.CustoEmbalagem);
        }

        // Linha que estava com 0 no pedido e veio (sabor trocado que já estava na lista) entra com o custo de agora.
        var diferencas = new List<LinhaRecebida>();
        foreach (var d in request.Diferencas ?? [])
        {
            decimal? custoAtual = null;
            if (pedido.Linha(d.ItemEstoqueId) is { PedidoEmbalagens: 0 } && d.RecebidoEmbalagens > 0)
            {
                custoAtual = (await _estoque.ObterItemAsync(d.ItemEstoqueId, cancellationToken))?.CustoEmbalagem;
            }

            diferencas.Add(new LinhaRecebida(d.ItemEstoqueId, d.RecebidoEmbalagens, d.PedirDeNovo, custoAtual));
        }

        var naoPedidos = new List<LinhaNaoPedida>();
        foreach (var linha in request.NaoPedidos ?? [])
        {
            var item = await _estoque.ObterItemAsync(linha.ItemEstoqueId, cancellationToken)
                ?? throw new DomainException("Item do catálogo não encontrado.");
            naoPedidos.Add(new LinhaNaoPedida(item, Passo(item, fornecedor), linha.RecebidoEmbalagens));
        }

        pedido.Receber(
            diferencas,
            naoPedidos,
            usuarioId,
            agora);

        var avisosFechados = 0;
        foreach (var linha in pedido.Itens.Where(i => i.RecebidoEmbalagens > 0))
        {
            if (await _avisos.ObterAbertoDoItemAsync(linha.ItemEstoqueId, cancellationToken) is { } aviso)
            {
                aviso.FecharPelaEntrega(pedido.Id, agora);
                avisosFechados++;
            }
        }

        await _uow.SaveChangesAsync(cancellationToken);
        var dto = await MontarDtoAsync(pedido, cancellationToken);
        var mensagem = MensagemDasDivergencias(await ConfiguracaoAsync(cancellationToken), pedido, dto);
        return new ReceberPedidoResponse(dto, avisosFechados, mensagem, mensagem is null ? null : MensagensWhatsApp.Link(fornecedor.WhatsApp, mensagem));

        // O que a conferência diz que veio: diferença informada, sabor não pedido ou, sem nada, o que foi pedido.
        decimal? QuantoVeio(Guid itemId) =>
            request.Diferencas?.FirstOrDefault(d => d.ItemEstoqueId == itemId)?.RecebidoEmbalagens
            ?? request.NaoPedidos?.FirstOrDefault(n => n.ItemEstoqueId == itemId)?.RecebidoEmbalagens
            ?? pedido.Linha(itemId)?.PedidoEmbalagens;
    }

    /// <summary>RN-RC-06: pedidos recebidos nos últimos 30 dias, valores, % atendido médio e os itens que mais faltam.</summary>
    public async Task<HistoricoFornecedorDto> HistoricoAsync(Guid fornecedorId, CancellationToken cancellationToken = default)
    {
        var fornecedor = await _estoque.ObterFornecedorAsync(fornecedorId, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Fornecedor", fornecedorId);
        var agora = _clock.UtcNow;
        var recebidos = (await _pedidos.ListarRecebidosEntreAsync(agora.AddDays(-30), agora.AddMinutes(1), cancellationToken))
            .Where(p => p.FornecedorId == fornecedorId)
            .ToList();
        var catalogo = await CatalogoAsync(cancellationToken);

        var faltas = recebidos
            .SelectMany(p => p.Itens.Where(i => i.Faltou > 0))
            .GroupBy(i => i.ItemEstoqueId)
            .Select(g => new ItemQueMaisFaltaDto(g.Key, catalogo.Descricao(g.Key), g.Count(), g.Sum(i => i.Faltou)))
            .OrderByDescending(f => f.VezesQueFaltou).ThenByDescending(f => f.EmbalagensQueFaltaram)
            .Take(10)
            .ToList();

        var percentuais = recebidos.Where(p => p.PercentualAtendido is not null).Select(p => p.PercentualAtendido!.Value).ToList();
        return new HistoricoFornecedorDto(
            fornecedor.Id,
            fornecedor.Nome,
            recebidos.Count,
            Moeda.Arredondar(recebidos.Sum(p => p.TotalPedido)),
            Moeda.Arredondar(recebidos.Sum(p => p.TotalRecebido ?? 0)),
            percentuais.Count == 0 ? null : Math.Round(percentuais.Average(), 2, MidpointRounding.AwayFromZero),
            faltas);
    }

    // ---------------------------------------------------------------- sugestão (RN-PF-02/03, RN-RC-04)

    private async Task<List<LinhaSugerida>> SugerirAsync(Fornecedor fornecedor, PedidoFornecedor? rascunho, CancellationToken cancellationToken)
    {
        var agora = _clock.UtcNow;
        var hoje = DiaComercial.DataDe(agora);
        var catalogo = await CatalogoAsync(cancellationToken);
        var itens = catalogo.Itens.Values.Where(i => i.Ativo && i.FornecedorId == fornecedor.Id).ToList();

        // Contagens e entregas das últimas semanas: base da saída semanal (RN-ES-07).
        var contagens = (await _contagens.ListarAsync(agora.AddDays(-DiasDeHistorico), agora.AddMinutes(1), cancellationToken))
            .Where(c => c.Concluida)
            .ToList();
        var ultimaContagem = await _contagens.UltimaContagemPorItemAsync(agora.AddMinutes(1), cancellationToken);
        // Entregas desde a mais antiga das duas: janela da média ou última contagem de algum item (o que chegou depois dela soma).
        var desde = ultimaContagem.Values.Select(c => c.ConcluidaEm).Append(agora.AddDays(-DiasDeHistorico)).Min();
        var recebidos = await _pedidos.ListarRecebidosEntreAsync(desde, agora.AddMinutes(1), cancellationToken);
        var avisosAbertos = (await _avisos.ListarAsync(fechadosDesdeUtc: null, cancellationToken))
            .Where(a => a.ItemEstoqueId is not null)
            .ToDictionary(a => a.ItemEstoqueId!.Value);
        var nomes = (await _usuarios.ListarAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Nome);
        var pedidosDoFornecedor = await _pedidos.ListarAsync(fornecedor.Id, agora.AddDays(-DiasNaLista), cancellationToken);

        // A caminho: enviado e ainda não recebido — já vai chegar, não pode ser pedido de novo.
        var aCaminho = pedidosDoFornecedor.Where(p => p.Status == StatusPedido.Enviado).ToList();
        // Item sem contagem: o que os pedidos enviados neste ciclo já trouxeram.
        var inicioDoCiclo = DiaComercial.IntervaloUtc(SugestaoPedido.InicioDoCiclo(hoje, fornecedor.DiasPedido)).InicioUtc;
        var recebidosNoCiclo = pedidosDoFornecedor.Where(p => p.Status == StatusPedido.Recebido && p.EnviadoEm >= inicioDoCiclo).ToList();
        // RN-RC-04: a falta de um pedido recebido volta enquanto nenhum pedido do fornecedor foi enviado depois da conferência
        // dele (se já saiu outro, a falta entrou nele ou a dona decidiu não pedir).
        var comFaltaPendente = pedidosDoFornecedor
            .Where(p => p.Status == StatusPedido.Recebido
                && !pedidosDoFornecedor.Any(o => o.EnviadoEm > p.RecebidoEm && o.Status is StatusPedido.Enviado or StatusPedido.Recebido))
            .OrderBy(p => p.RecebidoEm)
            .ToList();
        var diasACobrir = SugestaoPedido.DiasACobrir(hoje.DayOfWeek, fornecedor.DiasPedido, fornecedor.PrazoEntregaDias);

        var linhas = new List<LinhaSugerida>();
        foreach (var item in itens.OrderBy(i => catalogo.Grupos[i.GrupoId].Ordem).ThenBy(i => i.Ordem).ThenBy(i => i.Nome))
        {
            var grupo = catalogo.Grupos[item.GrupoId];
            var passo = Passo(item, fornecedor);
            var aCaminhoDoItem = aCaminho.Where(p => p.Linha(item.Id) is { PedidoEmbalagens: > 0 }).OrderBy(p => p.EnviadoEm).ToList();
            var marcas = new List<string>();
            decimal sugerido = 0;
            var origem = OrigemLinhaPedido.Sugestao;

            if (grupo.EntraNaContagem)
            {
                var historico = contagens
                    .Select(c => (c.ConcluidaEm, Linha: c.Itens.FirstOrDefault(i => i.ItemEstoqueId == item.Id)))
                    .Where(x => x.Linha?.ContadoUnidades is not null)
                    .Select(x => new SugestaoPedido.Contado(x.ConcluidaEm!.Value, x.Linha!.ContadoUnidades!.Value))
                    .ToList();
                var media = SugestaoPedido.SaidaSemanalMedia(historico, (de, ate) => RecebidoEntre(recebidos, item, de, ate));

                // Estoque de agora: pela última contagem (menos a saída estimada desde ela) ou, sem contagem que sirva, pelo que o
                // ciclo já trouxe; mais o que está a caminho.
                decimal? estoque = null;
                var temContagem = ultimaContagem.TryGetValue(item.Id, out var contado);
                var contagemAntiga = temContagem && agora - contado.ConcluidaEm > ContagemRecente;
                if (temContagem && (media is not null || !contagemAntiga))
                {
                    var diasDesde = (decimal)(agora - contado.ConcluidaEm).Ticks / TimeSpan.TicksPerDay;
                    estoque = SugestaoPedido.EstoqueEstimado(
                        contado.Unidades, RecebidoEntre(recebidos, item, contado.ConcluidaEm, agora.AddMinutes(1)), media, diasDesde);
                }
                else
                {
                    var noCiclo = recebidosNoCiclo.Sum(p => (p.Linha(item.Id)?.RecebidoEmbalagens ?? 0) * item.UnidadesPorEmbalagem);
                    estoque = noCiclo > 0 ? noCiclo : null;
                }

                var chegando = aCaminhoDoItem.Sum(p => p.Linha(item.Id)!.PedidoEmbalagens * item.UnidadesPorEmbalagem);
                if (chegando > 0)
                {
                    estoque = (estoque ?? 0) + chegando;
                }

                decimal? alvo = item.EstoqueAlvoEmbalagens is { } a ? a * item.UnidadesPorEmbalagem : null;
                var necessidade = SugestaoPedido.NecessidadeEmUnidades(media, diasACobrir, estoque, alvo);
                sugerido = SugestaoPedido.EmEmbalagens(necessidade, item.UnidadesPorEmbalagem, passo);

                if (contagemAntiga)
                {
                    marcas.Add($"Última contagem em {DiaComercial.DataDe(contado.ConcluidaEm):dd/MM}");
                }
            }

            // RN-RC-04: o que faltou volta (se a dona não desmarcou "pedir de novo"). Fica marcado mesmo quando a conta já
            // cobre a falta, para a dona ver de onde veio.
            var faltas = comFaltaPendente
                .Select(p => (Pedido: p, Linha: p.Linha(item.Id)))
                .Where(x => x.Linha is { Faltou: > 0, PedirDeNovo: true })
                .ToList();
            if (faltas.Count > 0)
            {
                var faltou = faltas.Sum(x => x.Linha!.Faltou);
                sugerido = Math.Max(sugerido, Math.Ceiling(faltou / passo) * passo);
                origem = OrigemLinhaPedido.FaltouAnterior;
                marcas.InsertRange(0, faltas.Select(x =>
                    $"Faltou {MensagensWhatsApp.Embalagens(x.Linha!.Faltou, item.Unidade)} no pedido nº {x.Pedido.Numero}"));
            }

            // RN-PF-03: aviso aberto entra com pelo menos 1 embalagem e marcado com quem avisou — salvo se o item já vem num
            // pedido enviado depois do aviso (o aviso fecha quando essa entrega for conferida; pedir de novo seria em dobro).
            if (avisosAbertos.TryGetValue(item.Id, out var aviso) && !aCaminhoDoItem.Any(p => p.EnviadoEm >= aviso.CriadoEm))
            {
                sugerido = Math.Max(sugerido, 1m);
                origem = OrigemLinhaPedido.Aviso;
                var autor = aviso.CriadoPorUsuarioId is { } autorId ? nomes.GetValueOrDefault(autorId, "Alguém") : "Sistema";
                marcas.Insert(0, $"{autor} avisou ({NomeDaPrioridade(aviso.Prioridade)}): {(aviso.QuantidadeAtual == 0 ? "acabou" : $"restam {aviso.QuantidadeAtual}")}");
            }

            if (aCaminhoDoItem.Count > 0)
            {
                var quanto = aCaminhoDoItem.Sum(p => p.Linha(item.Id)!.PedidoEmbalagens);
                marcas.Add($"A caminho: {MensagensWhatsApp.Embalagens(quanto, item.Unidade)} no pedido nº {string.Join(", ", aCaminhoDoItem.Select(p => p.Numero))}");
            }

            var observacao = marcas.Count == 0 ? null : Cortar(string.Join(" · ", marcas), 200);

            // Insumos (SoAviso) só entram com aviso ou falta; o resto entra sempre (a dona vê o catálogo inteiro e ajusta com +/−).
            if (grupo.EntraNaContagem || origem != OrigemLinhaPedido.Sugestao)
            {
                linhas.Add(new LinhaSugerida(item, passo, sugerido, origem, observacao));
            }
        }

        // Linhas do rascunho que saíram da sugestão (insumo acrescentado pela dona, item desativado) também acompanham custo e
        // passo de agora, com sugerido 0 e sem marcas velhas. Item que mudou de fornecedor fica como está.
        foreach (var linha in rascunho?.Itens ?? [])
        {
            if (linhas.All(l => l.Item.Id != linha.ItemEstoqueId)
                && catalogo.Itens.GetValueOrDefault(linha.ItemEstoqueId) is { } item
                && item.FornecedorId == fornecedor.Id)
            {
                linhas.Add(new LinhaSugerida(item, Passo(item, fornecedor), 0, OrigemLinhaPedido.Sugestao));
            }
        }

        return linhas;
    }

    private static decimal RecebidoEntre(IReadOnlyList<PedidoFornecedor> recebidos, ItemEstoque item, DateTime de, DateTime ate) =>
        recebidos
            .Where(p => p.RecebidoEm > de && p.RecebidoEm <= ate)
            .SelectMany(p => p.Itens.Where(i => i.ItemEstoqueId == item.Id))
            .Sum(i => (i.RecebidoEmbalagens ?? 0) * item.UnidadesPorEmbalagem);

    private static string MensagemDoPedido(ConfiguracaoLoja configuracao, Fornecedor fornecedor, PedidoFornecedor pedido, PedidoDto dto) =>
        MensagensWhatsApp.Pedido(
            configuracao.NomeLoja,
            fornecedor.Nome,
            pedido.Numero,
            DiaComercial.DataDe(pedido.EnviadoEm!.Value),
            dto.Linhas.Select(l => new MensagensWhatsApp.LinhaPedido(l.GrupoNome, l.ItemNome, l.PedidoEmbalagens, Enum.Parse<UnidadeCompra>(l.Unidade))).ToList(),
            fornecedor.MostrarPrecos ? pedido.TotalPedido : null);

    /// <summary>RN-RC-04: o que faltou, veio a mais ou veio trocado; nulo quando chegou tudo certo.</summary>
    private static string? MensagemDasDivergencias(ConfiguracaoLoja configuracao, PedidoFornecedor pedido, PedidoDto dto)
    {
        var faltou = Linhas("Faltou", l => l.PedidoEmbalagens - l.RecebidoEmbalagens!.Value);
        var aMais = Linhas("VeioAMais", l => l.RecebidoEmbalagens!.Value - l.PedidoEmbalagens);
        var trocados = Linhas("NaoPedido", l => l.RecebidoEmbalagens!.Value);
        return faltou.Count + aMais.Count + trocados.Count == 0
            ? null
            : MensagensWhatsApp.Divergencias(configuracao.NomeLoja, pedido.Numero, faltou, aMais, trocados);

        List<MensagensWhatsApp.LinhaPedido> Linhas(string situacao, Func<LinhaPedidoDto, decimal> quantidade) =>
            dto.Linhas
                .Where(l => l.Situacao == situacao)
                .Select(l => new MensagensWhatsApp.LinhaPedido(l.GrupoNome, l.ItemNome, quantidade(l), Enum.Parse<UnidadeCompra>(l.Unidade)))
                .ToList();
    }

    private static string Cortar(string texto, int maximo) => texto.Length <= maximo ? texto : string.Concat(texto.AsSpan(0, maximo - 1), "…");

    /// <summary>Meia caixa só para caixa e só quando o fornecedor aceita (docs/10 §3, dúvida 2).</summary>
    private static decimal Passo(ItemEstoque item, Fornecedor fornecedor) =>
        item.Unidade == UnidadeCompra.Caixa && fornecedor.AceitaMeiaCaixa ? 0.5m : 1m;

    private static string NomeDaPrioridade(PrioridadeAviso prioridade) => prioridade switch
    {
        PrioridadeAviso.Urgente => "urgente",
        PrioridadeAviso.Alta => "alta",
        _ => "média",
    };

    // ---------------------------------------------------------------- apoio

    private async Task<PedidoDto> MontarDtoAsync(PedidoFornecedor pedido, CancellationToken cancellationToken)
    {
        var fornecedor = await _estoque.ObterFornecedorAsync(pedido.FornecedorId, cancellationToken);
        var catalogo = await CatalogoAsync(cancellationToken);
        var nomes = (await _usuarios.ListarAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Nome);
        var contados = await _contagens.UltimaContagemPorItemAsync(_clock.UtcNow.AddMinutes(1), cancellationToken);
        var referencia = pedido.EnviadoEm ?? _clock.UtcNow;

        var linhas = pedido.Itens
            .Select(l => (Linha: l, Item: catalogo.Itens.GetValueOrDefault(l.ItemEstoqueId)))
            .Select(x => (x.Linha, x.Item, Grupo: x.Item is null ? null : catalogo.Grupos.GetValueOrDefault(x.Item.GrupoId)))
            .OrderBy(x => x.Grupo?.Ordem ?? int.MaxValue).ThenBy(x => x.Grupo?.Nome).ThenBy(x => x.Item?.Ordem ?? int.MaxValue).ThenBy(x => x.Item?.Nome)
            .Select(x => new LinhaPedidoDto(
                x.Linha.ItemEstoqueId,
                x.Grupo?.Id ?? Guid.Empty,
                x.Grupo?.Nome ?? string.Empty,
                x.Item?.Nome ?? "Item removido",
                (x.Item?.Unidade ?? UnidadeCompra.Unidade).ToString(),
                x.Item?.UnidadesPorEmbalagem ?? 1,
                x.Linha.Passo,
                contados.TryGetValue(x.Linha.ItemEstoqueId, out var contado) ? contado.Unidades : null,
                x.Linha.SugeridoEmbalagens,
                x.Linha.PedidoEmbalagens,
                x.Linha.RecebidoEmbalagens,
                x.Linha.CustoEmbalagem,
                x.Linha.Subtotal,
                x.Linha.AlteradoPelaDona,
                x.Linha.Origem.ToString(),
                x.Linha.Observacao,
                x.Linha.PedirDeNovo,
                x.Linha.Situacao?.ToString(),
                x.Linha.DiferencaValor))
            .ToList();

        return new PedidoDto(
            pedido.Id,
            pedido.Numero,
            pedido.FornecedorId,
            fornecedor?.Nome ?? "Fornecedor removido",
            (fornecedor?.Envio ?? FormaEnvioPedido.Impresso).ToString(),
            fornecedor?.MostrarPrecos ?? true,
            fornecedor?.AceitaMeiaCaixa ?? false,
            pedido.Status.ToString(),
            nomes.GetValueOrDefault(pedido.CriadoPorUsuarioId, "Usuário removido"),
            pedido.CriadoEm,
            pedido.EnviadoEm,
            pedido.RecebidoEm,
            pedido.RecebidoPorUsuarioId is { } recebeu ? nomes.GetValueOrDefault(recebeu, "Usuário removido") : null,
            pedido.CanceladoEm,
            pedido.CanceladoPorUsuarioId is { } cancelou ? nomes.GetValueOrDefault(cancelou, "Usuário removido") : null,
            pedido.MotivoCancelamento,
            DiaComercial.DataDe(referencia).AddDays(fornecedor?.PrazoEntregaDias ?? 0),
            pedido.TotalPedido,
            pedido.TotalRecebido,
            pedido.PercentualAtendido,
            linhas);
    }

    private async Task<PedidoFornecedor> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _pedidos.ObterPorIdAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Pedido", id);

    private async Task<ConfiguracaoLoja> ConfiguracaoAsync(CancellationToken cancellationToken) =>
        await _configuracao.ObterAsync(cancellationToken) ?? ConfiguracaoLoja.Padrao(_clock.UtcNow);

    private async Task<Catalogo> CatalogoAsync(CancellationToken cancellationToken)
    {
        var itens = await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken);
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        return new Catalogo(itens.ToDictionary(i => i.Id), grupos.ToDictionary(g => g.Id));
    }

    private sealed record Catalogo(Dictionary<Guid, ItemEstoque> Itens, Dictionary<Guid, GrupoEstoque> Grupos)
    {
        public string Descricao(Guid itemId) =>
            Itens.TryGetValue(itemId, out var item)
                ? MensagensWhatsApp.DescricaoDoItem(Grupos.GetValueOrDefault(item.GrupoId)?.Nome, item.Nome)
                : "Item removido";
    }
}
