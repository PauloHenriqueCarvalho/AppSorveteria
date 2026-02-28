using Microsoft.Extensions.Logging;
using SorveteriaMaui.View;
using SorveteriaMaui.ViewModel;
using SuaSorveteria.Services;

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
            // Registrar o Banco
            builder.Services.AddSingleton<DatabaseService>();

            // Registrar as Views e ViewModels
            // No MauiProgram.cs
            builder.Services.AddSingleton<DatabaseService>();

            // Listagem
            builder.Services.AddTransient<ListarComandasView>();
            builder.Services.AddTransient<ListarComandasViewModel>();

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
