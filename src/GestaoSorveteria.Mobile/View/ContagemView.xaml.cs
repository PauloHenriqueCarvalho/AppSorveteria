using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class ContagemView : ContentPage
{
    public ContagemView(ContagemViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    // Toda vez que a tela aparece (inclusive ao voltar da tela de PIN): o que está no tablet na hora
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ContagemViewModel vm)
        {
            await vm.CarregarAsync();
        }
    }

    // Ao sair, a tela para de escutar a fila de envio
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (BindingContext is ContagemViewModel vm)
        {
            vm.Parar();
        }
    }
}
