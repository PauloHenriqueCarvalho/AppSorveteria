using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Estoque;

/// <summary>
/// Saldo dos potes (grupos <c>SaldoPelaVenda</c>, RN-ES-08): baixa da venda que chega na sincronização (RN-ES-09), estorno
/// pelo painel, entrada na conferência da entrega (RN-RC-03) e aviso automático de mínimo e zerado (RN-AV-04).
/// Só marca as alterações; quem confirma é o caso de uso que chamou (<c>IUnitOfWork</c>).
/// </summary>
public sealed class SaldoEstoqueService
{
    /// <summary>Observação do aviso criado pelo sistema (autor "Sistema").</summary>
    public const string ObservacaoDoSistema = "Pelo saldo do sistema (vendas e entregas desde a última contagem)";

    /// <summary>Janela da média de vendas por semana dos potes (4 semanas, como a média da contagem).</summary>
    public const int DiasDaMediaDeVendas = 28;

    private readonly IMovimentoEstoqueRepository _movimentos;
    private readonly IContagemRepository _contagens;
    private readonly IEstoqueRepository _estoque;
    private readonly IAvisoRepository _avisos;
    private readonly IClock _clock;

    public SaldoEstoqueService(
        IMovimentoEstoqueRepository movimentos,
        IContagemRepository contagens,
        IEstoqueRepository estoque,
        IAvisoRepository avisos,
        IClock clock)
    {
        _movimentos = movimentos;
        _contagens = contagens;
        _estoque = estoque;
        _avisos = avisos;
        _clock = clock;
    }

