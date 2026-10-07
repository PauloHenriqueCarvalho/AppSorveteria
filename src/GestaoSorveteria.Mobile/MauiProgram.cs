using Microsoft.Extensions.Logging;
using SorveteriaMaui.View;
using SorveteriaMaui.ViewModel;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;
using SorveteriaMaui.Data;

namespace SorveteriaMaui
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            // Shell decide a tela inicial (login ou comandas) pela sessão guardada
            builder.Services.AddSingleton<AppShell>();

            // API: endereço configurável, sessão (token no SecureStorage) e cliente HTTP único com timeout de 90 s
            builder.Services.AddSingleton<ConfiguracaoApi>();
            builder.Services.AddSingleton<SessaoUsuario>();
            builder.Services.AddSingleton<ApiCliente>();
            builder.Services.AddSingleton<CatalogoService>(); // catálogo da API em cache no SQLite (C2)

            // Banco local, repositórios e casos de uso (carrega → Domain → grava numa transação)
            builder.Services.AddSingleton<BancoLocal>();
            builder.Services.AddSingleton<ComandaRepository>();
            builder.Services.AddSingleton<ProdutoRepository>();
            builder.Services.AddSingleton<CaixaProvisorio>();
            builder.Services.AddSingleton<ComandaAppService>();
            // Diálogos de pagamento (não gravam nada)
            builder.Services.AddSingleton<IPaymentService, PaymentService>();

            // Login com PIN (C1)
            builder.Services.AddTransient<LoginView>();
            builder.Services.AddTransient<LoginViewModel>();

            // Listagem
            builder.Services.AddTransient<ListarComandasView>();
            builder.Services.AddTransient<ListarComandasViewModel>();

            // Produtos
            builder.Services.AddTransient<ListarProdutosView>();
            builder.Services.AddTransient<ListarProdutosViewModel>();

            // Seleção de Produto
            builder.Services.AddTransient<SelecaoProdutoView>();
            builder.Services.AddTransient<SelecaoProdutoViewModel>();

            builder.Services.AddTransient<DetalhesComandaViewModel>();
            builder.Services.AddTransient<DetalhesComandaView>();
#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
