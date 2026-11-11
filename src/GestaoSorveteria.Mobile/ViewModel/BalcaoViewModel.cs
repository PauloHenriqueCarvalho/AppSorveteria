using System.Collections.ObjectModel;
using System.Windows.Input;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using NotaParaTroco = GestaoSorveteria.Domain.Comandas.NotaParaTroco;
using NotasDoTroco = GestaoSorveteria.Domain.Comandas.NotasDoTroco;
using ComandaDominio = GestaoSorveteria.Domain.Comandas.Comanda;
using DadosPagamento = GestaoSorveteria.Domain.Comandas.DadosPagamento;
using FormaPagamento = GestaoSorveteria.Domain.Comandas.FormaPagamento;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// RN-CM-13: uma linha da venda do balcão, ainda não gravada (nada foi pago). Tocar de novo no mesmo produto (e sabor)
/// soma 1. Valor digitado e produto de valor livre ficam sempre numa linha própria.
/// </summary>
public sealed class LinhaDoBalcao : BindableObject
{
    public LinhaDoBalcao(Guid? produtoId, Guid? saborId, string descricao, decimal precoUnitario, bool valorLivre)
    {
        ProdutoId = produtoId;
        SaborId = saborId;
        Descricao = descricao;
        PrecoUnitario = precoUnitario;
        ValorLivre = valorLivre;
    }

    public Guid? ProdutoId { get; }
    public Guid? SaborId { get; }
    public string Descricao { get; }
    public decimal PrecoUnitario { get; }
    public bool ValorLivre { get; }

    /// <summary>Self-service e valor avulso ficam em 1: o "+" dobraria o valor da balança. Para mudar, tira e digita de novo.</summary>
    public bool PodeMudarQuantidade => !ValorLivre;

    private int _quantidade = 1;
    public int Quantidade
    {
        get => _quantidade;
        set
        {
            _quantidade = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Subtotal));
        }
    }

    /// <summary>Só para mostrar na linha; o total cobrado vem do Domain (<see cref="ComandaAppService.TotalDoBalcaoAsync"/>).</summary>
    public decimal Subtotal => Moeda.Arredondar(PrecoUnitario * Quantidade);

    public bool Mesmo(Guid produtoId, Guid? saborId) => !ValorLivre && ProdutoId == produtoId && SaborId == saborId;

    public ItemDoBalcao ParaVenda() => ProdutoId is { } id
        ? new ItemDoBalcao(id, Quantidade, SaborId, ValorLivre ? PrecoUnitario : null)
        : new ItemDoBalcao(null, Quantidade, null, PrecoUnitario, Descricao);
}

/// <summary>
/// RN-CM-13: tela do balcão, para o cliente que pega, paga e vai embora. É a primeira aba do app. Botões grandes com os
/// produtos na ordem que a dona escolhe no painel (não mudam de lugar durante o dia), toque soma na venda, e o
/// pagamento é 1 toque: Pix, Débito, Crédito ou Dinheiro gravam a comanda já paga numa operação só. No dinheiro, o
/// atendente toca no que o cliente entregou (exato ou a nota). Self-service e valor avulso usam o teclado da própria tela.
/// Pote: 1 toque no sabor ou em "Sem sabor" (RN-ES-09). Depois de gravar, a tela fica pronta para o próximo cliente.
/// </summary>
public class BalcaoViewModel : BindableObject
{
    public const string Todos = "Todos";

    /// <summary>Até R$ 99.999,99 no teclado (7 dígitos em centavos).</summary>
    private const int DigitosMaximos = 7;

    private readonly ComandaAppService _comandas;
    private readonly ProdutoRepository _produtos;
    private readonly ItemEstoqueRepository _itensEstoque;
    private readonly CaixaAppService _caixa;
    private readonly IPaymentService _pagamento;

    private List<Produto> _todos = new();
    private int _versaoTotal;
    private bool _cobrando;

    /// <summary>Entre o toque na forma de pagamento e a gravação (caixa e total pelo Domain): segura toques repetidos.</summary>
    private bool _preparandoPagamento;

    /// <summary>Os itens que deram o total do dinheiro aberto: o troco paga exatamente essa venda.</summary>
    private List<ItemDoBalcao> _itensDoTroco = [];

    public ObservableCollection<Produto> Produtos { get; } = new();
    public ObservableCollection<string> Categorias { get; } = new() { Todos };
    public ObservableCollection<LinhaDoBalcao> Itens { get; } = new();

