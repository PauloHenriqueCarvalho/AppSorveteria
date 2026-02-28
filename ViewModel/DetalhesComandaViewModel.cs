using System.Collections.ObjectModel;
using System.Windows.Input;
using SorveteriaMaui.Model;
using SuaSorveteria.Services;

namespace SorveteriaMaui.ViewModel;

[QueryProperty(nameof(ComandaAtual), "ComandaSelecionada")] // ...tem que ser igual a este nome aqui!
public class DetalhesComandaViewModel : BindableObject 
{
    private readonly DatabaseService _dbService;
    private Comanda _comandaAtual;
     
    public Comanda ComandaAtual
    {
        get => _comandaAtual;
        set
        {
            _comandaAtual = value;
            OnPropertyChanged();
        }
    }
    public ICommand AumentarQuantidadeCommand { get; }
    public ICommand DiminuirQuantidadeCommand { get; }

    public ObservableCollection<ItemComanda> Itens { get; set; } = new();

    public ICommand RemoverItemCommand { get; }
    public ICommand EditarNomeCommand { get; }

    public DetalhesComandaViewModel(DatabaseService dbService)
    {
        _dbService = dbService;
        RemoverItemCommand = new Command<ItemComanda>(async (item) => await RemoverItem(item));
        EditarNomeCommand = new Command(async () => await EditarNome());
        AumentarQuantidadeCommand = new Command<ItemComanda>(async (item) => await AumentarQuantidade(item));
        DiminuirQuantidadeCommand = new Command<ItemComanda>(async (item) => await DiminuirQuantidade(item));
    }
    private async Task DiminuirQuantidade(ItemComanda item)
    {
        if (item.Quantidade > 1)
        {
            item.Quantidade--;
            item.Total = item.Quantidade * item.PrecoUnitario;

            await _dbService.SalvarComandaCompleta(ComandaAtual, Itens.ToList());
            await CarregarItens();
        }
        else
        {
            bool confirmar = await Application.Current.MainPage.DisplayAlert(
                "Excluir item",
                $"Remover {item.ProdutoNome} da comanda?",
                "Remover",
                "Cancelar");

            if (confirmar)
            {
                await _dbService.RemoverProdutoDaComanda(item);
                await CarregarItens();
            }
        }
        RecalcularTotal();

    }
    private void RecalcularTotal()
    {
        ComandaAtual.Subtotal = Itens.Sum(i => i.Total);
        ComandaAtual.Total = (ComandaAtual.Subtotal + ComandaAtual.AcrescimoManual)
                             - ComandaAtual.DescontoManual;

        OnPropertyChanged(nameof(ComandaAtual));
    }
    private async Task AumentarQuantidade(ItemComanda item)
    {
        item.Quantidade++;
        item.Total = item.Quantidade * item.PrecoUnitario;

        await _dbService.SalvarComandaCompleta(ComandaAtual, Itens.ToList());
        await CarregarItens();
        RecalcularTotal();

    }
    public async Task CarregarItens()
    {
        if (ComandaAtual == null) return;

        var lista = await _dbService.GetItensDaComanda(ComandaAtual.Id);

        MainThread.BeginInvokeOnMainThread(() => {
            Itens.Clear();
            foreach (var i in lista) Itens.Add(i);
        });

        // Atualiza os dados da comanda (como o Total) caso tenha mudado
        var atualizada = await _dbService.GetComandaPorId(ComandaAtual.Id);
        if (atualizada != null) ComandaAtual = atualizada;
    }

    private async Task RemoverItem(ItemComanda item)
    {
        bool confirm = await Application.Current.MainPage.DisplayAlert("Remover",
            $"Deseja remover {item.ProdutoNome}?", "Sim", "Não");

        if (confirm)
        {
            await _dbService.RemoverProdutoDaComanda(item);
            await CarregarItens(); // Recarrega a lista e o total
        }
    }

    private async Task EditarNome()
    {
        string novoNome = await Application.Current.MainPage.DisplayPromptAsync("Editar",
            "Nome do Cliente:", "Salvar", "Cancelar", initialValue: ComandaAtual.NomeCliente);

        if (novoNome != null)
        {
            await _dbService.AtualizarNomeComanda(ComandaAtual.Id, novoNome);
            ComandaAtual.NomeCliente = novoNome;
            OnPropertyChanged(nameof(ComandaAtual));
        }
    }
}