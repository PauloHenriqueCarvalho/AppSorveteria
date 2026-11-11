using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;

namespace SorveteriaMaui.ViewModel;

/// <summary>Botão da grade de sabores do pote (RN-ES-09), na ordem da planilha.</summary>
public sealed record SaborParaEscolher(Guid Id, string Nome);

[QueryProperty(nameof(ComandaAtual), "ComandaSelecionada")]
public class SelecaoProdutoViewModel : BindableObject
{
    private readonly ComandaAppService _comandas;
    private readonly ProdutoRepository _produtos;
    private readonly ItemEstoqueRepository _itensEstoque;
    private Comanda? _comandaAtual;

    public Comanda? ComandaAtual
    {
        get => _comandaAtual;
        set { _comandaAtual = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Produto> Produtos { get; set; } = new();

    private bool _semProdutos;
    /// <summary>C2: o catálogo ainda não foi baixado neste aparelho (aba Produtos → Atualizar).</summary>
    public bool SemProdutos
    {
        get => _semProdutos;
        set { _semProdutos = value; OnPropertyChanged(); }
    }

    // B12: busca por nome e filtro por categoria sobre a lista carregada
    public const string TodasCategorias = "Todas";
    private List<Produto> _todos = new();

    public ObservableCollection<string> Categorias { get; } = new() { TodasCategorias };

    private string _filtroNome = string.Empty;
    public string FiltroNome
    {
        get => _filtroNome;
        set
        {
            if (_filtroNome == value) return;
            _filtroNome = value ?? string.Empty;
            OnPropertyChanged();
            AplicarFiltro(); // filtra enquanto digita
        }
    }

    private string _categoriaSelecionada = TodasCategorias;
    public string CategoriaSelecionada
    {
        get => _categoriaSelecionada;
        set
        {
            var nova = string.IsNullOrEmpty(value) ? TodasCategorias : value;
            if (_categoriaSelecionada == nova) return;
            _categoriaSelecionada = nova;
            OnPropertyChanged();
            AplicarFiltro();
        }
    }

    public ICommand BuscarCommand { get; }

    // Propriedade de Quantidade controlada pela tela
    private int _quantidade = 1;
    public int Quantidade
    {
        get => _quantidade;
        set
        {
            if (_quantidade != value)
            {
                _quantidade = value;
                OnPropertyChanged();
            }
        }
    }

    // ---------------------------------------------------------------- RN-ES-09: grade de sabores, só nos potes

    /// <summary>O pote tocado, à espera do sabor. Nulo = grade fechada (ou o sabor já foi tocado e está sendo lançado).</summary>
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

    /// <summary>1 toque no sabor lança o pote com aquele sabor (na quantidade escolhida embaixo).</summary>
    public ICommand EscolherSaborCommand { get; }

    /// <summary>"Voltar" (ou o botão voltar do aparelho): fecha a grade sem lançar nada.</summary>
    public ICommand CancelarSaborCommand { get; }

    /// <summary>RN-ES-09: "Sem sabor" lança o pote sem sabor (com fila no balcão); a contagem semanal corrige o estoque.</summary>
    public ICommand SemSaborCommand { get; }

    // Comandos chamados pela Interface (XAML)
    public ICommand SelecionarProdutoCommand { get; }
    public ICommand PrecoManualCommand { get; }
    public ICommand AumentarQtdeCommand { get; }
    public ICommand DiminuirQtdeCommand { get; }
    public ICommand DigitarQtdeCommand { get; }

    public SelecaoProdutoViewModel(ComandaAppService comandas, ProdutoRepository produtos, ItemEstoqueRepository itensEstoque)
    {
        _comandas = comandas;
        _produtos = produtos;
        _itensEstoque = itensEstoque;

        SelecionarProdutoCommand = new Command<Produto>(async (p) => await AdicionarItem(p));
        PrecoManualCommand = new Command(async () => await AdicionarPrecoManual());
        EscolherSaborCommand = new Command<SaborParaEscolher>(async (s) => await EscolherSabor(s));
        CancelarSaborCommand = new Command(FecharGradeDeSabores);
        SemSaborCommand = new Command(async () => await SemSabor());

        AumentarQtdeCommand = new Command(() => Quantidade++);
        DiminuirQtdeCommand = new Command(() => { if (Quantidade > 1) Quantidade--; });
        DigitarQtdeCommand = new Command(async () =>
        {
            if (await PedirQuantidade.PerguntarAsync("Quantidade a lançar", Quantidade) is { } digitada)
            {
                Quantidade = digitada; // RN-ES-04
            }
        });
        BuscarCommand = new Command(AplicarFiltro);

        _ = CarregarProdutos();
    }

    async Task CarregarProdutos()
    {
        await Operacao.CarregarAsync(async () =>
        {
            var lista = await _produtos.ListarAtivosAsync(); // cache do catálogo da API (C2)

            // RN-ES-09: marca os potes ("escolhe o sabor") pelo modo do grupo que o tablet conhece
            var modos = await _itensEstoque.ModosDosGruposAsync();
            foreach (var produto in lista)
            {
                produto.PedeSaborNaVenda = produto.PedeSaborNoModo(ModoDoGrupo(modos, produto));
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                _todos = lista;
                SemProdutos = lista.Count == 0;
                var categoriaAtual = CategoriaSelecionada; // Clear() zera a seleção da tela
                Categorias.Clear();
                Categorias.Add(TodasCategorias);
                foreach (var categoria in lista.Select(p => p.Categoria).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().Order())
                {
                    Categorias.Add(categoria);
                }

                _categoriaSelecionada = Categorias.Contains(categoriaAtual) ? categoriaAtual : TodasCategorias;
                OnPropertyChanged(nameof(CategoriaSelecionada));
                AplicarFiltro();
            });
        });
    }

    async Task AdicionarPrecoManual()
    {
        if (ComandaAtual == null) return;

        string resultado = await Shell.Current.DisplayPromptAsync(
            "Self-Service",
            "Digite o valor (ex: 15,50):",
            "Adicionar",
            "Cancelar",
            keyboard: Keyboard.Telephone);

        if (string.IsNullOrWhiteSpace(resultado)) return;

        if (!Conversoes.TentarLerDinheiro(resultado, out decimal valor) || valor <= 0) // RN-CM-03: item livre > 0
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Formato de preço inválido!", "Tentar novamente");
            return;
        }

        // B9: self-service é item livre (ProdutoId nulo), não um produto inventado
        if (await Executar(() => _comandas.AdicionarItemLivreAsync(ComandaAtual.Id, "Self-Service", valor)))
        {
            AvisoRapido.Mostrar($"Self-Service R$ {valor:F2} adicionado"); // B11: sem toque extra
            await Shell.Current.GoToAsync("..");
        }
    }

