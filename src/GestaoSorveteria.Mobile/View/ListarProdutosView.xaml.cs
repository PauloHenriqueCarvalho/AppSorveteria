using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class ListarProdutosView : ContentPage
{
    public ListarProdutosView(ListarProdutosViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ListarProdutosViewModel vm)
        {
            await vm.CarregarProdutos();
            await vm.TentarDeNovoSeRenovouAsync(); // voltou da tela de PIN (sessão vencida)
        }
    }
}
