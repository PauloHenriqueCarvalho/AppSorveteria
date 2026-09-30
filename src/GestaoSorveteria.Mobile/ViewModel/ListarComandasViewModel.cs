using System.Collections.ObjectModel;
using System.Windows.Input;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;

namespace SorveteriaMaui.ViewModel;

public class ListarComandasViewModel : BindableObject
{
    private readonly ComandaAppService _comandas;
    private readonly ComandaRepository _repositorio;
    private readonly IPaymentService _paymentService;

    public ObservableCollection<Comanda> Comandas { get; set; } = new();

    private int _totalComandasAbertas;
    public int TotalComandasAbertas
    {
        get => _totalComandasAbertas;
        set { _totalComandasAbertas = value; OnPropertyChanged(); }
    }

    public ICommand NovaComandaCommand { get; }
    public ICommand NovoDeliveryCommand { get; }
    public ICommand AdicionarProdutoCommand { get; }
    public ICommand FinalizarComandaCommand { get; }
    public ICommand VendaRapidaCommand { get; }
    public ICommand VerDetalhesCommand { get; }

    public ListarComandasViewModel(ComandaAppService comandas, ComandaRepository repositorio, IPaymentService paymentService)
    {
        _comandas = comandas;
        _repositorio = repositorio;
        _paymentService = paymentService;

        NovaComandaCommand = new Command(async () => await AbrirNovaComanda());
        NovoDeliveryCommand = new Command(async () => await AbrirDelivery());
        AdicionarProdutoCommand = new Command<Comanda>(async (c) => await Navegar("SelecaoProdutoView", c));
        FinalizarComandaCommand = new Command<Comanda>(async (c) => await FecharComanda(c));
        VendaRapidaCommand = new Command(async () => await ExecutarVendaRapida());
        VerDetalhesCommand = new Command<Comanda>(async (c) => await Navegar("DetalhesComandaView", c));
        // A lista é carregada no OnAppearing da tela.
    }

    public async Task CarregarComandas()
    {
        await Operacao.CarregarAsync(async () =>
        {
            var lista = await _repositorio.ListarAbertasAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Comandas.Clear();
                foreach (var c in lista) Comandas.Add(c);
                TotalComandasAbertas = Comandas.Count;
            });
        });
    }

    private static async Task Navegar(string rota, Comanda? comanda)
    {
        if (comanda == null) return;

        await Shell.Current.GoToAsync(rota, new Dictionary<string, object> { { "ComandaSelecionada", comanda } });
    }

    private async Task AbrirNovaComanda()
    {
        string nome = await Shell.Current.DisplayPromptAsync("Nova Comanda", "Nome do Cliente (Opcional):", "Abrir", "Cancelar");

        if (nome != null) // Se não cancelou
        {
            // RN-CM-02 / B8: número sequencial no caixa, calculado ao gravar
            await Executar(() => _comandas.AbrirAsync(nome));
        }
    }

    /// <summary>RN-CM-11: delivery na F1 é marcação + observação (nome/endereço); sem taxa.</summary>
    private async Task AbrirDelivery()
    {
        string nome = await Shell.Current.DisplayPromptAsync("Delivery", "Nome do cliente (opcional):", "Próximo", "Cancelar");
        if (nome == null) return;

        string observacao = await Shell.Current.DisplayPromptAsync("Delivery", "Endereço / observação:", "Abrir", "Cancelar", maxLength: 300);
        if (observacao == null) return;

        await Executar(() => _comandas.AbrirAsync(nome, GestaoSorveteria.Domain.Comandas.TipoComanda.Delivery, observacao));
    }

    private async Task FecharComanda(Comanda comanda)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync("Finalizar", $"Deseja fechar a comanda {comanda.Numero}?", "Sim", "Não");
        if (!confirm) return;

        // Total vem do banco, não do objeto da lista (B4)
        Comanda? atual = null;
        if (!await Operacao.CarregarAsync(async () => atual = await _repositorio.ObterLinhaAsync(comanda.Id)) || atual == null) return;

        var pagamento = await _paymentService.PerguntarPagamentoAsync(atual.Total);
        if (pagamento is null) return;

        GestaoSorveteria.Domain.Comandas.Comanda? fechada = null;
        if (await Executar(async () => fechada = await _comandas.FecharAsync(atual.Id, pagamento.Value))
            && fechada is { TotalTroco: > 0 })
        {
            await _paymentService.MostrarTrocoAsync(fechada.TotalTroco); // B7: só depois de gravado
        }
    }

    private async Task ExecutarVendaRapida()
    {
        string resultado = await Shell.Current.DisplayPromptAsync(
            "Venda Rápida",
            "Valor da Venda:",
            "Confirmar",
            "Cancelar",
            keyboard: Keyboard.Telephone);

        if (string.IsNullOrWhiteSpace(resultado)) return;

        if (!Conversoes.TentarLerDinheiro(resultado, out decimal valor) || valor <= 0) // RN-CM-03: item livre > 0
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Valor inválido.", "OK");
            return;
        }

        var pagamento = await _paymentService.PerguntarPagamentoAsync(valor);
        if (pagamento is null) return;

        // RN-CM-10 / B1: abre, lança "Venda avulsa" e fecha numa gravação só
        GestaoSorveteria.Domain.Comandas.Comanda? venda = null;
        if (await Executar(async () => venda = await _comandas.VendaRapidaAsync(valor, pagamento.Value))
            && venda is { TotalTroco: > 0 })
        {
            await _paymentService.MostrarTrocoAsync(venda.TotalTroco); // B7: só depois de gravado
        }
    }

    /// <summary>Falha aparece para o atendente (B7); depois recarrega a lista com o que está gravado.</summary>
    private async Task<bool> Executar(Func<Task> acao)
    {
        var gravou = await Operacao.GravarAsync(acao);
        await CarregarComandas();
        return gravou;
    }
}
