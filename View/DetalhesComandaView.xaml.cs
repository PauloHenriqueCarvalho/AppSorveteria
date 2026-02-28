namespace SorveteriaMaui.View;

public partial class DetalhesComandaView : ContentPage
{
    // Verifique se a ViewModel está a ser injetada corretamente
    public DetalhesComandaView(ViewModel.DetalhesComandaViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Se você não usar o OnAppearing, a lista virá vazia
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var vm = (ViewModel.DetalhesComandaViewModel)BindingContext;
        await vm.CarregarItens();
    }
}