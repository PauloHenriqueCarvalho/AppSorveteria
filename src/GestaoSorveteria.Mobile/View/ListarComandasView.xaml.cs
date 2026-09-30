using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class ListarComandasView : ContentPage
{
    public ListarComandasView(ListarComandasViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Sobrescrevemos este método para atualizar a lista toda vez que a tela aparecer
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ListarComandasViewModel vm)
        {
            await vm.CarregarComandas();
        }
    }
}