using SorveteriaMaui.Model;
using SuaSorveteria.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;

namespace SorveteriaMaui.ViewModel
{
    public class ListarComandasViewModel : BindableObject
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<Comanda> Comandas { get; set; } = new();

        private int _totalComandasAbertas;
        public int TotalComandasAbertas
        {
            get => _totalComandasAbertas;
            set { _totalComandasAbertas = value; OnPropertyChanged(); }
        }

        public ICommand NovaComandaCommand { get; }
        public ICommand AdicionarProdutoCommand { get; }
        public ICommand FinalizarComandaCommand { get; }

        public ListarComandasViewModel(DatabaseService dbService)
        {
            _dbService = dbService;

            NovaComandaCommand = new Command(async () => await AbrirNovaComanda());
            AdicionarProdutoCommand = new Command<Comanda>(async (c) => await IrParaAdicionarProduto(c));
            FinalizarComandaCommand = new Command<Comanda>(async (c) => await FecharComanda(c));

            Task.Run(CarregarComandas);
        }

        public async Task CarregarComandas()
        {
            var lista = await _dbService.GetComandasAbertas();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Comandas.Clear();
                foreach (var c in lista) Comandas.Add(c);
                TotalComandasAbertas = Comandas.Count;
            });
        }
        public ICommand VerDetalhesCommand => new Command<Comanda>(async (comanda) =>
        {
            if (comanda == null) return;

            var navigationParameter = new Dictionary<string, object>
    {
        { "ComandaSelecionada", comanda } // O nome aqui...
    };

            await Shell.Current.GoToAsync("DetalhesComandaView", navigationParameter);
        });
        private async Task AbrirNovaComanda()
        {
            // Exemplo simples: pegando o próximo número de comanda
            string nome = await Application.Current.MainPage.DisplayPromptAsync("Nova Comanda", "Nome do Cliente (Opcional):", "Abrir", "Cancelar");

            if (nome != null) // Se não cancelou
            {
                int novoNumero = Comandas.Count + 1;
                await _dbService.CriarComanda(novoNumero, nome);
                await CarregarComandas();
            }
        }

        private async Task IrParaAdicionarProduto(Comanda comanda)
        {
            // Prepara o parâmetro para a SelecaoProdutoViewModel
            var navigationParameter = new Dictionary<string, object>
    {
        { "ComandaSelecionada", comanda }
    };

            // Navega para a rota que registraremos no próximo passo
            await Shell.Current.GoToAsync("SelecaoProdutoView", navigationParameter);
        }

        private async Task FecharComanda(Comanda comanda)
        {
            bool confirm = await Application.Current.MainPage.DisplayAlert("Finalizar", $"Deseja fechar a comanda {comanda.Numero}?", "Sim", "Não");
            if (confirm)
            {
                await _dbService.FinalizarComanda(comanda.Id);
                await CarregarComandas();
            }
        }
    }
}