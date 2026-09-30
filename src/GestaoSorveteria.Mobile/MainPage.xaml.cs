using SorveteriaMaui.View;
using System.Threading.Tasks;

namespace SorveteriaMaui
{
    public partial class MainPage : ContentPage
    {

        public MainPage()
        {
            InitializeComponent();
        }


        private async void OnCounterClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(ListarComandasView));
        }
    }
}
