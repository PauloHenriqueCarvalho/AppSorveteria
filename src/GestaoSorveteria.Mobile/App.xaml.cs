using Microsoft.Extensions.DependencyInjection;

namespace SorveteriaMaui
{
    public partial class App : Application
    {
        private readonly IServiceProvider _servicos;

        public App(IServiceProvider servicos)
        {
            InitializeComponent();
            _servicos = servicos;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(_servicos.GetRequiredService<AppShell>());
        }
    }
}
