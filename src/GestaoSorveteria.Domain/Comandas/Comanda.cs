using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Produtos;

namespace GestaoSorveteria.Domain.Comandas;

/// <summary>
/// Agregado de venda (RN-CM-*). Nasce aberta dentro de um caixa aberto, recebe itens,
/// fecha com pagamentos que somam exatamente o total, ou é cancelada. Depois de fechada é imutável;
/// só Admin estorna (vira Cancelada, nada é apagado).
/// </summary>
public sealed class Comanda : Entity
{
    public const string DescricaoVendaAvulsa = "Venda avulsa";

    private readonly List<ItemComanda> _itens = new();
    private readonly List<Pagamento> _pagamentos = new();

    /// <summary>RN-CM-02: sequencial dentro do caixa (1, 2, 3...).</summary>
    public int Numero { get; private set; }

    public Guid CaixaId { get; private set; }
    public Guid UsuarioId { get; private set; }
    public TipoComanda Tipo { get; private set; }
    public StatusComanda Status { get; private set; }
    public string? Observacao { get; private set; }
    public decimal Total { get; private set; }

    /// <summary>Hora do dispositivo (RN-CM-12).</summary>
    public DateTime CriadaEm { get; private set; }

    /// <summary>Hora em que o servidor recebeu (RN-CM-12).</summary>
    public DateTime RecebidaEm { get; private set; }

    public DateTime? FechadaEm { get; private set; }
    public DateTime? CanceladaEm { get; private set; }
    public string? MotivoCancelamento { get; private set; }

    /// <summary>RN-CX-08: chegou depois do fechamento do caixa a que pertence (sincronização atrasada).</summary>
    public bool RecebidaAposFechamentoCaixa { get; private set; }

    public IReadOnlyCollection<ItemComanda> Itens => _itens.AsReadOnly();
    public IReadOnlyCollection<Pagamento> Pagamentos => _pagamentos.AsReadOnly();

    public bool EstaAberta => Status == StatusComanda.Aberta;
    public bool EstaFechada => Status == StatusComanda.Fechada;

    /// <summary>RN-PG-05: só o que entrou na gaveta.</summary>
    public decimal TotalEmDinheiro => _pagamentos.Where(p => p.Forma == FormaPagamento.Dinheiro).Sum(p => p.Valor);

    public decimal TotalTroco => _pagamentos.Sum(p => p.Troco);

    // EF Core
    private Comanda()
    {
    }

    private Comanda(Guid? id, Guid caixaId, Guid usuarioId, int numero, TipoComanda tipo, DateTime criadaEmUtc, DateTime recebidaEmUtc, string? observacao)
        : base(id)
    {
        CaixaId = caixaId;
        UsuarioId = usuarioId;
        Numero = numero;
        Tipo = tipo;
        Status = StatusComanda.Aberta;
        CriadaEm = criadaEmUtc;
        RecebidaEm = recebidaEmUtc;
        Observacao = observacao;
        Total = 0m;
    }

    /// <summary>RN-CX-03: não há venda sem caixa aberto. RN-CM-01: o Id pode vir do app.</summary>
    public static Comanda Abrir(Caixa caixa, Guid usuarioId, int numero, TipoComanda tipo, DateTime criadaEmUtc, DateTime recebidaEmUtc, string? observacao = null, Guid? id = null)
    {
        Guard.Contra(caixa is null || !caixa.EstaAberto, "Não há caixa aberto. Abra o caixa antes de vender (RN-CX-03).");
        Guard.NaoVazio(usuarioId, "o usuário");
        Guard.Contra(numero < 1, "O número da comanda deve ser maior que zero.");
        Guard.Contra(!Enum.IsDefined(tipo), "Tipo de comanda desconhecido.");

        return new Comanda(
            id,
            caixa!.Id,
            usuarioId,
            numero,
            tipo,
            Guard.Utc(criadaEmUtc, "a data de criação"),
            Guard.Utc(recebidaEmUtc, "a data de recebimento"),
            Guard.TextoOpcional(observacao, "a observação", 300));
    }

