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
            Categorias = new ObservableCollection<string>(Produto.Categorias);
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

        // B13: categoria pelo nome, não pelo índice
        private string _categoria = Produto.Categorias[0];
        public string Categoria { get => _categoria; set { _categoria = value; OnPropertyChanged(); } }

        private Produto? _existente;

        public async Task LoadIfNeeded()
        {
            if (string.IsNullOrEmpty(ProdutoId)) return;

            var lista = await _dbService.GetProdutosAtivos();
            var p = Guid.TryParse(ProdutoId, out var id) ? lista.FirstOrDefault(x => x.Id == id) : null;
            if (p != null)
            {
                _existente = p;
                Nome = p.Nome;
                PrecoString = p.Preco.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                if (!Categorias.Contains(p.Categoria)) Categorias.Add(p.Categoria);
                Categoria = p.Categoria;
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

            if (!Conversoes.TentarLerDinheiro(string.IsNullOrWhiteSpace(PrecoString) ? "0" : PrecoString, out decimal preco))
            {
                await Application.Current.MainPage.DisplayAlert("Erro", "Preço inválido", "OK");
                return;
            }

            var produto = _existente ?? new Produto { CriadoEm = DateTime.UtcNow };
            produto.Nome = Nome;
            produto.Preco = preco;
            produto.Categoria = Categoria;
            if (_existente != null) produto.AtualizadoEm = DateTime.UtcNow;

            await _dbService.SalvarProduto(produto);
            await Shell.Current.GoToAsync("..");
        }
    }
}
