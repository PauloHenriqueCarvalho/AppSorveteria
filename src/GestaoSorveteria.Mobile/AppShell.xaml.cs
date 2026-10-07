using SorveteriaMaui.Services.Api;
using SorveteriaMaui.View;

namespace SorveteriaMaui
{
    public partial class AppShell : Shell
    {
        public AppShell(SessaoUsuario sessao)
        {
            InitializeComponent();
            Routing.RegisterRoute("SelecaoProdutoView", typeof(SelecaoProdutoView));
            Routing.RegisterRoute("DetalhesComandaView", typeof(DetalhesComandaView));
            Routing.RegisterRoute("CadastroProdutoView", typeof(View.CadastroProdutoView));

            // Sem atendente no aparelho → tela de PIN. Token vencido não bloqueia: a venda segue offline (docs/03, seção 7).
            if (!sessao.TemUsuario)
            {
                // O XAML embrulha o ShellContent "Login" num ShellItem implícito; é ele que vira o item atual.
                var itemLogin = Items.FirstOrDefault(item => item.Items.Any(secao => secao.Items.Contains(Login)));
                if (itemLogin is not null)
                {
                    CurrentItem = itemLogin;
                }
            }
        }
    }
}
