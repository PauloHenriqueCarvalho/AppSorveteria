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
            var janela = new Window(_servicos.GetRequiredService<AppShell>());
            // Tablet fica ligado o turno inteiro: ao voltar do segundo plano, o catálogo pode atualizar de novo (C2)
            janela.Resumed += (_, _) => _servicos.GetRequiredService<Services.Api.CatalogoService>().PermitirNovaAtualizacaoAoAbrir();
            return janela;
        }
    }
}