    private bool _semProdutos;
    public bool SemProdutos
    {
        get => _semProdutos;
        set { _semProdutos = value; OnPropertyChanged(); }
    }

    private bool _caixaFechado;
    /// <summary>C3: faixa "Caixa fechado — toque para abrir". Os botões continuam visíveis; cobrar pede o caixa.</summary>
    public bool CaixaFechado
    {
        get => _caixaFechado;
        set { _caixaFechado = value; OnPropertyChanged(); }
    }

    private string _categoriaSelecionada = Todos;
    public string CategoriaSelecionada
    {
        get => _categoriaSelecionada;
        set
        {
            var nova = string.IsNullOrEmpty(value) ? Todos : value;
            if (_categoriaSelecionada == nova) return;
            _categoriaSelecionada = nova;
            OnPropertyChanged();
            AplicarFiltro();
        }
    }

    private decimal _total;
    public decimal Total
    {
        get => _total;
        private set
        {
            _total = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TextoTotal));
        }
    }

    public bool TemItens => Itens.Count > 0;
    public bool SemItens => Itens.Count == 0;
    public string TextoTotal => $"R$ {Total:F2}";

    /// <summary>Algum painel cobre os botões (sabor, teclado ou troco): toques nos produtos e no pagamento esperam.</summary>
    private bool Ocupado => EscolhendoSabor || DigitandoValor || EscolhendoTroco || _cobrando || _preparandoPagamento;

    // ---------------------------------------------------------------- RN-ES-09: grade de sabores, só nos potes

    private Produto? _produtoDoSabor;

    private bool _escolhendoSabor;
    public bool EscolhendoSabor
    {
        get => _escolhendoSabor;
        set { _escolhendoSabor = value; OnPropertyChanged(); }
    }

    private string _tituloSabor = string.Empty;
    public string TituloSabor
    {
        get => _tituloSabor;
        set { _tituloSabor = value; OnPropertyChanged(); }
    }

    private IReadOnlyList<SaborParaEscolher> _sabores = [];
    public IReadOnlyList<SaborParaEscolher> Sabores
    {
        get => _sabores;
        set { _sabores = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------- teclado: self-service e valor avulso (RN-PR-02, RN-CM-03/10)

    /// <summary>Produto de valor livre sendo digitado (self-service). Nulo com o teclado aberto = valor avulso.</summary>
    private Produto? _produtoDoValor;
    private string _digitos = string.Empty;

    private bool _digitandoValor;
    public bool DigitandoValor
    {
        get => _digitandoValor;
        set { _digitandoValor = value; OnPropertyChanged(); }
    }

    private string _tituloValor = string.Empty;
    public string TituloValor
    {
        get => _tituloValor;
        set { _tituloValor = value; OnPropertyChanged(); }
    }

    /// <summary>Digitado em centavos, como na maquininha: 1-5-5-0 → R$ 15,50.</summary>
    public decimal ValorDigitado => _digitos.Length == 0 ? 0m : decimal.Parse(_digitos) / 100m;
    public string TextoValorDigitado => $"R$ {ValorDigitado:F2}";

    private string _erroValor = string.Empty;
    public string ErroValor
    {
        get => _erroValor;
        set { _erroValor = value; OnPropertyChanged(); }
    }

    // ---------------------------------------------------------------- dinheiro: quanto o cliente entregou (RN-PG-03)

    private bool _escolhendoTroco;
    public bool EscolhendoTroco
    {
        get => _escolhendoTroco;
        set { _escolhendoTroco = value; OnPropertyChanged(); }
    }

    private decimal _totalDoTroco;
    public string TituloTroco => $"Dinheiro — R$ {_totalDoTroco:F2}";

    private IReadOnlyList<NotaParaTroco> _notas = [];
    public IReadOnlyList<NotaParaTroco> Notas
    {
        get => _notas;
        set { _notas = value; OnPropertyChanged(); }
    }

    public ICommand TocarProdutoCommand { get; }
    public ICommand EscolherSaborCommand { get; }
    public ICommand SemSaborCommand { get; }
    public ICommand CancelarSaborCommand { get; }
    public ICommand ValorAvulsoCommand { get; }
    public ICommand TeclaCommand { get; }
    public ICommand ApagarTeclaCommand { get; }
    public ICommand ConfirmarValorCommand { get; }
    public ICommand CancelarValorCommand { get; }
    public ICommand MaisUmCommand { get; }
    public ICommand MenosUmCommand { get; }
    public ICommand DigitarQuantidadeCommand { get; }
    public ICommand LimparCommand { get; }
    public ICommand PagarCommand { get; }
    public ICommand NotaCommand { get; }
    public ICommand OutroValorRecebidoCommand { get; }
    public ICommand CancelarTrocoCommand { get; }
    public ICommand DividirCommand { get; }
    public ICommand AbrirCaixaCommand { get; }

    public BalcaoViewModel(ComandaAppService comandas, ProdutoRepository produtos, ItemEstoqueRepository itensEstoque,
        CaixaAppService caixa, IPaymentService pagamento)
    {
        _comandas = comandas;
        _produtos = produtos;
        _itensEstoque = itensEstoque;
        _caixa = caixa;
        _pagamento = pagamento;

        Itens.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemItens));
            OnPropertyChanged(nameof(SemItens));
        };

        TocarProdutoCommand = new Command<Produto>(async p => await TocarProduto(p));
        EscolherSaborCommand = new Command<SaborParaEscolher>(EscolherSabor);
        SemSaborCommand = new Command(SemSabor);
        CancelarSaborCommand = new Command(FecharGradeDeSabores);
        ValorAvulsoCommand = new Command(() => AbrirTeclado(produto: null));
        TeclaCommand = new Command<string>(Tecla);
        ApagarTeclaCommand = new Command(ApagarTecla);
        ConfirmarValorCommand = new Command(ConfirmarValor);
        CancelarValorCommand = new Command(FecharTeclado);
        MaisUmCommand = new Command<LinhaDoBalcao>(l => { if (l is not null) AlterarQuantidade(l, l.Quantidade + 1); });
        MenosUmCommand = new Command<LinhaDoBalcao>(l => { if (l is not null) AlterarQuantidade(l, l.Quantidade - 1); });
        DigitarQuantidadeCommand = new Command<LinhaDoBalcao>(async l => await DigitarQuantidade(l));
        LimparCommand = new Command(async () => await Limpar());
        PagarCommand = new Command<string>(async f => await Pagar(f));
        NotaCommand = new Command<NotaParaTroco>(async n => await ReceberDinheiro(n?.Valor));
        OutroValorRecebidoCommand = new Command(async () => await OutroValorRecebido());
        CancelarTrocoCommand = new Command(FecharTroco);
        DividirCommand = new Command(async () => await Dividir());
        AbrirCaixaCommand = new Command(async () => await Shell.Current.GoToAsync("//CaixaView"));
    }

    /// <summary>
    /// Ao abrir a aba: carrega os botões na ordem do painel (RN-PR-05) e mostra se o caixa está fechado. Não trava a tela
    /// com alerta: o caixa só é exigido na hora de cobrar (RN-CX-03).
    /// </summary>
    public async Task AoAparecerAsync()
    {
        await Operacao.CarregarAsync(async () =>
        {
            var caixa = await _caixa.ObterAbertoLinhaAsync();
            var lista = await _produtos.ListarAtivosAsync(); // cache do catálogo da API (C2), já na ordem dos botões
            var modos = await _itensEstoque.ModosDosGruposAsync();
            foreach (var produto in lista)
            {
                produto.PedeSaborNaVenda = produto.PedeSaborNoModo(ModoDoGrupo(modos, produto));
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                CaixaFechado = caixa is null;
                _todos = lista;
                SemProdutos = lista.Count == 0;
                var categoriaAtual = CategoriaSelecionada;
                Categorias.Clear();
                Categorias.Add(Todos);
                // Categorias na ordem em que aparecem nos botões (a dona já ordenou), não em ordem alfabética
                foreach (var categoria in lista.Select(p => p.Categoria).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct())
                {
                    Categorias.Add(categoria);
                }

                _categoriaSelecionada = Categorias.Contains(categoriaAtual) ? categoriaAtual : Todos;
                OnPropertyChanged(nameof(CategoriaSelecionada));
                AplicarFiltro();
            });
        });
    }

    private async Task TocarProduto(Produto? produto)
    {
        if (produto is null || Ocupado) return;

        if (produto.PermiteValorLivre)
        {
            AbrirTeclado(produto); // RN-PR-02: self-service por peso → valor no teclado da tela
            return;
        }

        SaboresDoProduto? sabores = null;
        if (!await Operacao.CarregarAsync(async () => sabores = await _comandas.SaboresAsync(produto.Id)) || sabores is null)
        {
            return;
        }

        if (Ocupado) return; // outro painel abriu enquanto os sabores carregavam

        if (sabores.PedeSabor && sabores.Sabores.Count > 0)
        {
            _produtoDoSabor = produto;
            TituloSabor = $"{produto.Nome} — R$ {produto.Preco:F2}";
            Sabores = sabores.Sabores.Select(s => new SaborParaEscolher(s.Id, s.Nome)).ToList();
            EscolhendoSabor = true;
            return;
        }

        Somar(produto, saborId: null, produto.Nome);
    }

    private void EscolherSabor(SaborParaEscolher? sabor)
    {
        var produto = _produtoDoSabor;
        if (produto is null || sabor is null) return;
        FecharGradeDeSabores();
        Somar(produto, sabor.Id, $"{produto.Nome} · {sabor.Nome}");
    }

    /// <summary>RN-ES-09: com fila no balcão o atendente pula o sabor; a venda entra sem baixa e a contagem corrige.</summary>
    private void SemSabor()
    {
        var produto = _produtoDoSabor;
        if (produto is null) return;
        FecharGradeDeSabores();
        Somar(produto, saborId: null, produto.Nome);
    }

    private void FecharGradeDeSabores()
    {
        _produtoDoSabor = null;
        EscolhendoSabor = false;
        Sabores = [];
    }

    private void Somar(Produto produto, Guid? saborId, string descricao)
    {
        var linha = Itens.FirstOrDefault(l => l.Mesmo(produto.Id, saborId));
        if (linha is not null)
        {
            linha.Quantidade = Math.Min(linha.Quantidade + 1, GestaoSorveteria.Domain.Comandas.ItemComanda.QuantidadeMaxima);
        }
        else
        {
            Itens.Add(new LinhaDoBalcao(produto.Id, saborId, descricao, produto.Preco, valorLivre: false));
        }

        AtualizarTotal();
    }

    // ---------------------------------------------------------------- teclado

    private void AbrirTeclado(Produto? produto)
    {
        if (Ocupado) return;
        _produtoDoValor = produto;
        _digitos = string.Empty;
        TituloValor = produto?.Nome ?? "Valor avulso";
        ErroValor = string.Empty;
        AvisarValor();
        DigitandoValor = true;
    }

    private void Tecla(string? tecla)
    {
        if (string.IsNullOrEmpty(tecla) || !tecla.All(char.IsAsciiDigit)) return;
        var novo = (_digitos + tecla).TrimStart('0');
        if (novo.Length > DigitosMaximos) return;
        _digitos = novo;
        ErroValor = string.Empty;
        AvisarValor();
    }

    private void ApagarTecla()
    {
        if (_digitos.Length == 0) return;
        _digitos = _digitos[..^1];
        AvisarValor();
    }

    /// <summary>RN-CM-03: valor &gt; 0. Self-service entra no produto de valor livre; o resto como "Venda avulsa".</summary>
    private void ConfirmarValor()
    {
        var valor = ValorDigitado;
        if (valor <= 0)
        {
            ErroValor = "Digite o valor da venda.";
            return;
        }

        var produto = _produtoDoValor;
        Itens.Add(produto is null
            ? new LinhaDoBalcao(null, null, ComandaDominio.DescricaoVendaAvulsa, valor, valorLivre: true)
            : new LinhaDoBalcao(produto.Id, null, produto.Nome, valor, valorLivre: true));
        FecharTeclado();
        AtualizarTotal();
    }

    private void FecharTeclado()
    {
        _produtoDoValor = null;
        _digitos = string.Empty;
        ErroValor = string.Empty;
        DigitandoValor = false;
        AvisarValor();
    }

    private void AvisarValor()
    {
        OnPropertyChanged(nameof(ValorDigitado));
        OnPropertyChanged(nameof(TextoValorDigitado));
    }

    // ---------------------------------------------------------------- itens

    private void AlterarQuantidade(LinhaDoBalcao? linha, int quantidade)
    {
        if (linha is null || Ocupado || !linha.PodeMudarQuantidade && quantidade > 0) return;
        if (quantidade <= 0)
        {
            Itens.Remove(linha);
        }
        else
        {
            linha.Quantidade = Math.Min(quantidade, GestaoSorveteria.Domain.Comandas.ItemComanda.QuantidadeMaxima);
        }

        AtualizarTotal();
    }

    private async Task DigitarQuantidade(LinhaDoBalcao? linha)
    {
        if (linha is null || Ocupado || !linha.PodeMudarQuantidade) return;
        if (await PedirQuantidade.PerguntarAsync(linha.Descricao, linha.Quantidade) is { } quantidade)
        {
            AlterarQuantidade(linha, quantidade); // RN-ES-04
        }
    }

    private async Task Limpar()
    {
        if (!TemItens || Ocupado) return;
        if (await Shell.Current.DisplayAlertAsync("Limpar", "Tirar todos os itens desta venda? Nada foi cobrado.", "Limpar", "Voltar"))
        {
            LimparVenda();
        }
    }

    /// <summary>Voltar do aparelho com venda montada: descarta depois de confirmar (nada foi gravado nem pago).</summary>
    public void LimparVenda()
    {
        FecharGradeDeSabores();
        FecharTeclado();
        FecharTroco();
        Itens.Clear();
        _versaoTotal++;
        Total = 0m;
    }

    /// <summary>
    /// Total na tela pelo Domain, a cada toque. Toques rápidos: só vale a conta mais nova. Se a conta falhar, mostra a soma
    /// das linhas; o pagamento refaz a conta pelo Domain antes de gravar.
    /// </summary>
    private async void AtualizarTotal()
    {
        var versao = ++_versaoTotal;
        var itens = Itens.Select(l => l.ParaVenda()).ToList();
        decimal total;
        try
        {
            total = await _comandas.TotalDoBalcaoAsync(itens);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sorveteria] Total do balcão pela soma das linhas: {ex.Message}");
            total = Moeda.Arredondar(Itens.Sum(l => l.Subtotal));
        }

        if (versao == _versaoTotal)
        {
            Total = total;
        }
    }

    // ---------------------------------------------------------------- pagamento (RN-CM-13, RN-PG-*)

    /// <summary>
    /// 1 toque na forma: Pix e cartão gravam na hora pelo total; dinheiro abre "quanto o cliente entregou".
    /// O total é sempre refeito pelo Domain antes (nunca o número da tela).
    /// </summary>
    private async Task Pagar(string? forma)
    {
        if (!Enum.TryParse<FormaPagamento>(forma, out var formaPagamento) || !PodeComecarPagamento()) return;

        _preparandoPagamento = true;
        try
        {
            var conta = await ContaParaCobrarAsync();
            if (conta is null || _cobrando || !TemItens) return;

            if (formaPagamento == FormaPagamento.Dinheiro)
            {
                _itensDoTroco = conta.Value.Itens;
                _totalDoTroco = conta.Value.Total;
                OnPropertyChanged(nameof(TituloTroco));
                Notas = NotasDoTroco.Para(conta.Value.Total);
                EscolhendoTroco = true;
                return;
            }

            await GravarAsync(conta.Value.Itens, [new DadosPagamento(formaPagamento, conta.Value.Total)]);
        }
        finally
        {
            _preparandoPagamento = false;
        }
    }

    /// <summary>
    /// Tocou numa forma de pagamento com o teclado ou o sabor aberto: avisa o que falta, em vez de não fazer nada.
    /// Venda vazia ou já cobrando: ignora.
    /// </summary>
    private bool PodeComecarPagamento()
    {
        if (!TemItens || _cobrando || _preparandoPagamento || EscolhendoTroco) return false;
        if (DigitandoValor)
        {
            AvisoRapido.Mostrar("Toque em Adicionar (ou Voltar) antes de cobrar");
            return false;
        }

        if (EscolhendoSabor)
        {
            AvisoRapido.Mostrar("Escolha o sabor (ou Sem sabor) antes de cobrar");
            return false;
        }

        return true;
    }

    /// <summary>RN-PG-03: o cliente entregou esse valor (≥ total); o troco é calculado pelo Domain ao fechar.</summary>
    private async Task ReceberDinheiro(decimal? recebido)
    {
        if (!EscolhendoTroco || recebido is null || _cobrando) return;
        var total = _totalDoTroco;
        if (recebido.Value < total)
        {
            await Shell.Current.DisplayAlertAsync("Atenção", $"O cliente precisa entregar pelo menos R$ {total:F2}.", "OK");
            return;
        }

        var itens = _itensDoTroco;
        FecharTroco();
        await GravarAsync(itens, [new DadosPagamento(FormaPagamento.Dinheiro, total, recebido.Value)]);
    }

    /// <summary>Nota que não está nos botões (ex.: R$ 70 em notas misturadas): digita quanto o cliente entregou.</summary>
    private async Task OutroValorRecebido()
    {
        if (!EscolhendoTroco || _cobrando) return;
        var texto = await Shell.Current.DisplayPromptAsync(TituloTroco, "Quanto o cliente entregou?", "Confirmar", "Cancelar",
            keyboard: Keyboard.Telephone);
        if (string.IsNullOrWhiteSpace(texto)) return;

        if (!Conversoes.TentarLerDinheiro(texto, out var recebido))
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Valor inválido.", "OK");
            return;
        }

        await ReceberDinheiro(recebido);
    }

    private void FecharTroco()
    {
        EscolhendoTroco = false;
        Notas = [];
        _itensDoTroco = [];
    }

    /// <summary>RN-PG-02: metade Pix, metade dinheiro etc. Usa o mesmo diálogo da comanda.</summary>
    private async Task Dividir()
    {
        if (!PodeComecarPagamento()) return;

        _preparandoPagamento = true;
        try
        {
            var conta = await ContaParaCobrarAsync();
            if (conta is null) return;

            var pagamentos = await _pagamento.PerguntarDivididoAsync(conta.Value.Total);
            if (pagamentos is null) return;
            await GravarAsync(conta.Value.Itens, pagamentos);
        }
        finally
        {
            _preparandoPagamento = false;
        }
    }

    /// <summary>
    /// Caixa aberto (RN-CX-03) e total pelo Domain, com a lista de itens que deu esse total: é ela que é gravada, nunca
    /// uma lista mexida depois. Nulo = não dá para cobrar agora (o motivo já apareceu).
    /// </summary>
    private async Task<(decimal Total, List<ItemDoBalcao> Itens)?> ContaParaCobrarAsync()
    {
        if (!await GarantirCaixaAberto()) return null;

        var itens = Itens.Select(l => l.ParaVenda()).ToList();
        decimal total = 0;
        return await Operacao.CarregarAsync(async () => total = await _comandas.TotalDoBalcaoAsync(itens)) ? (total, itens) : null;
    }

    /// <summary>
    /// RN-CM-13 / RN-CM-07: uma única operação abre a comanda, lança os itens e fecha com o pagamento. Se falhar, os itens
    /// continuam na tela. O troco aparece só depois de gravado (B7).
    /// </summary>
    private async Task GravarAsync(IReadOnlyList<ItemDoBalcao> itens, IReadOnlyList<DadosPagamento> pagamentos)
    {
        if (_cobrando || itens.Count == 0) return;
        _cobrando = true;
        try
        {
            ComandaDominio? venda = null;
            if (await Operacao.GravarAsync(async () => venda = await _comandas.VenderNoBalcaoAsync(itens, pagamentos))
                && venda is not null)
            {
                Itens.Clear();
                _versaoTotal++;
                Total = 0m;
                AvisoRapido.Mostrar($"Venda nº {venda.Numero} registrada — R$ {venda.Total:F2}");
                if (venda.TotalTroco > 0)
                {
                    await _pagamento.MostrarTrocoAsync(venda.TotalTroco);
                }
            }
        }
        finally
        {
            _cobrando = false;
        }
    }

    /// <summary>RN-CX-03 / C3: sem caixa aberto não há venda; oferece abrir na aba Caixa. A venda montada fica na tela.</summary>
    private async Task<bool> GarantirCaixaAberto()
    {
        Caixa? caixa = null;
        if (!await Operacao.CarregarAsync(async () => caixa = await _caixa.ObterAbertoLinhaAsync())) return false;
        CaixaFechado = caixa is null;
        if (caixa is not null) return true;

        if (await Shell.Current.DisplayAlertAsync(
                "Caixa fechado",
                "Não há caixa aberto. Abra o caixa (com o fundo de troco) antes de cobrar. A venda montada continua aqui.",
                "Abrir caixa",
                "Agora não"))
        {
            await Shell.Current.GoToAsync("//CaixaView");
        }

        return false;
    }

    private static ModoControle? ModoDoGrupo(IReadOnlyDictionary<Guid, ModoControle> modos, Produto produto) =>
        produto.GrupoEstoqueId is { } grupoId && modos.TryGetValue(grupoId, out var modo) ? modo : null;

    private void AplicarFiltro()
    {
        var filtrados = CategoriaSelecionada == Todos
            ? _todos
            : _todos.Where(p => p.Categoria == CategoriaSelecionada);

        Produtos.Clear();
        foreach (var produto in filtrados) Produtos.Add(produto);
    }
}
