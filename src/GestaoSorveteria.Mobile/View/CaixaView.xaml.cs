using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class CaixaView : ContentPage
{
    public CaixaView(CaixaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CaixaViewModel vm)
        {
            await vm.Carregar();
        }
    }
}
