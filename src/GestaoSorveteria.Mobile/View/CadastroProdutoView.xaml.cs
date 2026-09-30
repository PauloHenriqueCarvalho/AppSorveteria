using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class CadastroProdutoView : ContentPage
{
    public CadastroProdutoView(CadastroProdutoViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is CadastroProdutoViewModel vm)
        {
            await vm.LoadIfNeeded();
        }
    }
}
