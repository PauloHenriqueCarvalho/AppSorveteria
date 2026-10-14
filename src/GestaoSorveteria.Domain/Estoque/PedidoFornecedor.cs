using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>Uma linha da sugestão calculada pela aplicação (RN-PF-02/03, RN-RC-04), já arredondada para o passo.</summary>
/// <param name="Passo">0,5 quando o item é caixa e o fornecedor aceita meia caixa; 1 no resto.</param>
public sealed record LinhaSugerida(ItemEstoque Item, decimal Passo, decimal SugeridoEmbalagens, OrigemLinhaPedido Origem, string? Observacao = null);

/// <summary>
/// O que a dona marcou na conferência para uma linha do pedido (só as diferentes; o resto "chegou certo").
/// <paramref name="CustoAtual"/>: custo do catálogo agora, para linha que estava com 0 no pedido e veio mesmo assim (sabor
/// trocado) — ela entra como "veio sem pedir" com o custo de agora, como o item que nem estava na lista (RN-RC-05).
/// </summary>
public sealed record LinhaRecebida(Guid ItemEstoqueId, decimal RecebidoEmbalagens, bool PedirDeNovo = true, decimal? CustoAtual = null);

/// <summary>Item que veio sem estar no pedido (sabor trocado). O custo é o do catálogo naquele momento.</summary>
public sealed record LinhaNaoPedida(ItemEstoque Item, decimal Passo, decimal RecebidoEmbalagens);

/// <summary>
/// Pedido a um fornecedor (RN-PF-04…08) e a conferência da entrega (RN-RC-01…03). Quantidades em embalagens
/// (meia caixa só quando o fornecedor aceita). Custo da embalagem guardado na linha no momento (snapshot, RN-PF-05).
/// Nada é apagado: correção de pedido enviado = cancelar com motivo e montar outro.
/// </summary>
public sealed class PedidoFornecedor : Entity
{
    public const decimal EmbalagensMaximo = 9999;

    private readonly List<ItemPedidoFornecedor> _itens = new();

    public Guid FornecedorId { get; private set; }

    /// <summary>Número para a folha e para a conversa com o fornecedor ("Pedido nº 12").</summary>
    public int Numero { get; private set; }

