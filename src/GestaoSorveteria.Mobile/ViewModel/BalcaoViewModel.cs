using System.Collections.ObjectModel;
using System.Windows.Input;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using ComandaDominio = GestaoSorveteria.Domain.Comandas.Comanda;

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
/// RN-CM-13: tela do balcão, para o cliente que pega, paga e vai embora. Botões grandes com os produtos (os mais vendidos
/// do aparelho primeiro), toque soma na venda, "Cobrar" pergunta o pagamento e grava a comanda já paga numa operação só.
/// Pote: 1 toque no sabor ou em "Sem sabor" (RN-ES-09). A tela fica aberta para o próximo cliente.
/// </summary>
public class BalcaoViewModel : BindableObject
{
    public const string MaisVendidos = "Mais vendidos";

    private readonly ComandaAppService _comandas;
    private readonly ProdutoRepository _produtos;
    private readonly ComandaRepository _repositorio;
    private readonly ItemEstoqueRepository _itensEstoque;
    private readonly CaixaAppService _caixa;
    private readonly IPaymentService _pagamento;

    private List<Produto> _todos = new();
    private int _versaoTotal;
    private bool _cobrando;

    public ObservableCollection<Produto> Produtos { get; } = new();
    public ObservableCollection<string> Categorias { get; } = new() { MaisVendidos };
    public ObservableCollection<LinhaDoBalcao> Itens { get; } = new();

    private bool _semProdutos;
    public bool SemProdutos
    {
        get => _semProdutos;
        set { _semProdutos = value; OnPropertyChanged(); }
    }

    private string _categoriaSelecionada = MaisVendidos;
    public string CategoriaSelecionada
    {
        get => _categoriaSelecionada;
        set
        {
            var nova = string.IsNullOrEmpty(value) ? MaisVendidos : value;
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
            OnPropertyChanged(nameof(TextoCobrar));
        }
    }

    public bool TemItens => Itens.Count > 0;
    public bool SemItens => Itens.Count == 0;
    public string TextoCobrar => TemItens ? $"Cobrar  R$ {Total:F2}" : "Cobrar";

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

    public ICommand TocarProdutoCommand { get; }
    public ICommand EscolherSaborCommand { get; }
    public ICommand SemSaborCommand { get; }
    public ICommand CancelarSaborCommand { get; }
    public ICommand ValorDigitadoCommand { get; }
    public ICommand MaisUmCommand { get; }
    public ICommand MenosUmCommand { get; }
    public ICommand DigitarQuantidadeCommand { get; }
    public ICommand LimparCommand { get; }
    public ICommand CobrarCommand { get; }

