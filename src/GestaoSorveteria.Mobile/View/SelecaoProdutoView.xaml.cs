using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class SelecaoProdutoView : ContentPage
{
    public SelecaoProdutoView(SelecaoProdutoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>RN-ES-09: com a grade de sabores aberta, o voltar do aparelho só fecha a grade (nada é lançado).</summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is SelecaoProdutoViewModel { EscolhendoSabor: true } vm)
        {
            vm.CancelarSaborCommand.Execute(null);
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
