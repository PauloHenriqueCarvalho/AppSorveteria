using SorveteriaMaui.View;

namespace SorveteriaMaui
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("SelecaoProdutoView", typeof(SelecaoProdutoView));
            Routing.RegisterRoute("DetalhesComandaView", typeof(DetalhesComandaView));
            Routing.RegisterRoute("CadastroProdutoView", typeof(View.CadastroProdutoView));
            Routing.RegisterRoute("ListarProdutosView", typeof(View.ListarProdutosView));
        }
    }
}