    public BalcaoViewModel(ComandaAppService comandas, ProdutoRepository produtos, ComandaRepository repositorio,
        ItemEstoqueRepository itensEstoque, CaixaAppService caixa, IPaymentService pagamento)
    {
        _comandas = comandas;
        _produtos = produtos;
        _repositorio = repositorio;
        _itensEstoque = itensEstoque;
        _caixa = caixa;
        _pagamento = pagamento;

        Itens.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TemItens));
            OnPropertyChanged(nameof(SemItens));
            OnPropertyChanged(nameof(TextoCobrar));
        };

        TocarProdutoCommand = new Command<Produto>(async p => await TocarProduto(p));
        EscolherSaborCommand = new Command<SaborParaEscolher>(EscolherSabor);
        SemSaborCommand = new Command(SemSabor);
        CancelarSaborCommand = new Command(FecharGradeDeSabores);
        ValorDigitadoCommand = new Command(async () => await ValorDigitado());
        MaisUmCommand = new Command<LinhaDoBalcao>(l => { if (l is not null) AlterarQuantidade(l, l.Quantidade + 1); });
        MenosUmCommand = new Command<LinhaDoBalcao>(l => { if (l is not null) AlterarQuantidade(l, l.Quantidade - 1); });
        DigitarQuantidadeCommand = new Command<LinhaDoBalcao>(async l => await DigitarQuantidade(l));
        LimparCommand = new Command(async () => await Limpar());
        CobrarCommand = new Command(async () => await Cobrar());
    }

    /// <summary>Ao abrir a tela: confere o caixa (RN-CX-03) e carrega os botões, os mais vendidos primeiro.</summary>
    public async Task AoAparecerAsync()
    {
        if (!await GarantirCaixaAberto())
        {
            return;
        }

        await Operacao.CarregarAsync(async () =>
        {
            var lista = await _produtos.ListarAtivosAsync(); // cache do catálogo da API (C2), na ordem dos botões
            var vendidos = await _repositorio.QuantidadeVendidaPorProdutoAsync();
            var modos = await _itensEstoque.ModosDosGruposAsync();
            foreach (var produto in lista)
            {
                produto.PedeSaborNaVenda = produto.PedeSaborNoModo(ModoDoGrupo(modos, produto));
            }

            // Mais vendidos primeiro; empate (e produto que ainda não vendeu) na ordem do cadastro
            var ordenada = lista
                .Select((p, posicao) => (Produto: p, Posicao: posicao, Vendidos: vendidos.GetValueOrDefault(p.Id)))
                .OrderByDescending(x => x.Vendidos)
                .ThenBy(x => x.Posicao)
                .Select(x => x.Produto)
                .ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _todos = ordenada;
                SemProdutos = ordenada.Count == 0;
                var categoriaAtual = CategoriaSelecionada;
                Categorias.Clear();
                Categorias.Add(MaisVendidos);
                foreach (var categoria in ordenada.Select(p => p.Categoria).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Order())
                {
                    Categorias.Add(categoria);
                }

                _categoriaSelecionada = Categorias.Contains(categoriaAtual) ? categoriaAtual : MaisVendidos;
                OnPropertyChanged(nameof(CategoriaSelecionada));
                AplicarFiltro();
            });
        });
    }

    private async Task TocarProduto(Produto? produto)
    {
        if (produto is null || EscolhendoSabor || _cobrando) return;

        if (produto.PermiteValorLivre)
        {
            // RN-PR-02: produto de valor livre (ex.: self-service por peso) pede o valor
            if (await PerguntarValorAsync(produto.Nome) is { } valor)
            {
                Itens.Add(new LinhaDoBalcao(produto.Id, null, produto.Nome, valor, valorLivre: true));
                AtualizarTotal();
            }

            return;
        }

        SaboresDoProduto? sabores = null;
        if (!await Operacao.CarregarAsync(async () => sabores = await _comandas.SaboresAsync(produto.Id)) || sabores is null)
        {
            return;
        }

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

    /// <summary>RN-CM-03: valor digitado (venda avulsa) entra como item livre na mesma venda.</summary>
    private async Task ValorDigitado()
    {
        if (_cobrando) return;
        if (await PerguntarValorAsync("Venda avulsa") is { } valor)
        {
            Itens.Add(new LinhaDoBalcao(null, null, ComandaDominio.DescricaoVendaAvulsa, valor, valorLivre: true));
            AtualizarTotal();
        }
    }

    private static async Task<decimal?> PerguntarValorAsync(string titulo)
    {
        var texto = await Shell.Current.DisplayPromptAsync(titulo, "Valor (ex.: 15,50):", "Adicionar", "Cancelar",
            keyboard: Keyboard.Telephone);
        if (string.IsNullOrWhiteSpace(texto)) return null;

        if (!Conversoes.TentarLerDinheiro(texto, out var valor) || valor <= 0) // RN-CM-03: valor > 0
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Valor inválido.", "OK");
            return null;
        }

        return valor;
    }

    private void AlterarQuantidade(LinhaDoBalcao? linha, int quantidade)
    {
        if (linha is null || _cobrando) return;
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
        if (linha is null || _cobrando) return;
        if (await PedirQuantidade.PerguntarAsync(linha.Descricao, linha.Quantidade) is { } quantidade)
        {
            AlterarQuantidade(linha, quantidade); // RN-ES-04
        }
    }

    private async Task Limpar()
    {
        if (!TemItens || _cobrando) return;
        if (await Shell.Current.DisplayAlertAsync("Limpar", "Tirar todos os itens desta venda? Nada foi cobrado.", "Limpar", "Voltar"))
        {
            Itens.Clear();
            AtualizarTotal();
        }
    }

    /// <summary>
    /// Total na tela pelo Domain, a cada toque. Toques rápidos: só vale a conta mais nova. Se a conta falhar (ex.: caixa
    /// fechado no meio), mostra a soma das linhas; o "Cobrar" refaz a conta pelo Domain antes de pedir o pagamento.
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
        catch (Exception)
        {
            total = Moeda.Arredondar(Itens.Sum(l => l.Subtotal));
        }

        if (versao == _versaoTotal)
        {
            Total = total;
        }
    }

    /// <summary>
    /// RN-CM-13 / RN-CM-07: total pelo Domain → forma de pagamento (e troco, em dinheiro) → grava a comanda já paga.
    /// Cancelar o pagamento não grava nada e mantém os itens na tela. O troco aparece só depois de gravado (B7).
    /// </summary>
    private async Task Cobrar()
    {
        if (_cobrando || !TemItens || EscolhendoSabor) return;
        _cobrando = true;
        try
        {
            if (!await GarantirCaixaAberto()) return;

            var itens = Itens.Select(l => l.ParaVenda()).ToList();
            decimal total = 0;
            if (!await Operacao.CarregarAsync(async () => total = await _comandas.TotalDoBalcaoAsync(itens))) return;

            var pagamentos = await _pagamento.PerguntarPagamentosAsync(total);
            if (pagamentos is null) return;

            ComandaDominio? venda = null;
            if (await Operacao.GravarAsync(async () => venda = await _comandas.VenderNoBalcaoAsync(itens, pagamentos))
                && venda is not null)
            {
                Itens.Clear();
                Total = 0m;
                _versaoTotal++;
                AvisoRapido.Mostrar($"Venda nº {venda.Numero} registrada — R$ {venda.Total:F2}");
                if (venda.TotalTroco > 0)
                {
                    await _pagamento.MostrarTrocoAsync(venda.TotalTroco);
                }

                _ = AoAparecerAsync(); // reordena os mais vendidos para o próximo cliente
            }
        }
        finally
        {
            _cobrando = false;
        }
    }

    /// <summary>RN-CX-03 / C3: sem caixa aberto não há venda; leva o atendente para a aba Caixa.</summary>
    private async Task<bool> GarantirCaixaAberto()
    {
        Caixa? caixa = null;
        if (!await Operacao.CarregarAsync(async () => caixa = await _caixa.ObterAbertoLinhaAsync())) return false;
        if (caixa is not null) return true;

        bool abrir = await Shell.Current.DisplayAlertAsync(
            "Caixa fechado",
            "Não há caixa aberto. Abra o caixa (com o fundo de troco) antes de vender.",
            "Abrir caixa",
            "Agora não");
        await Shell.Current.GoToAsync(abrir ? "//CaixaView" : "..");
        return false;
    }

    private static ModoControle? ModoDoGrupo(IReadOnlyDictionary<Guid, ModoControle> modos, Produto produto) =>
        produto.GrupoEstoqueId is { } grupoId && modos.TryGetValue(grupoId, out var modo) ? modo : null;

    private void AplicarFiltro()
    {
        var filtrados = CategoriaSelecionada == MaisVendidos
            ? _todos
            : _todos.Where(p => p.Categoria == CategoriaSelecionada);

        Produtos.Clear();
        foreach (var produto in filtrados) Produtos.Add(produto);
    }
}