    /// <summary>
    /// Saldo de cada item: última contagem (pela hora em que o item foi contado) + movimentos depois dela.
    /// <paramref name="aindaNaoGravados"/>: movimentos marcados neste caso de uso e ainda não confirmados.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> SaldosAsync(
        IReadOnlyCollection<Guid> itemEstoqueIds,
        IEnumerable<MovimentoEstoque>? aindaNaoGravados = null,
        CancellationToken cancellationToken = default)
    {
        var ids = itemEstoqueIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var bases = await _contagens.UltimaContagemPorItemAsync(_clock.UtcNow.AddMinutes(1), cancellationToken);
        // Só os movimentos depois da base mais antiga (todos, se algum item nunca foi contado).
        DateTime? desde = ids.All(bases.ContainsKey) ? ids.Min(id => bases[id].Em) : null;
        var gravados = await _movimentos.ListarDosItensAsync(ids, desde, cancellationToken);
        var todos = gravados.Concat(aindaNaoGravados ?? []).DistinctBy(m => m.Id).ToList();
        return ids.ToDictionary(
            id => id,
            id => CalculoEstoque.Saldo(
                bases.TryGetValue(id, out var contagem) ? new CalculoEstoque.BaseDoSaldo(contagem.Unidades, contagem.Em) : null,
                todos.Where(m => m.ItemEstoqueId == id)));
    }

    /// <summary>
    /// Saída por semana pelas vendas (vendas − estornos ÷ dias × 7) — a base do pedido dos potes. Conta desde a primeira venda
    /// dentro dos últimos <see cref="DiasDaMediaDeVendas"/> dias (no mínimo uma semana), para o pote que começou a ser vendido
    /// com sabor há pouco não parecer vender menos. Nulo para o item sem venda na janela (fica com a média pela contagem).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, decimal?>> SaidaSemanalPelasVendasAsync(
        IReadOnlyCollection<Guid> itemEstoqueIds, CancellationToken cancellationToken = default)
    {
        var ids = itemEstoqueIds.Distinct().ToList();
        var agora = _clock.UtcNow;
        var vendas = (await _movimentos.ListarDosItensAsync(ids, agora.AddDays(-DiasDaMediaDeVendas), cancellationToken))
            .Where(m => m.Tipo is TipoMovimentoEstoque.BaixaVenda or TipoMovimentoEstoque.EstornoVenda && m.Em <= agora)
            .ToList();
        return ids.ToDictionary(
            id => id,
            id =>
            {
                var doItem = vendas.Where(m => m.ItemEstoqueId == id).ToList();
                if (!doItem.Any(m => m.Tipo == TipoMovimentoEstoque.BaixaVenda))
                {
                    return (decimal?)null;
                }

                var primeira = doItem.Where(m => m.Tipo == TipoMovimentoEstoque.BaixaVenda).Min(m => m.Em);
                var dias = Math.Max(7m, (decimal)(agora - primeira).Ticks / TimeSpan.TicksPerDay);
                return Math.Max(0, -doItem.Sum(m => m.Efeito)) * 7m / dias;
            });
    }

    /// <summary>
    /// RN-ES-09: venda fechada que chegou ao servidor — uma baixa por linha com sabor de pote (quantidade × unidades por
    /// venda do produto), no momento do fechamento (nunca no futuro do servidor). Cancelada não baixa. Depois, avisos.
    /// </summary>
    public async Task BaixarVendaAsync(
        Comanda comanda, IReadOnlyDictionary<Guid, Produto> produtos, CancellationToken cancellationToken = default)
    {
        if (comanda.Status != StatusComanda.Fechada)
        {
            return;
        }

        var agora = _clock.UtcNow;
        var vendidaEm = comanda.FechadaEm is { } fechada && fechada < agora ? fechada : agora;
        var novos = new List<MovimentoEstoque>();
        foreach (var linha in comanda.Itens.Where(i => i.ItemEstoqueId is not null && i.ProdutoId is not null))
        {
            if (!produtos.TryGetValue(linha.ProdutoId!.Value, out var produto)
                || !await EhPoteAsync(linha.ItemEstoqueId!.Value, cancellationToken))
            {
                continue;
            }

            var baixa = MovimentoEstoque.BaixaVenda(
                linha.ItemEstoqueId!.Value, linha.Quantidade * produto.UnidadesPorVenda, comanda.Id, linha.Id, vendidaEm, agora);
            await _movimentos.AdicionarAsync(baixa, cancellationToken);
            novos.Add(baixa);
        }

        await AvisarPeloSaldoAsync(novos.Select(m => m.ItemEstoqueId).ToList(), vendidaEm, novos, cancellationToken);
    }

    /// <summary>Estorno da venda pelo painel (RN-CM-09): devolve cada baixa dela uma vez só.</summary>
    public async Task EstornarVendaAsync(Guid comandaId, CancellationToken cancellationToken = default)
    {
        var agora = _clock.UtcNow;
        var daVenda = await _movimentos.ListarDaComandaAsync(comandaId, cancellationToken);
        var jaEstornadas = daVenda.Where(m => m.Tipo == TipoMovimentoEstoque.EstornoVenda).Select(m => m.ItemComandaId).ToHashSet();
        foreach (var baixa in daVenda.Where(m => m.Tipo == TipoMovimentoEstoque.BaixaVenda && !jaEstornadas.Contains(m.ItemComandaId)))
        {
            await _movimentos.AdicionarAsync(MovimentoEstoque.EstornoVenda(baixa, agora), cancellationToken);
        }
    }

    /// <summary>
    /// RN-RC-03: entrega conferida — os potes que vieram entram no saldo na hora em que a entrega chegou na loja
    /// (embalagens × unidades por embalagem; "veio 0" não gera entrada).
    /// </summary>
    public async Task RegistrarEntradaAsync(PedidoFornecedor pedido, CancellationToken cancellationToken = default)
    {
        if (pedido.Status != StatusPedido.Recebido)
        {
            return;
        }

        var chegouEm = pedido.ChegouEm ?? pedido.RecebidoEm!.Value;
        foreach (var linha in pedido.Itens.Where(i => i.RecebidoEmbalagens > 0))
        {
            var item = await _estoque.ObterItemAsync(linha.ItemEstoqueId, cancellationToken);
            if (item is null || !await EhPoteAsync(item.Id, cancellationToken))
            {
                continue;
            }

            // Potes vêm por unidade; meia caixa com número ímpar perde a fração (a contagem corrige).
            var unidades = (int)Math.Floor(linha.RecebidoEmbalagens!.Value * item.UnidadesPorEmbalagem);
            if (unidades > 0)
            {
                await _movimentos.AdicionarAsync(
                    MovimentoEstoque.Entrada(item.Id, unidades, pedido.Id, chegouEm, pedido.RecebidoEm), cancellationToken);
            }
        }
    }

    /// <summary>
    /// RN-AV-04: pote que zerou vira aviso Urgente; que chegou no mínimo, Alta (autor "Sistema"), na hora da venda
    /// (<paramref name="emUtc"/>). Só pote que já foi contado alguma vez: antes disso o saldo não tem base. Item com aviso aberto não ganha outro: a prioridade só sobe, e a quantidade só acompanha o
    /// saldo enquanto a última informação for do sistema (o que a atendente informou fica).
    /// </summary>
    public async Task AvisarPeloSaldoAsync(
        IReadOnlyCollection<Guid> itemEstoqueIds,
        DateTime emUtc,
        IEnumerable<MovimentoEstoque>? aindaNaoGravados = null,
        CancellationToken cancellationToken = default)
    {
        if (itemEstoqueIds.Count == 0)
        {
            return;
        }

        // O saldo só vale depois da primeira contagem (como em SaldoEsperadoNaContagemAsync): pote nunca contado não avisa.
        var contados = await _contagens.UltimaContagemPorItemAsync(_clock.UtcNow.AddMinutes(1), cancellationToken);
        var comBase = itemEstoqueIds.Where(contados.ContainsKey).ToList();
        if (comBase.Count == 0)
        {
            return;
        }

        var saldos = await SaldosAsync(comBase, aindaNaoGravados, cancellationToken);
        foreach (var (itemId, saldo) in saldos)
        {
            var item = await _estoque.ObterItemAsync(itemId, cancellationToken);
            if (item is null || !item.Ativo || CalculoEstoque.PrioridadePeloSaldo(saldo, item.MinimoUnidades) is not { } prioridade)
            {
                continue;
            }

            var quantidade = Math.Clamp(saldo, 0, Aviso.QuantidadeMaxima);
            var aberto = await _avisos.ObterAbertoDoItemAsync(itemId, cancellationToken);
            if (aberto is null)
            {
                await _avisos.AdicionarAsync(Aviso.DoItem(itemId, quantidade, prioridade, ObservacaoDoSistema, null, emUtc), cancellationToken);
            }
            else
            {
                aberto.AtualizarPeloSaldo(quantidade, prioridade, emUtc < aberto.CriadoEm ? aberto.CriadoEm : emUtc);
            }
        }
    }

    /// <summary>GET /api/estoque/saldos: os potes ativos (e os inativos com saldo) na ordem da planilha.</summary>
    public async Task<IReadOnlyList<SaldoPoteDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var grupos = (await _estoque.ListarGruposAsync(cancellationToken))
            .Where(g => g.ModoControle == ModoControle.SaldoPelaVenda)
            .ToDictionary(g => g.Id);
        var itens = (await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken))
            .Where(i => grupos.ContainsKey(i.GrupoId))
            .ToList();
        var ids = itens.Select(i => i.Id).ToList();
        var saldos = await SaldosAsync(ids, cancellationToken: cancellationToken);
        var contagens = await _contagens.UltimaContagemPorItemAsync(_clock.UtcNow.AddMinutes(1), cancellationToken);
        var semana = (await _movimentos.ListarDosItensAsync(ids, _clock.UtcNow.AddDays(-7), cancellationToken))
            .Where(m => m.Tipo is TipoMovimentoEstoque.BaixaVenda or TipoMovimentoEstoque.EstornoVenda)
            .GroupBy(m => m.ItemEstoqueId)
            .ToDictionary(g => g.Key, g => Math.Max(0, -g.Sum(m => m.Efeito)));

        return itens
            .Where(i => i.Ativo || saldos[i.Id] != 0)
            .OrderBy(i => grupos[i.GrupoId].Ordem).ThenBy(i => grupos[i.GrupoId].Nome).ThenBy(i => i.Ordem).ThenBy(i => i.Nome)
            .Select(i =>
            {
                var contagem = contagens.TryGetValue(i.Id, out var c) ? c : (UltimaContagemDoItem?)null;
                return new SaldoPoteDto(
                    i.Id, i.GrupoId, grupos[i.GrupoId].Nome, i.Nome, saldos[i.Id], i.MinimoUnidades,
                    contagem?.Unidades, contagem?.Em, semana.GetValueOrDefault(i.Id), i.Ativo);
            })
            .ToList();
    }

    /// <summary>Diferença acima disso (em unidades) entre o contado e o sistema vai para o resumo da dona (RN-ES-08).</summary>
    public const int DiferencaQueAvisa = 1;

    /// <summary>
    /// RN-ES-08: quanto o sistema esperava de cada pote na hora em que ele foi contado = contagem anterior + movimentos até a
    /// hora do item (sem hora, a conclusão; aberta, agora). Calculado na leitura: venda que sincroniza depois entra certa.
    /// Pote sem contagem anterior fica de fora — o saldo só vale depois da primeira contagem.
    /// <paramref name="anteriores"/>: as últimas contagens antes desta (corte na conclusão dela, ou agora), se já carregadas.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> SaldoEsperadoNaContagemAsync(
        Contagem contagem,
        IReadOnlyCollection<Guid> potes,
        IReadOnlyDictionary<Guid, UltimaContagemDoItem>? anteriores = null,
        CancellationToken cancellationToken = default)
    {
        var agora = _clock.UtcNow;
        anteriores ??= await _contagens.UltimaContagemPorItemAsync(contagem.ConcluidaEm ?? agora, cancellationToken);
        var ids = potes.Distinct().Where(anteriores.ContainsKey).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var movimentos = (await _movimentos.ListarDosItensAsync(ids, ids.Min(id => anteriores[id].Em), cancellationToken))
            .ToLookup(m => m.ItemEstoqueId);
        var linhas = contagem.Itens.ToDictionary(i => i.ItemEstoqueId);
        return ids.ToDictionary(id => id, id =>
        {
            var quando = linhas.GetValueOrDefault(id)?.ContadoEm ?? contagem.ConcluidaEm ?? agora;
            var anterior = anteriores[id];
            return CalculoEstoque.Saldo(new CalculoEstoque.BaseDoSaldo(anterior.Unidades, anterior.Em), movimentos[id].Where(m => m.Em <= quando));
        });
    }

    /// <summary>
    /// RN-AV-07/RN-ES-08: potes da última contagem concluída no período em que o contado ficou mais de 1 longe do sistema.
    /// O resumo olha ontem e hoje: a contagem da véspera do pedido costuma ser feita depois de fechar o caixa.
    /// </summary>
    public async Task<MensagensWhatsApp.DiferencasDaContagem?> DiferencasDaUltimaContagemAsync(
        DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default)
    {
        var grupos = (await _estoque.ListarGruposAsync(cancellationToken)).ToDictionary(g => g.Id);
        var itens = (await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken)).ToDictionary(i => i.Id);
        bool EhPote(ItemContagem linha) =>
            linha.ContadoUnidades is not null && itens.TryGetValue(linha.ItemEstoqueId, out var item)
            && grupos.GetValueOrDefault(item.GrupoId)?.ModoControle == ModoControle.SaldoPelaVenda;

        // A mais nova que contou potes: uma contagem só de picolés no meio não esconde a dos potes.
        var contagem = (await _contagens.ListarConcluidasEntreAsync(deUtc, ateUtc, cancellationToken)).FirstOrDefault(c => c.Itens.Any(EhPote));
        if (contagem?.ConcluidaEm is not { } concluidaEm)
        {
            return null;
        }

        var potes = contagem.Itens.Where(EhPote).ToList();
        var esperados = await SaldoEsperadoNaContagemAsync(contagem, potes.Select(p => p.ItemEstoqueId).ToList(), cancellationToken: cancellationToken);
        var linhas = potes
            .Where(l => esperados.TryGetValue(l.ItemEstoqueId, out var esperado) && Math.Abs(l.ContadoUnidades!.Value - esperado) > DiferencaQueAvisa)
            .Select(l => itens[l.ItemEstoqueId])
            .OrderBy(i => grupos[i.GrupoId].Ordem).ThenBy(i => i.Ordem).ThenBy(i => i.Nome)
            .Select(i => new MensagensWhatsApp.LinhaDiferencaContagem(
                MensagensWhatsApp.DescricaoDoItem(grupos[i.GrupoId].Nome, i.Nome),
                esperados[i.Id],
                potes.First(l => l.ItemEstoqueId == i.Id).ContadoUnidades!.Value))
            .ToList();
        return linhas.Count == 0 ? null : new MensagensWhatsApp.DiferencasDaContagem(DiaComercial.DataDe(concluidaEm), linhas);
    }

    private async Task<bool> EhPoteAsync(Guid itemEstoqueId, CancellationToken cancellationToken)
    {
        var item = await _estoque.ObterItemAsync(itemEstoqueId, cancellationToken);
        if (item is null)
        {
            return false;
        }

        var grupo = await _estoque.ObterGrupoAsync(item.GrupoId, cancellationToken);
        return grupo?.ModoControle == ModoControle.SaldoPelaVenda;
    }
}