    async Task AdicionarItem(Produto produto)
    {
        if (ComandaAtual == null || EscolhendoSabor) return;

        // RN-ES-09: pote (grupo com saldo pela venda) abre a grade de sabores; picolé e massa entram direto, como sempre
        SaboresDoProduto? sabores = null;
        if (!await Operacao.CarregarAsync(async () => sabores = await _comandas.SaboresAsync(produto.Id)) || sabores is null)
        {
            return;
        }

        if (sabores.PedeSabor && sabores.Sabores.Count > 0)
        {
            AbrirGradeDeSabores(produto, sabores);
            return;
        }

        // Pote sem sabores no tablet (a lista do estoque ainda não foi baixada): vende sem sabor e avisa sem travar
        var aviso = sabores.SemSaboresNoTablet ? " sem sabor (os sabores ainda não chegaram ao tablet)" : string.Empty;
        await Lancar(produto, saborId: null, descricao: produto.Nome, complemento: aviso);
    }

    private void AbrirGradeDeSabores(Produto produto, SaboresDoProduto sabores)
    {
        _produtoDoSabor = produto;
        TituloSabor = produto.Nome;
        Sabores = sabores.Sabores.Select(s => new SaborParaEscolher(s.Id, s.Nome)).ToList();
        EscolhendoSabor = true;
    }

    private void FecharGradeDeSabores()
    {
        _produtoDoSabor = null;
        EscolhendoSabor = false;
        Sabores = [];
    }

    async Task EscolherSabor(SaborParaEscolher? sabor)
    {
        // Toque duplo no sabor: o segundo encontra a grade "ocupada" e não lança outra vez
        var produto = _produtoDoSabor;
        if (ComandaAtual == null || produto is null || sabor is null) return;
        _produtoDoSabor = null;

        try
        {
            await Lancar(produto, sabor.Id, $"{produto.Nome} · {sabor.Nome}", complemento: string.Empty);
        }
        finally
        {
            FecharGradeDeSabores(); // deu certo ou não (o alerta já apareceu): volta para a lista de produtos
        }
    }

    async Task SemSabor()
    {
        var produto = _produtoDoSabor;
        if (ComandaAtual == null || produto is null) return;
        _produtoDoSabor = null;

        try
        {
            await Lancar(produto, saborId: null, descricao: produto.Nome, complemento: " sem sabor");
        }
        finally
        {
            FecharGradeDeSabores();
        }
    }

    /// <summary>Lança o produto (com o sabor, nos potes) e confirma com toast, sem toque extra (B11).</summary>
    private async Task Lancar(Produto produto, Guid? saborId, string descricao, string complemento)
    {
        if (ComandaAtual == null) return;

        var quantidade = Quantidade;
        var comandaId = ComandaAtual.Id;
        if (await Executar(() => _comandas.AdicionarProdutoAsync(comandaId, produto.Id, quantidade, saborId)))
        {
            AvisoRapido.Mostrar($"{quantidade}x {descricao} adicionado{complemento}"); // B11: sem toque extra

            // Reseta a quantidade para 1 para o próximo clique
            Quantidade = 1;
        }
    }

    private static ModoControle? ModoDoGrupo(IReadOnlyDictionary<Guid, ModoControle> modos, Produto produto) =>
        produto.GrupoEstoqueId is { } grupoId && modos.TryGetValue(grupoId, out var modo) ? modo : null;

    private void AplicarFiltro()
    {
        var termo = SemAcento(FiltroNome.Trim());
        var filtrados = _todos.Where(p =>
            (CategoriaSelecionada == TodasCategorias || p.Categoria == CategoriaSelecionada)
            && (termo.Length == 0 || SemAcento(p.Nome).Contains(termo, StringComparison.OrdinalIgnoreCase)));

        Produtos.Clear();
        foreach (var produto in filtrados) Produtos.Add(produto);
    }

    /// <summary>"picole" encontra "Picolé", "acai" encontra "Açaí".</summary>
    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(c);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>B7: falha aparece para o atendente e o fluxo para (sem "Sucesso").</summary>
    private static Task<bool> Executar(Func<Task> acao) => Operacao.GravarAsync(acao);
}
