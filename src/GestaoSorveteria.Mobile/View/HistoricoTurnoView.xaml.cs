using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class HistoricoTurnoView : ContentPage
{
    public HistoricoTurnoView(HistoricoTurnoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is HistoricoTurnoViewModel vm)
        {
            vm.Acompanhar(true);
            await vm.Carregar();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as HistoricoTurnoViewModel)?.Acompanhar(false);
    }
}
