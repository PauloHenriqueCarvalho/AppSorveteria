using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SorveteriaMaui.ViewModel
{
    public class ListarProdutosViewModel : BindableObject
    {
        private readonly DatabaseService _dbService;

        public ListarProdutosViewModel(DatabaseService dbService)
        {
            _dbService = dbService;
            Produtos = new ObservableCollection<Produto>();
            NovoProdutoCommand = new Command(async () => await Shell.Current.GoToAsync("CadastroProdutoView"));
            ItemSelectedCommand = new Command<SelectionChangedEventArgs>(async e => await ItemSelected(e));
        }

        public ObservableCollection<Produto> Produtos { get; }

        public ICommand NovoProdutoCommand { get; }
        public ICommand ItemSelectedCommand { get; }

        public async Task CarregarProdutos()
        {
            Produtos.Clear();
            var lista = await _dbService.GetProdutosAtivos();
            foreach (var p in lista)
                Produtos.Add(p);
        }

        private async Task ItemSelected(SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection != null && e.CurrentSelection.Count > 0)
            {
                if (e.CurrentSelection[0] is Produto p)
                {
                    await Shell.Current.GoToAsync($"CadastroProdutoView?ProdutoId={p.Id}");
                }
            }
        }
    }
}