    /// <summary>
    /// RN-SY-06 / RN-CX-08: remonta no servidor a comanda que o app já fechou ou cancelou, sobre o
    /// <see cref="Restaurar"/> (mesmas regras de itens, total, pagamentos e troco) com as conferências da sincronização:
    /// <list type="bullet">
    /// <item>descrição e preço praticados na venda (RN-PR-04), mesmo que o produto tenha mudado ou sido desativado depois;</item>
    /// <item>subtotal, total e troco enviados pelo app precisam bater com os calculados;</item>
    /// <item>só <c>Fechada</c> ou <c>Cancelada</c>; cancelada no celular não tem pagamentos (estorno é só pelo painel, RN-CM-09);</item>
    /// <item>dispensa o caixa aberto (exceção à RN-CX-03): comanda de caixa já fechado é aceita e marcada para conferência,
    /// sem mudar os valores do caixa (RN-CX-07).</item>
    /// </list>
    /// </summary>
    public static Comanda Remontar(
        Caixa caixa,
        Guid id,
        Guid usuarioId,
        int numero,
        TipoComanda tipo,
        StatusComanda status,
        DateTime criadaEmUtc,
        DateTime recebidaEmUtc,
        string? observacao,
        IEnumerable<DadosItemRecebido> itens,
        decimal totalInformado,
        IEnumerable<DadosPagamento>? pagamentos = null,
        DateTime? fechadaEmUtc = null,
        DateTime? canceladaEmUtc = null,
        string? motivoCancelamento = null)
    {
        Guard.Contra(caixa is null, "Informe o caixa da comanda.");
        Guard.Contra(status is not (StatusComanda.Fechada or StatusComanda.Cancelada),
            "Só comandas fechadas ou canceladas são sincronizadas.");
        Guard.Utc(criadaEmUtc, "a data de criação");
        // Abertura do caixa e criação da comanda vêm do mesmo relógio (o celular): venda antes da abertura não existe.
        Guard.Contra(criadaEmUtc < caixa!.AbertoEm, "A comanda foi criada antes da abertura do caixa (RN-CX-03).");

        var listaPagamentos = (pagamentos ?? Enumerable.Empty<DadosPagamento>()).ToList();
        if (status == StatusComanda.Cancelada)
        {
            Guard.Contra(listaPagamentos.Count > 0 || fechadaEmUtc is not null,
                "Comanda cancelada no celular não pode ter pagamentos nem fechamento (estorno é só pelo painel, RN-CM-09).");
        }

        var listaItens = (itens ?? Enumerable.Empty<DadosItemRecebido>()).ToList();
        foreach (var dados in listaItens)
        {
            Guard.NaoVazio(dados.Id, "o Id do item");
            Guard.Contra(dados.ProdutoId == Guid.Empty, "Item livre deve vir sem produto (RN-CM-03).");
        }

        var comanda = Restaurar(
            id,
            caixa.Id,
            usuarioId,
            numero,
            tipo,
            status,
            criadaEmUtc,
            recebidaEmUtc,
            observacao,
            listaItens.Select(d => new DadosItem(d.Id, d.ProdutoId, d.Descricao, d.Quantidade, d.PrecoUnitario)),
            listaPagamentos,
            fechadaEmUtc,
            canceladaEmUtc,
            motivoCancelamento);

        foreach (var dados in listaItens)
        {
            var item = comanda._itens.Single(i => i.Id == dados.Id);
            Guard.Contra(item.Subtotal != dados.Subtotal,
                $"O subtotal de \"{item.Descricao}\" enviado ({dados.Subtotal:N2}) não confere com o calculado ({item.Subtotal:N2}) (RN-CM-05).");
        }

        Guard.Contra(comanda.Total != totalInformado,
            $"O total enviado ({totalInformado:N2}) não confere com a soma dos itens ({comanda.Total:N2}) (RN-CM-05).");

        comanda.RecebidaAposFechamentoCaixa = !caixa.EstaAberto;
        return comanda;
    }

