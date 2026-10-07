using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class LoginView : ContentPage
{
    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
