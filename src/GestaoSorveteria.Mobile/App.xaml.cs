using Microsoft.Extensions.DependencyInjection;

namespace SorveteriaMaui
{
    public partial class App : Application
    {
        private readonly IServiceProvider _servicos;

        public App(IServiceProvider servicos)
        {
            InitializeComponent();
            // Tema claro sempre: as cores do Tema.xaml são da marca Verão Tropical e o balcão é bem iluminado
            UserAppTheme = AppTheme.Light;
            _servicos = servicos;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var janela = new Window(_servicos.GetRequiredService<AppShell>());
            // Tablet fica ligado o turno inteiro: ao voltar do segundo plano, o catálogo pode atualizar de novo (C2)
            janela.Resumed += (_, _) =>
            {
                _servicos.GetRequiredService<Services.Api.CatalogoService>().PermitirNovaAtualizacaoAoAbrir();
                _servicos.GetRequiredService<Services.Api.EstoqueService>().PermitirNovaAtualizacaoAoAbrir(); // Fase 2: avisos fechados pela dona
                _servicos.GetRequiredService<Services.Api.SyncService>().Disparar(); // C4: ao voltar, tenta enviar o que ficou
            };
            return janela;
        }
    }
}