    /// <summary>
    /// RN-CM-03/04: produto cadastrado. Para produto com valor livre, <paramref name="valorInformado"/> é obrigatório.
    /// Mesmo produto (mesmo preço) soma na linha existente.
    /// </summary>
    public ItemComanda AdicionarProduto(Produto produto, int quantidade, decimal? valorInformado = null, Guid? itemId = null)
    {
        ExigirAberta();
        Guard.Contra(produto is null, "Informe o produto.");
        Guard.Contra(!produto!.Ativo, $"O produto \"{produto.Nome}\" está desativado.");

        decimal preco;
        if (produto.PermiteValorLivre)
        {
            Guard.Contra(valorInformado is null, $"Informe o valor de \"{produto.Nome}\" (RN-PR-02).");
            preco = Guard.Dinheiro(valorInformado!.Value, "o valor informado", permiteZero: false);
        }
        else
        {
            Guard.Contra(valorInformado is not null && valorInformado != produto.Preco,
                $"O produto \"{produto.Nome}\" não aceita valor livre.");
            preco = produto.Preco;
        }

        if (itemId is { } idInformado)
        {
            var jaExiste = _itens.FirstOrDefault(i => i.Id == idInformado);
            if (jaExiste is not null)
            {
                return jaExiste; // idempotência (RN-SY-02)
            }
        }

        if (!produto.PermiteValorLivre)
        {
            var linha = _itens.FirstOrDefault(i => i.ProdutoId == produto.Id && i.PrecoUnitario == preco);
            if (linha is not null)
            {
                linha.Somar(quantidade);
                RecalcularTotal();
                return linha;
            }
        }

        var item = new ItemComanda(Id, produto.Id, produto.Nome, quantidade, preco, itemId);
        _itens.Add(item);
        RecalcularTotal();
        return item;
    }

    /// <summary>RN-CM-03: item sem produto (self-service, venda avulsa).</summary>
    public ItemComanda AdicionarItemLivre(string descricao, decimal valor, int quantidade = 1, Guid? itemId = null)
    {
        ExigirAberta();

        if (itemId is { } idInformado)
        {
            var jaExiste = _itens.FirstOrDefault(i => i.Id == idInformado);
            if (jaExiste is not null)
            {
                return jaExiste;
            }
        }

        var item = new ItemComanda(Id, null, descricao, quantidade, valor, itemId);
        _itens.Add(item);
        RecalcularTotal();
        return item;
    }

    public void AlterarQuantidade(Guid itemId, int quantidade)
    {
        ExigirAberta();
        ObterItem(itemId).AlterarQuantidade(quantidade);
        RecalcularTotal();
    }

    /// <summary>RN-CM-06.</summary>
    public void RemoverItem(Guid itemId)
    {
        ExigirAberta();
        _itens.Remove(ObterItem(itemId));
        RecalcularTotal();
    }

    public void AlterarObservacao(string? observacao)
    {
        ExigirAberta();
        Observacao = Guard.TextoOpcional(observacao, "a observação", 300);
    }

    /// <summary>RN-CM-07 + RN-PG-02/03: pagamentos somam exatamente o total.</summary>
    public void Fechar(IEnumerable<DadosPagamento> pagamentos, DateTime agoraUtc)
    {
        ExigirAberta();
        Guard.Utc(agoraUtc, "a data de fechamento");
        Guard.Contra(_itens.Count == 0, "A comanda não tem itens. Adicione itens ou cancele a comanda.");
        Guard.Contra(Total <= 0, "O total da comanda deve ser maior que zero para fechar.");

        var lista = (pagamentos ?? Enumerable.Empty<DadosPagamento>()).ToList();
        Guard.Contra(lista.Count == 0, "Informe pelo menos um pagamento.");

        var novos = lista.Select(dados => new Pagamento(Id, dados)).ToList();
        var soma = Moeda.Arredondar(novos.Sum(p => p.Valor));
        Guard.Contra(soma != Total,
            $"Os pagamentos somam {soma:N2}, mas o total da comanda é {Total:N2}.");

        _pagamentos.Clear();
        _pagamentos.AddRange(novos);
        FechadaEm = agoraUtc;
        Status = StatusComanda.Fechada;
    }

    /// <summary>RN-CM-08: só comanda aberta; motivo opcional.</summary>
    public void Cancelar(string? motivo, DateTime agoraUtc)
    {
        ExigirAberta();
        MotivoCancelamento = Guard.TextoOpcional(motivo, "o motivo", 300);
        CanceladaEm = Guard.Utc(agoraUtc, "a data de cancelamento");
        Status = StatusComanda.Cancelada;
    }

    /// <summary>RN-CM-09: só comanda fechada, motivo obrigatório. Quem pode (Admin) é decidido na Application.</summary>
    public void Estornar(string motivo, DateTime agoraUtc)
    {
        Guard.Contra(!EstaFechada, "Só é possível estornar uma comanda fechada.");
        MotivoCancelamento = Guard.Texto(motivo, "o motivo do estorno", 3, 300);
        CanceladaEm = Guard.Utc(agoraUtc, "a data do estorno");
        Status = StatusComanda.Cancelada;
    }

