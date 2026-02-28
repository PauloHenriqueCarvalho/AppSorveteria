using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class SelecaoProdutoView : ContentPage
{
    public SelecaoProdutoView(SelecaoProdutoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

}