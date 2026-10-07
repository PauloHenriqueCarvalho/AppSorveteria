using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class LoginView : ContentPage
{
    /// <summary>Rota empilhada para renovar a sessão sem trocar o atendente (docs/03, seção 7).</summary>
    public const string RotaRenovar = "RenovarSessaoView";

    /// <summary>Abre a tela de PIN por cima da atual; quem chamou tenta a operação de novo quando a tela volta.</summary>
    public static Task PedirPinDeNovoAsync() => Shell.Current.GoToAsync($"{RotaRenovar}?renovar=true");

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