    /// <summary>RN-CX-08.</summary>
    public void MarcarRecebidaAposFechamentoCaixa() => RecebidaAposFechamentoCaixa = true;

    /// <summary>
    /// Reconstrói uma comanda já gravada (ex.: SQLite do app) para aplicar as regras de novo.
    /// Não é regra nova: itens e pagamentos passam pelas mesmas validações, o total é recalculado
    /// (RN-CM-05) e dados incoerentes com o status lançam <see cref="DomainException"/>.
    /// Não confere o caixa (RN-CX-03 vale só na abertura).
    /// </summary>
    public static Comanda Restaurar(
        Guid id,
        Guid caixaId,
        Guid usuarioId,
        int numero,
        TipoComanda tipo,
        StatusComanda status,
        DateTime criadaEmUtc,
        DateTime recebidaEmUtc,
        string? observacao,
        IEnumerable<DadosItem> itens,
        IEnumerable<DadosPagamento>? pagamentos = null,
        DateTime? fechadaEmUtc = null,
        DateTime? canceladaEmUtc = null,
        string? motivoCancelamento = null)
    {
        Guard.NaoVazio(id, "a comanda");
        Guard.NaoVazio(caixaId, "o caixa");
        Guard.NaoVazio(usuarioId, "o usuário");
        Guard.Contra(numero < 1, "O número da comanda deve ser maior que zero.");
        Guard.Contra(!Enum.IsDefined(tipo), "Tipo de comanda desconhecido.");
        Guard.Contra(!Enum.IsDefined(status), "Status de comanda desconhecido.");

        var comanda = new Comanda(
            id,
            caixaId,
            usuarioId,
            numero,
            tipo,
            Guard.Utc(criadaEmUtc, "a data de criação"),
            Guard.Utc(recebidaEmUtc, "a data de recebimento"),
            Guard.TextoOpcional(observacao, "a observação", 300));

        foreach (var dados in itens ?? Enumerable.Empty<DadosItem>())
        {
            Guard.Contra(comanda._itens.Any(i => i.Id == dados.Id), "Item repetido na comanda.");
            comanda._itens.Add(new ItemComanda(id, dados.ProdutoId, dados.Descricao, dados.Quantidade, dados.PrecoUnitario, dados.Id));
        }

        comanda.RecalcularTotal();

        var listaPagamentos = (pagamentos ?? Enumerable.Empty<DadosPagamento>()).ToList();
        switch (status)
        {
            case StatusComanda.Aberta:
                Guard.Contra(listaPagamentos.Count > 0, "Comanda aberta não tem pagamentos.");
                break;

            case StatusComanda.Fechada:
                Guard.Contra(fechadaEmUtc is null, "Comanda fechada sem data de fechamento.");
                comanda.Fechar(listaPagamentos, fechadaEmUtc!.Value); // mesmas regras: RN-CM-07, RN-PG-*
                break;

            case StatusComanda.Cancelada:
                Guard.Contra(canceladaEmUtc is null, "Comanda cancelada sem data de cancelamento.");
                comanda._pagamentos.AddRange(listaPagamentos.Select(dados => new Pagamento(id, dados)));
                comanda.FechadaEm = fechadaEmUtc is { } fechada ? Guard.Utc(fechada, "a data de fechamento") : null;
                comanda.CanceladaEm = Guard.Utc(canceladaEmUtc!.Value, "a data de cancelamento");
                comanda.MotivoCancelamento = Guard.TextoOpcional(motivoCancelamento, "o motivo", 300);
                comanda.Status = StatusComanda.Cancelada;
                break;
        }

        return comanda;
    }

    private ItemComanda ObterItem(Guid itemId) =>
        _itens.FirstOrDefault(i => i.Id == itemId)
        ?? throw new DomainException("Item não encontrado nesta comanda.");

    private void RecalcularTotal() => Total = Moeda.Arredondar(_itens.Sum(i => i.Subtotal));

    private void ExigirAberta() => Guard.Contra(!EstaAberta, "Esta comanda não está aberta (RN-CM-07).");
}
