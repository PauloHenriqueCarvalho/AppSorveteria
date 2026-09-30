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
        }
    }

        private void CollectionView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BindingContext is ListarProdutosViewModel vm)
            {
                if (e.CurrentSelection != null && e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is Model.Produto p)
                {
                    vm.ItemSelectedCommand.Execute(e);
                }
            }
            // limpar seleção para permitir nova seleção
            if (sender is CollectionView cv) cv.SelectedItem = null;
        }
}
