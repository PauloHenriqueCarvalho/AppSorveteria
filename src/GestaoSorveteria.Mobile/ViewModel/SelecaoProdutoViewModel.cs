using System.Collections.ObjectModel;
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
                Produtos.Clear();
                foreach (var item in lista) Produtos.Add(item);
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
        if (ComandaAtual == null) return;

        var quantidade = Quantidade;
        if (await Executar(() => _comandas.AdicionarProdutoAsync(ComandaAtual.Id, produto.Id, quantidade)))
        {
            AvisoRapido.Mostrar($"{quantidade}x {produto.Nome} adicionado"); // B11: sem toque extra

            // Reseta a quantidade para 1 para o próximo clique
            Quantidade = 1;
        }
    }

    /// <summary>B7: falha aparece para o atendente e o fluxo para (sem "Sucesso").</summary>
    private static Task<bool> Executar(Func<Task> acao) => Operacao.GravarAsync(acao);
}
