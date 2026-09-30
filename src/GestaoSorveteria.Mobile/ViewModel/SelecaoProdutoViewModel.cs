using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;

namespace SorveteriaMaui.ViewModel;

[QueryProperty(nameof(ComandaAtual), "ComandaSelecionada")]
public class SelecaoProdutoViewModel : BindableObject
{
    private readonly ComandaAppService _comandas;
    private readonly ProdutoRepository _produtos;
    private Comanda? _comandaAtual;

    public Comanda? ComandaAtual
    {
        get => _comandaAtual;
        set { _comandaAtual = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Produto> Produtos { get; set; } = new();

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

    // Comandos chamados pela Interface (XAML)
    public ICommand SelecionarProdutoCommand { get; }
    public ICommand PrecoManualCommand { get; }
    public ICommand AumentarQtdeCommand { get; }
    public ICommand DiminuirQtdeCommand { get; }

    public SelecaoProdutoViewModel(ComandaAppService comandas, ProdutoRepository produtos)
    {
        _comandas = comandas;
        _produtos = produtos;

        SelecionarProdutoCommand = new Command<Produto>(async (p) => await AdicionarItem(p));
        PrecoManualCommand = new Command(async () => await AdicionarPrecoManual());

        AumentarQtdeCommand = new Command(() => Quantidade++);
        DiminuirQtdeCommand = new Command(() => { if (Quantidade > 1) Quantidade--; });
        BuscarCommand = new Command(AplicarFiltro);

        _ = CarregarProdutos();
    }

    async Task CarregarProdutos()
    {
        await Operacao.CarregarAsync(async () =>
        {
            await _produtos.PopularSeVazioAsync();
            var lista = await _produtos.ListarAtivosAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _todos = lista;
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
            await Shell.Current.DisplayAlertAsync("Sucesso", $"Valor de R$ {valor:F2} adicionado!", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    async Task AdicionarItem(Produto produto)
    {
        if (ComandaAtual == null) return;

        var quantidade = Quantidade;
        if (await Executar(() => _comandas.AdicionarProdutoAsync(ComandaAtual.Id, produto.Id, quantidade)))
        {
            await Shell.Current.DisplayAlertAsync("Sucesso", $"{quantidade}x {produto.Nome} adicionado!", "OK");

            // Reseta a quantidade para 1 para o próximo clique
            Quantidade = 1;
        }
    }

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
