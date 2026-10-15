using System.Diagnostics;
using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class EstaAcabandoView : ContentPage
{
    public EstaAcabandoView(EstaAcabandoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.RolarParaOInicio += AoPedirRolagem;
    }

    // Toda vez que a tela aparece (inclusive ao voltar da tela de PIN): lista do tablet na hora e atualização em segundo plano
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EstaAcabandoViewModel vm)
        {
            await vm.CarregarAsync();
        }
    }

    /// <summary>Tocou num aviso lá embaixo: o formulário do "Ainda falta" fica no alto da tela.</summary>
    private async void AoPedirRolagem(object? sender, EventArgs e)
    {
        try
        {
            await Rolagem.ScrollToAsync(0, 0, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Não rolou para o formulário: {ex.Message}");
        }
    }
}
