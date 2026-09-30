using System.Collections.ObjectModel;
using GestaoSorveteria.Domain.Common;
using System.Windows.Input;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace SorveteriaMaui.ViewModel;

[QueryProperty(nameof(ComandaAtual), "ComandaSelecionada")]
public class DetalhesComandaViewModel : BindableObject
{
    private readonly DatabaseService _dbService;
    private readonly IPaymentService _paymentService;
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
    public ICommand FinalizarComandaCommand { get; }

    public DetalhesComandaViewModel(DatabaseService dbService, IPaymentService paymentService)
    {
        _dbService = dbService;
        _paymentService = paymentService;
        RemoverItemCommand = new Command<ItemComanda>(async (item) => await RemoverItem(item));
        EditarNomeCommand = new Command(async () => await EditarNome());
        AumentarQuantidadeCommand = new Command<ItemComanda>(async (item) => await AumentarQuantidade(item));
        DiminuirQuantidadeCommand = new Command<ItemComanda>(async (item) => await DiminuirQuantidade(item));
        FinalizarComandaCommand = new Command(async () => await FinalizarComanda());
    }

    private async Task DiminuirQuantidade(ItemComanda item)
    {
        if (item.Quantidade > 1)
        {
            item.Quantidade--;
            item.Subtotal = Moeda.Arredondar(item.Quantidade * item.PrecoUnitario);

            await _dbService.SalvarComandaCompleta(ComandaAtual, Itens.ToList());
            await CarregarItens();
        }
        else
        {
            bool confirmar = await Application.Current.MainPage.DisplayAlert(
                "Excluir item",
                $"Remover {item.Descricao} da comanda?",
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
        // RN-CM-05: Total = soma dos subtotais
        ComandaAtual.Total = Itens.Sum(i => i.Subtotal);

        OnPropertyChanged(nameof(ComandaAtual));
    }

    private async Task AumentarQuantidade(ItemComanda item)
    {
        item.Quantidade++;
        item.Subtotal = Moeda.Arredondar(item.Quantidade * item.PrecoUnitario);

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

        var atualizada = await _dbService.GetComandaPorId(ComandaAtual.Id);
        if (atualizada != null) ComandaAtual = atualizada;
    }

    private async Task RemoverItem(ItemComanda item)
    {
        bool confirm = await Application.Current.MainPage.DisplayAlert("Remover",
            $"Deseja remover {item.Descricao}?", "Sim", "Não");

        if (confirm)
        {
            await _dbService.RemoverProdutoDaComanda(item);
            await CarregarItens();
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

    private async Task FinalizarComanda()
    {
        if (ComandaAtual == null) return;

        var sucesso = await _paymentService.ProcessarPagamentoAsync(ComandaAtual);
        if (sucesso)
        {
            var atualizada = await _dbService.GetComandaPorId(ComandaAtual.Id);
            if (atualizada != null) ComandaAtual = atualizada;
        }
    }
}
