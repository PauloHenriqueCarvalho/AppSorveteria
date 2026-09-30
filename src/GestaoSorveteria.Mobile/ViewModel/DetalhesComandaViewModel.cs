using System.Collections.ObjectModel;
using System.Windows.Input;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;

namespace SorveteriaMaui.ViewModel;

[QueryProperty(nameof(ComandaAtual), "ComandaSelecionada")]
public class DetalhesComandaViewModel : BindableObject
{
    private readonly ComandaAppService _comandas;
    private readonly ComandaRepository _repositorio;
    private readonly IPaymentService _paymentService;
    private Comanda? _comandaAtual;

    public Comanda? ComandaAtual
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
    public ICommand EditarObservacaoCommand { get; }
    public ICommand FinalizarComandaCommand { get; }

    public DetalhesComandaViewModel(ComandaAppService comandas, ComandaRepository repositorio, IPaymentService paymentService)
    {
        _comandas = comandas;
        _repositorio = repositorio;
        _paymentService = paymentService;
        RemoverItemCommand = new Command<ItemComanda>(async (item) => await RemoverItem(item));
        EditarNomeCommand = new Command(async () => await EditarNome());
        EditarObservacaoCommand = new Command(async () => await EditarObservacao());
        AumentarQuantidadeCommand = new Command<ItemComanda>(async (item) => await AlterarQuantidade(item, item.Quantidade + 1));
        DiminuirQuantidadeCommand = new Command<ItemComanda>(async (item) => await DiminuirQuantidade(item));
        FinalizarComandaCommand = new Command(async () => await FinalizarComanda());
    }

    private async Task DiminuirQuantidade(ItemComanda item)
    {
        if (item.Quantidade > 1)
        {
            await AlterarQuantidade(item, item.Quantidade - 1);
            return;
        }

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Excluir item",
            $"Remover {item.Descricao} da comanda?",
            "Remover",
            "Cancelar");

        if (confirmar)
        {
            await Executar(() => _comandas.RemoverItemAsync(item.ComandaId, item.Id));
        }
    }

    // B4: grava a quantidade e o total juntos (Domain recalcula) e recarrega do banco
    private Task AlterarQuantidade(ItemComanda item, int quantidade) =>
        Executar(() => _comandas.AlterarQuantidadeAsync(item.ComandaId, item.Id, quantidade));

    public async Task CarregarItens()
    {
        if (ComandaAtual == null) return;

        var comandaId = ComandaAtual.Id;
        await Operacao.CarregarAsync(async () =>
        {
            var atualizada = await _repositorio.ObterLinhaAsync(comandaId);
            var lista = await _repositorio.ListarItensAsync(comandaId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (atualizada != null) ComandaAtual = atualizada;
                Itens.Clear();
                foreach (var i in lista) Itens.Add(i);
            });
        });
    }

    private async Task RemoverItem(ItemComanda item)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync("Remover",
            $"Deseja remover {item.Descricao}?", "Sim", "Não");

        if (confirm)
        {
            // B5: o Domain recalcula o total ao remover
            await Executar(() => _comandas.RemoverItemAsync(item.ComandaId, item.Id));
        }
    }

    private async Task EditarNome()
    {
        if (ComandaAtual == null) return;

        var comandaId = ComandaAtual.Id;
        string novoNome = await Shell.Current.DisplayPromptAsync("Editar",
            "Nome do Cliente:", "Salvar", "Cancelar", initialValue: ComandaAtual.NomeCliente);

        if (novoNome != null)
        {
            await Executar(() => _comandas.AlterarNomeClienteAsync(comandaId, novoNome));
        }
    }

    private async Task EditarObservacao()
    {
        if (ComandaAtual == null) return;

        var comandaId = ComandaAtual.Id;
        string observacao = await Shell.Current.DisplayPromptAsync("Observação",
            ComandaAtual.EhDelivery ? "Endereço / observação do delivery:" : "Observação:",
            "Salvar", "Cancelar", maxLength: 300, initialValue: ComandaAtual.Observacao);

        if (observacao != null)
        {
            await Executar(() => _comandas.AlterarObservacaoAsync(comandaId, observacao));
        }
    }

    private async Task FinalizarComanda()
    {
        if (ComandaAtual == null) return;

        var comandaId = ComandaAtual.Id;
        var pagamento = await _paymentService.PerguntarPagamentoAsync(ComandaAtual.Total);
        if (pagamento is null) return;

        GestaoSorveteria.Domain.Comandas.Comanda? fechada = null;
        var fechou = await Executar(async () => fechada = await _comandas.FecharAsync(comandaId, pagamento.Value));

        // B7: troco e saída da tela só depois de gravado
        if (fechou && fechada is not null)
        {
            if (fechada.TotalTroco > 0)
            {
                await _paymentService.MostrarTrocoAsync(fechada.TotalTroco);
            }

            await Shell.Current.GoToAsync(".."); // comanda fechada não fica na tela de edição
        }
    }

    /// <summary>Falha aparece para o atendente (B7); depois recarrega o que de fato está gravado.</summary>
    private async Task<bool> Executar(Func<Task> acao)
    {
        var gravou = await Operacao.GravarAsync(acao);
        await CarregarItens();
        return gravou;
    }
}
