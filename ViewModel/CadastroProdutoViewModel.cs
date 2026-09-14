using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SorveteriaMaui.ViewModel
{
    [QueryProperty(nameof(ProdutoId), "ProdutoId")]
    public class CadastroProdutoViewModel : BindableObject
    {
        private readonly DatabaseService _dbService;

        public CadastroProdutoViewModel(DatabaseService dbService)
        {
            _dbService = dbService;
            Categorias = new ObservableCollection<string> { "Picolé", "Pote", "Bebida", "Acompanhamento", "Self-Service" };
            SalvarCommand = new Command(async () => await Salvar());
        }

        public ObservableCollection<string> Categorias { get; }

        public ICommand SalvarCommand { get; }

        private string _produtoId;
        public string ProdutoId
        {
            get => _produtoId;
            set { _produtoId = value; OnPropertyChanged(); }
        }

        private string _nome;
        public string Nome { get => _nome; set { _nome = value; OnPropertyChanged(); } }

        private string _precoString;
        public string PrecoString { get => _precoString; set { _precoString = value; OnPropertyChanged(); } }

        private int _categoriaIndex = 0;
        public int CategoriaIndex { get => _categoriaIndex; set { _categoriaIndex = value; OnPropertyChanged(); } }

        public async Task LoadIfNeeded()
        {
            if (string.IsNullOrEmpty(ProdutoId)) return;

            var lista = await _dbService.GetProdutosAtivos();
            var p = lista.FirstOrDefault(x => x.Id == ProdutoId);
            if (p != null)
            {
                Nome = p.Nome;
                PrecoString = p.Preco.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                CategoriaIndex = p.Tipo; // assume mapeamento por índice
            }
        }

        private async Task Salvar()
        {
            // validações simples
            if (string.IsNullOrWhiteSpace(Nome))
            {
                await Application.Current.MainPage.DisplayAlert("Erro", "Nome obrigatório", "OK");
                return;
            }

            if (!double.TryParse((PrecoString ?? "0").Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double preco))
            {
                await Application.Current.MainPage.DisplayAlert("Erro", "Preço inválido", "OK");
                return;
            }

            var produto = new Produto
            {
                Id = string.IsNullOrEmpty(ProdutoId) ? Guid.NewGuid().ToString() : ProdutoId,
                Nome = Nome,
                Preco = preco,
                Tipo = CategoriaIndex,
                Ativo = 1,
                DataCriacao = DateTime.Now
            };

            await _dbService.SalvarProduto(produto);
            await Shell.Current.GoToAsync("..");
        }
    }
}
