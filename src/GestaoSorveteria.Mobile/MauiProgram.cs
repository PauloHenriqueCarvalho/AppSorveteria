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
            builder.Services.AddSingleton<SyncService>();     // fila de caixas/comandas/avisos → POST /api/sync/* (C4, Fase 2)
            builder.Services.AddSingleton<EstoqueService>();  // Fase 2: catálogo de estoque, WhatsApp da dona e avisos abertos

            // Banco local, repositórios e casos de uso (carrega → Domain → grava numa transação)
            builder.Services.AddSingleton<BancoLocal>();
            builder.Services.AddSingleton<ComandaRepository>();
            builder.Services.AddSingleton<ProdutoRepository>();
            builder.Services.AddSingleton<CaixaRepository>();
            builder.Services.AddSingleton<ItemEstoqueRepository>(); // Fase 2
            builder.Services.AddSingleton<AvisoRepository>();       // Fase 2
            builder.Services.AddSingleton<ContagemRepository>();    // Fase 2: contagem semanal
            builder.Services.AddSingleton<CaixaAppService>(); // C3: abrir, sangria/suprimento, fechar com conferência
            builder.Services.AddSingleton<ComandaAppService>();
            builder.Services.AddSingleton<AvisoAppService>(); // Fase 2: "Está acabando", urgente e resumo do dia
            builder.Services.AddSingleton<ContagemAppService>(); // Fase 2: contagem semanal do freezer (RN-ES-05/06)
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

            // Caixa (C3)
            builder.Services.AddTransient<CaixaView>();
            builder.Services.AddTransient<CaixaViewModel>();

            // Histórico do turno (C5)
            builder.Services.AddTransient<HistoricoTurnoView>();
            builder.Services.AddTransient<HistoricoTurnoViewModel>();

            // "Está acabando" (Fase 2)
            builder.Services.AddTransient<EstaAcabandoView>();
            builder.Services.AddTransient<EstaAcabandoViewModel>();

            // Contagem semanal (Fase 2)
            builder.Services.AddTransient<ContagemView>();
            builder.Services.AddTransient<ContagemViewModel>();

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