    public StatusPedido Status { get; private set; }
    public Guid CriadoPorUsuarioId { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public DateTime AtualizadoEm { get; private set; }
    public DateTime? EnviadoEm { get; private set; }
    /// <summary>Quando a entrega foi conferida no sistema.</summary>
    public DateTime? RecebidoEm { get; private set; }

    /// <summary>
    /// Quando a entrega chegou na loja (RN-RC-01: a dona pode conferir dias depois). É esta data que conta para o estoque:
    /// a entrega que chegou antes da contagem já está no que foi contado, mesmo que a conferência tenha sido depois.
    /// </summary>
    public DateTime? ChegouEm { get; private set; }

    public Guid? RecebidoPorUsuarioId { get; private set; }
    public DateTime? CanceladoEm { get; private set; }
    public Guid? CanceladoPorUsuarioId { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    /// <summary>Σ dos subtotais das linhas (pedido × custo, 2 casas cada — RN-PF-05): bate com a soma da folha.</summary>
    public decimal TotalPedido { get; private set; }

    /// <summary>Σ dos subtotais recebidos (recebido × custo, 2 casas cada), depois da conferência.</summary>
    public decimal? TotalRecebido { get; private set; }

    /// <summary>RN-RC-03: Σ min(recebido, pedido) ÷ Σ pedido, em %, 2 casas.</summary>
    public decimal? PercentualAtendido { get; private set; }

    public IReadOnlyCollection<ItemPedidoFornecedor> Itens => _itens.AsReadOnly();

    public bool EhRascunho => Status == StatusPedido.Rascunho;

    // EF Core
    private PedidoFornecedor()
    {
    }

    private PedidoFornecedor(Guid? id) : base(id)
    {
    }

    /// <summary>RN-PF-04: monta o rascunho com a sugestão. Uma linha por item (a primeira origem vale).</summary>
    public static PedidoFornecedor Montar(
        Fornecedor fornecedor, int numero, Guid usuarioId, DateTime agoraUtc, IEnumerable<LinhaSugerida> sugestao, Guid? id = null)
    {
        Guard.Contra(!fornecedor.Ativo, $"{fornecedor.Nome} está desativado: ative o fornecedor antes de montar o pedido.");
        Guard.Contra(numero < 1, "Número do pedido inválido.");
        var agora = Guard.Utc(agoraUtc, "a data do pedido");
        var pedido = new PedidoFornecedor(id)
        {
            FornecedorId = fornecedor.Id,
            Numero = numero,
            Status = StatusPedido.Rascunho,
            CriadoPorUsuarioId = Guard.NaoVazio(usuarioId, "quem montou o pedido"),
            CriadoEm = agora,
            AtualizadoEm = agora,
        };

        foreach (var linha in sugestao)
        {
            pedido.ExigirDoFornecedor(linha.Item);
            if (pedido.Linha(linha.Item.Id) is null)
            {
                pedido._itens.Add(ItemPedidoFornecedor.Sugerido(pedido.Id, linha));
            }
        }

        pedido.Recalcular();
        return pedido;
    }

    /// <summary>
    /// RN-PF-04: remontar mantém o que a dona alterou à mão. Linha não alterada recebe a sugestão nova; a que saiu da
    /// sugestão fica com 0 (não é apagada, para a dona ver que deixou de ser sugerida).
    /// </summary>
    public void Remontar(IEnumerable<LinhaSugerida> sugestao, DateTime agoraUtc)
    {
        ExigirRascunho();
        var agora = Guard.Utc(agoraUtc, "a data");
        var novas = new Dictionary<Guid, LinhaSugerida>();
        foreach (var linha in sugestao)
        {
            ExigirDoFornecedor(linha.Item);
            novas.TryAdd(linha.Item.Id, linha);
        }

        foreach (var linha in _itens)
        {
            linha.ReceberSugestao(novas.GetValueOrDefault(linha.ItemEstoqueId));
        }

        foreach (var nova in novas.Values.Where(n => Linha(n.Item.Id) is null))
        {
            _itens.Add(ItemPedidoFornecedor.Sugerido(Id, nova));
        }

        AtualizadoEm = agora;
        Recalcular();
    }

    /// <summary>+/− da dona (RN-PF-04/05): a linha passa a ser "alterada pela dona" e não muda mais ao remontar.</summary>
    public void AlterarQuantidade(Guid itemEstoqueId, decimal embalagens, DateTime agoraUtc)
    {
        ExigirRascunho();
        var linha = Linha(itemEstoqueId) ?? throw new DomainException("Este item não está no pedido. Use \"Adicionar item\".");
        linha.DefinirPedido(embalagens);
        AtualizadoEm = Guard.Utc(agoraUtc, "a data");
        Recalcular();
    }

    /// <summary>A dona acrescenta um item que a sugestão não trouxe.</summary>
    public void AdicionarItem(ItemEstoque item, decimal passo, decimal embalagens, DateTime agoraUtc)
    {
        ExigirRascunho();
        ExigirDoFornecedor(item);
        Guard.Contra(!item.Ativo, $"{item.Nome} está desativado no catálogo.");
        Guard.Contra(Linha(item.Id) is not null, $"{item.Nome} já está no pedido: ajuste a quantidade na linha.");
        var linha = ItemPedidoFornecedor.Sugerido(Id, new LinhaSugerida(item, passo, 0, OrigemLinhaPedido.AdicionadoPelaDona));
        linha.DefinirPedido(embalagens);
        _itens.Add(linha);
        AtualizadoEm = Guard.Utc(agoraUtc, "a data");
        Recalcular();
    }

    /// <summary>RN-PF-06: confirma o pedido (pelo menos uma linha &gt; 0). Depois disso as quantidades não mudam.</summary>
    public void Enviar(DateTime agoraUtc)
    {
        ExigirRascunho();
        var agora = Guard.Utc(agoraUtc, "a data do envio");
        Guard.Contra(agora < CriadoEm, "O envio não pode ser anterior à montagem do pedido.");
        Guard.Contra(_itens.All(i => i.PedidoEmbalagens == 0), "O pedido está vazio: ponha a quantidade de pelo menos um item.");
        Status = StatusPedido.Enviado;
        EnviadoEm = agora;
        AtualizadoEm = agora;
        Recalcular();
    }

    /// <summary>RN-PF-08: cancela rascunho ou pedido enviado, com motivo e quem cancelou. Recebido não é cancelado.</summary>
    public void Cancelar(string motivo, Guid usuarioId, DateTime agoraUtc)
    {
        Guard.Contra(Status is StatusPedido.Recebido or StatusPedido.Cancelado,
            Status == StatusPedido.Recebido ? "Pedido já recebido não pode ser cancelado." : "Este pedido já foi cancelado.");
        var motivoValido = Guard.Texto(motivo, "o motivo", 3, 200);
        var quem = Guard.NaoVazio(usuarioId, "quem cancelou");
        var agora = Guard.Utc(agoraUtc, "a data");
        Guard.Contra(agora < CriadoEm, "O cancelamento não pode ser anterior à montagem do pedido.");
        MotivoCancelamento = motivoValido;
        CanceladoPorUsuarioId = quem;
        CanceladoEm = agora;
        AtualizadoEm = agora;
        Status = StatusPedido.Cancelado;
    }

    /// <summary>
    /// RN-RC-01…03: conferência da entrega. Linha do pedido que não veio em <paramref name="diferencas"/> chegou certa
    /// (recebido = pedido) — "Chegou tudo certo" é a lista vazia. <paramref name="naoPedidos"/> são os sabores trocados.
    /// Calcula o total recebido e o % atendido.
    /// </summary>
    public void Receber(
        IEnumerable<LinhaRecebida> diferencas,
        IEnumerable<LinhaNaoPedida> naoPedidos,
        Guid usuarioId,
        DateTime agoraUtc,
        DateTime? chegouEmUtc = null)
    {
        Guard.Contra(Status != StatusPedido.Enviado,
            Status == StatusPedido.Rascunho ? "Envie o pedido antes de conferir a entrega." : "Este pedido não está aguardando entrega.");
        var agora = Guard.Utc(agoraUtc, "a data da entrega");
        Guard.Contra(EnviadoEm is { } enviado && agora < enviado, "A entrega não pode ser anterior ao envio do pedido.");
        var chegou = chegouEmUtc is { } informado ? Guard.Utc(informado, "quando a entrega chegou") : agora;
        Guard.Contra(chegou > agora, "A entrega não pode ter chegado depois de agora.");
        Guard.Contra(EnviadoEm is { } envio && chegou < envio, "A entrega não pode ter chegado antes do envio do pedido.");
        var quemConferiu = Guard.NaoVazio(usuarioId, "quem conferiu");

        // Valida tudo antes de mudar qualquer linha: erro na conferência não deixa o pedido pela metade.
        var listaDiferencas = diferencas.ToList();
        var listaNaoPedidos = naoPedidos.ToList();
        var porItem = new Dictionary<Guid, LinhaRecebida>();
        foreach (var diferenca in listaDiferencas)
        {
            Guard.Contra(!porItem.TryAdd(diferenca.ItemEstoqueId, diferenca), "O mesmo item apareceu duas vezes na conferência.");
            var linhaDoPedido = Linha(diferenca.ItemEstoqueId) ?? throw new DomainException("Item que não está no pedido: registre como \"não pedido\".");
            linhaDoPedido.ValidarRecebido(diferenca.RecebidoEmbalagens);
            Guard.Contra(diferenca.CustoAtual < 0, "O custo não pode ser negativo.");
        }

        var novas = new List<ItemPedidoFornecedor>();
        foreach (var naoPedido in listaNaoPedidos)
        {
            ExigirDoFornecedor(naoPedido.Item);
            Guard.Contra(Linha(naoPedido.Item.Id) is not null,
                $"{naoPedido.Item.Nome} está no pedido: corrija a quantidade recebida na linha dele.");
            Guard.Contra(novas.Any(n => n.ItemEstoqueId == naoPedido.Item.Id), "O mesmo item apareceu duas vezes na conferência.");
            Guard.Contra(naoPedido.RecebidoEmbalagens <= 0, $"Não pedido sem quantidade: {naoPedido.Item.Nome}.");
            var linha = ItemPedidoFornecedor.Sugerido(Id, new LinhaSugerida(naoPedido.Item, naoPedido.Passo, 0, OrigemLinhaPedido.NaoPedido));
            linha.ValidarRecebido(naoPedido.RecebidoEmbalagens);
            novas.Add(linha);
        }

        foreach (var linha in _itens)
        {
            var recebida = porItem.GetValueOrDefault(linha.ItemEstoqueId);
            linha.DefinirRecebido(recebida?.RecebidoEmbalagens ?? linha.PedidoEmbalagens, recebida?.PedirDeNovo ?? true, recebida?.CustoAtual);
        }

        foreach (var (linha, naoPedido) in novas.Zip(listaNaoPedidos))
        {
            linha.DefinirRecebido(naoPedido.RecebidoEmbalagens, pedirDeNovo: false);
            _itens.Add(linha);
        }

        Status = StatusPedido.Recebido;
        RecebidoEm = agora;
        ChegouEm = chegou;
        RecebidoPorUsuarioId = quemConferiu;
        AtualizadoEm = agora;
        Recalcular();

        TotalRecebido = _itens.Sum(i => i.SubtotalRecebido ?? 0);
        var pedidoEmbalagens = _itens.Sum(i => i.PedidoEmbalagens);
        var atendidas = _itens.Sum(i => Math.Min(i.RecebidoEmbalagens ?? 0, i.PedidoEmbalagens));
        PercentualAtendido = pedidoEmbalagens == 0 ? null : Math.Round(atendidas / pedidoEmbalagens * 100m, 2, MidpointRounding.AwayFromZero);
    }

    public ItemPedidoFornecedor? Linha(Guid itemEstoqueId) => _itens.FirstOrDefault(i => i.ItemEstoqueId == itemEstoqueId);

    private void Recalcular() => TotalPedido = _itens.Sum(i => i.Subtotal);

    private void ExigirRascunho() =>
        Guard.Contra(Status != StatusPedido.Rascunho, "O pedido já foi enviado e não muda mais. Para corrigir, cancele com o motivo e monte outro.");

    private void ExigirDoFornecedor(ItemEstoque item) =>
        Guard.Contra(item.FornecedorId != FornecedorId, $"{item.Nome} é de outro fornecedor.");
}

/// <summary>Linha do pedido: sugerido, pedido, recebido (na conferência) e o custo da embalagem no momento.</summary>
public sealed class ItemPedidoFornecedor : Entity
{
    public Guid PedidoId { get; private set; }
    public Guid ItemEstoqueId { get; private set; }

    /// <summary>Passo do pedido: 0,5 (caixa, fornecedor aceita meia caixa) ou 1.</summary>
    public decimal Passo { get; private set; }

    /// <summary>Passo da conferência: meia caixa sempre vale para caixa (RN-RC-01), mesmo que o pedido seja em caixas inteiras.</summary>
    public decimal PassoRecebimento { get; private set; }

    public decimal SugeridoEmbalagens { get; private set; }
    public decimal PedidoEmbalagens { get; private set; }
    public decimal? RecebidoEmbalagens { get; private set; }

    /// <summary>Snapshot do custo quando a linha entrou no pedido (RN-PF-05); preço novo na nota não muda o pedido antigo (RN-RC-05).</summary>
    public decimal CustoEmbalagem { get; private set; }

    public bool AlteradoPelaDona { get; private set; }
    public OrigemLinhaPedido Origem { get; private set; }

    /// <summary>Para a dona: "Ana avisou: acabou", "Faltou no pedido nº 11".</summary>
    public string? Observacao { get; private set; }

    /// <summary>RN-RC-04: o que faltou volta no próximo pedido, salvo se a dona desmarcar.</summary>
    public bool PedirDeNovo { get; private set; } = true;

    /// <summary>Pedido × custo, 2 casas (RN-PF-05). O total do pedido é a soma destes.</summary>
    public decimal Subtotal => Moeda.Arredondar(PedidoEmbalagens * CustoEmbalagem);

    /// <summary>Recebido × custo, 2 casas; nulo antes da conferência.</summary>
    public decimal? SubtotalRecebido => RecebidoEmbalagens is { } recebido ? Moeda.Arredondar(recebido * CustoEmbalagem) : null;

    /// <summary>RN-RC-02. Nulo enquanto a entrega não foi conferida.</summary>
    public SituacaoRecebimento? Situacao => RecebidoEmbalagens is not { } recebido
        ? null
        : Origem == OrigemLinhaPedido.NaoPedido || (PedidoEmbalagens == 0 && recebido > 0) ? SituacaoRecebimento.NaoPedido
        : recebido < PedidoEmbalagens ? SituacaoRecebimento.Faltou
        : recebido > PedidoEmbalagens ? SituacaoRecebimento.VeioAMais
        : SituacaoRecebimento.Certo;

    /// <summary>RN-RC-02: (recebido − pedido) × custo; negativo = faltou.</summary>
    public decimal? DiferencaValor => SubtotalRecebido is { } recebido ? recebido - Subtotal : null;

    /// <summary>Embalagens que faltaram (0 se não faltou ou ainda não conferido).</summary>
    public decimal Faltou => RecebidoEmbalagens is { } recebido && recebido < PedidoEmbalagens ? PedidoEmbalagens - recebido : 0;

    // EF Core
    private ItemPedidoFornecedor()
    {
    }

    private ItemPedidoFornecedor(Guid? id) : base(id)
    {
    }

    internal static ItemPedidoFornecedor Sugerido(Guid pedidoId, LinhaSugerida linha)
    {
        Guard.Contra(linha.Passo is not (0.5m or 1m), "Passo do pedido inválido.");
        Guard.Contra(linha.Passo == 0.5m && linha.Item.Unidade != UnidadeCompra.Caixa, "Meia embalagem só vale para caixa.");
        var item = new ItemPedidoFornecedor(null)
        {
            PedidoId = pedidoId,
            ItemEstoqueId = linha.Item.Id,
            Passo = linha.Passo,
            PassoRecebimento = linha.Item.PassoEmbalagens,
            CustoEmbalagem = linha.Item.CustoEmbalagem,
            Origem = linha.Origem,
            Observacao = Guard.TextoOpcional(linha.Observacao, "a observação", 200),
        };
        item.SugeridoEmbalagens = item.Validar(linha.SugeridoEmbalagens, "A sugestão");
        item.PedidoEmbalagens = item.SugeridoEmbalagens;
        return item;
    }

    internal void ReceberSugestao(LinhaSugerida? linha)
    {
        if (linha is null)
        {
            SugeridoEmbalagens = 0;
            if (!AlteradoPelaDona && Origem != OrigemLinhaPedido.AdicionadoPelaDona)
            {
                PedidoEmbalagens = 0;
                Origem = OrigemLinhaPedido.Sugestao;
                Observacao = null;
            }

            return;
        }

        Guard.Contra(linha.Passo is not (0.5m or 1m), "Passo do pedido inválido.");
        // Passo, custo e marcações acompanham o catálogo e o fornecedor de agora; a quantidade que a dona mexeu fica.
        Passo = linha.Passo;
        PassoRecebimento = linha.Item.PassoEmbalagens;
        CustoEmbalagem = linha.Item.CustoEmbalagem;
        // Item que a dona acrescentou continua "acrescentado" enquanto não houver motivo novo (aviso, falta).
        Origem = Origem == OrigemLinhaPedido.AdicionadoPelaDona && linha.Origem == OrigemLinhaPedido.Sugestao ? Origem : linha.Origem;
        Observacao = Guard.TextoOpcional(linha.Observacao, "a observação", 200);
        SugeridoEmbalagens = Validar(linha.SugeridoEmbalagens, "A sugestão");
        PedidoEmbalagens = AlteradoPelaDona
            ? Math.Ceiling(PedidoEmbalagens / Passo) * Passo // fornecedor deixou de aceitar meia caixa: arredonda para cima
            : SugeridoEmbalagens;
    }

    internal void DefinirPedido(decimal embalagens)
    {
        PedidoEmbalagens = Validar(embalagens, "A quantidade");
        AlteradoPelaDona = true;
    }

    internal void ValidarRecebido(decimal embalagens) => Validar(embalagens, "A quantidade recebida", PassoRecebimento);

    /// <param name="custoAtual">Linha com 0 no pedido que veio mesmo assim: passa a valer o custo de agora (veio sem pedir).</param>
    internal void DefinirRecebido(decimal embalagens, bool pedirDeNovo, decimal? custoAtual = null)
    {
        RecebidoEmbalagens = Validar(embalagens, "A quantidade recebida", PassoRecebimento);
        PedirDeNovo = pedirDeNovo;
        if (PedidoEmbalagens == 0 && embalagens > 0 && custoAtual is { } custo)
        {
            CustoEmbalagem = custo; // o subtotal do pedido continua 0: o total pedido não muda
        }
    }

    private decimal Validar(decimal embalagens, string campo) => Validar(embalagens, campo, Passo);

    private static decimal Validar(decimal embalagens, string campo, decimal passo)
    {
        Guard.Contra(embalagens < 0, $"{campo} não pode ser negativa.");
        Guard.Contra(embalagens > PedidoFornecedor.EmbalagensMaximo, $"{campo} passa do máximo aceito.");
        Guard.Contra(embalagens % passo != 0, passo == 0.5m
            ? $"{campo} vai de meia em meia caixa (ex.: 2,5)."
            : $"{campo} deve ser um número inteiro de embalagens.");
        return embalagens;
    }
}
