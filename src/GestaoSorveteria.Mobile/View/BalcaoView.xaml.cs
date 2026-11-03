using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class BalcaoView : ContentPage
{
    public BalcaoView(BalcaoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is BalcaoViewModel vm)
        {
            await vm.AoAparecerAsync();
        }
    }

    /// <summary>RN-ES-09: com a grade de sabores aberta, o voltar do aparelho só fecha a grade (nada é lançado).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is BalcaoViewModel { EscolhendoSabor: true } vm)
        {
            vm.CancelarSaborCommand.Execute(null);
            return true;
        }

        // Venda montada e ainda não cobrada: confirma antes de sair (nada foi gravado nem pago)
        if (BindingContext is BalcaoViewModel { TemItens: true })
        {
            Dispatcher.Dispatch(async () =>
            {
                if (await DisplayAlertAsync("Sair do balcão", "Esta venda ainda não foi cobrada. Sair e descartar os itens?", "Sair", "Ficar"))
                {
                    await Shell.Current.GoToAsync("..");
                }
            });
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
