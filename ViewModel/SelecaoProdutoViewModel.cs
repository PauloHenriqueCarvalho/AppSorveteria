using SorveteriaMaui.Model;
using SuaSorveteria.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SorveteriaMaui.ViewModel
{
    [QueryProperty(nameof(ComandaAtual), "ComandaSelecionada")]
    public class SelecaoProdutoViewModel : BindableObject
    {
        private readonly DatabaseService _dbService;
        private Comanda _comandaAtual;

        public Comanda ComandaAtual
        {
            get => _comandaAtual;
            set { _comandaAtual = value; OnPropertyChanged(); }
        }

        public ObservableCollection<Produto> Produtos { get; set; } = new();

        // Propriedade de Quantidade controlada pela tela
        private double _quantidade = 1;
        public double Quantidade
        {
            get => _quantidade;
            set
            {
                if (_quantidade != value)
                {
                    _quantidade = value;
                    OnPropertyChanged(); // ISSO AQUI FAZ O NÚMERO APARECER NA TELA
                }
            }
        }

        // Comandos chamados pela Interface (XAML)
        public ICommand SelecionarProdutoCommand { get; }
        public ICommand PrecoManualCommand { get; }
        public ICommand AumentarQtdeCommand { get; }
        public ICommand DiminuirQtdeCommand { get; }

        public SelecaoProdutoViewModel(DatabaseService dbService)
        {
            _dbService = dbService;

            // Inicializando os Comandos
            SelecionarProdutoCommand = new Command<Produto>(async (p) => await AdicionarItem(p));
            PrecoManualCommand = new Command(async () => await AdicionarPrecoManual());

            AumentarQtdeCommand = new Command(() => Quantidade++);
            DiminuirQtdeCommand = new Command(() => { if (Quantidade > 1) Quantidade--; });

            _ = CarregarProdutos();
        }

        async Task CarregarProdutos()
        {
            await _dbService.PopularBancoSeVazio();
            var lista = await _dbService.GetProdutosAtivos();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Produtos.Clear();
                foreach (var item in lista) Produtos.Add(item);
            });
        }

        async Task AdicionarPrecoManual()
        {
            string resultado = await Application.Current.MainPage.DisplayPromptAsync(
          "Self-Service",
          "Digite o valor (ex: 15,50):",
          "Adicionar",
          "Cancelar",
          keyboard: Keyboard.Telephone);

            if (string.IsNullOrWhiteSpace(resultado)) return;

            // TRATAMENTO DE STRING:
            // 1. Troca vírgula por ponto para o C# entender
            string valorTratado = resultado.Replace(",", ".");

            // 2. Tenta converter usando a cultura invariante (ponto como decimal)
            if (double.TryParse(valorTratado, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valor))
            {
                var prodManual = new Produto
                {
                    Id = "MANUAL_" + Guid.NewGuid().ToString(),
                    Nome = "Self-Service",
                    Preco = valor,
                    Ativo = 1
                };

                await _dbService.AdicionarProdutoNaComanda(ComandaAtual.Id, prodManual, 1);

                await Application.Current.MainPage.DisplayAlert("Sucesso", $"Valor de R$ {valor:F2} adicionado!", "OK");

                // Opcional: Voltar para a lista ou ficar na tela
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Application.Current.MainPage.DisplayAlert("Erro", "Formato de preço inválido!", "Tentar novamente");
            }
        }

        async Task AdicionarItem(Produto produto)
        {
            // Usa a variável Quantidade ao invés de fixar 1
            await _dbService.AdicionarProdutoNaComanda(ComandaAtual.Id, produto, Quantidade);

            // Mostra um aviso rápido tipo "Toast" para não atrapalhar o fluxo
            await Application.Current.MainPage.DisplayAlert("Sucesso", $"{Quantidade}x {produto.Nome} adicionado!", "OK");

            // Reseta a quantidade para 1 para o próximo clique
            Quantidade = 1;

            // REMOVI o GoToAsync("..") daqui para que o atendente possa continuar lançando outros itens!
        }
    }
}