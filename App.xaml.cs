using Microsoft.Extensions.DependencyInjection;
using SorveteriaMaui.View;

namespace SorveteriaMaui
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            Routing.RegisterRoute("ListarComandasView", typeof(ListarComandasView));
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}