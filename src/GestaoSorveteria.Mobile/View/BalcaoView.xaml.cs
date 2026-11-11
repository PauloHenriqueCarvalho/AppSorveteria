using SorveteriaMaui.ViewModel;

namespace SorveteriaMaui.View;

public partial class BalcaoView : ContentPage
{
    /// <summary>Abaixo desta largura (celular em pé) a venda vai para baixo dos produtos.</summary>
    private const double LarguraMinimaLadoALado = 720;

    private bool? _compacto;

    public BalcaoView(BalcaoViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        SizeChanged += (_, _) => AjustarAoTamanho();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is BalcaoViewModel vm)
        {
            await vm.AoAparecerAsync();
        }
    }

    /// <summary>
    /// RN-CM-13: tablet (ou celular deitado) mostra produtos e venda lado a lado; celular em pé mostra produtos em cima e
    /// venda/pagamento embaixo, com 2 botões por linha e as 4 formas de pagamento numa linha só.
    /// </summary>
    private void AjustarAoTamanho()
    {
        if (Width <= 0) return;
        var compacto = Width < LarguraMinimaLadoALado;
        if (_compacto == compacto) return;
        _compacto = compacto;

        Raiz.ColumnDefinitions.Clear();
        Raiz.RowDefinitions.Clear();
        Grid[] areasDeCima = [AreaProdutos, AreaSabores, AreaTeclado, AreaTroco];

        if (compacto)
        {
            Raiz.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Raiz.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Raiz.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            foreach (var area in areasDeCima)
            {
                Grid.SetRow(area, 0);
                Grid.SetColumn(area, 0);
                area.Padding = new Thickness(10, 10, 10, 6);
            }

            Grid.SetRow(PainelVenda, 1);
            Grid.SetColumn(PainelVenda, 0);
            PainelVenda.Padding = new Thickness(10, 8);
            ListaItens.MaximumHeightRequest = 132; // ~3 linhas; mais itens rolam dentro da venda

            LayoutProdutos.Span = 2;
            LayoutSabores.Span = 2;
            LayoutNotas.Span = 2;
            PagamentoNumaLinha(true);
        }
        else
        {
            Raiz.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(3, GridUnitType.Star)));
            Raiz.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            Raiz.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            foreach (var area in areasDeCima)
            {
                Grid.SetRow(area, 0);
                Grid.SetColumn(area, 0);
                area.Padding = new Thickness(12, 12, 6, 8);
            }

            Grid.SetRow(PainelVenda, 0);
            Grid.SetColumn(PainelVenda, 1);
            PainelVenda.Padding = new Thickness(12);
            ListaItens.MaximumHeightRequest = double.PositiveInfinity;

            LayoutProdutos.Span = 3;
            LayoutSabores.Span = 3;
            LayoutNotas.Span = 3;
            PagamentoNumaLinha(false);
        }
    }

    /// <summary>Celular: Pix · Débito · Crédito · Dinheiro numa linha. Tablet: 2 × 2, botões maiores.</summary>
    private void PagamentoNumaLinha(bool numaLinha)
    {
        GradePagamento.ColumnDefinitions.Clear();
        GradePagamento.RowDefinitions.Clear();
        Button[] botoes = [BotaoPix, BotaoDebito, BotaoCredito, BotaoDinheiro];
        var colunas = numaLinha ? 4 : 2;
        for (var c = 0; c < colunas; c++)
        {
            GradePagamento.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        for (var r = 0; r < botoes.Length / colunas; r++)
        {
            GradePagamento.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var i = 0; i < botoes.Length; i++)
        {
            Grid.SetRow(botoes[i], i / colunas);
            Grid.SetColumn(botoes[i], i % colunas);
            botoes[i].FontSize = numaLinha ? 15 : 18;
            botoes[i].HeightRequest = numaLinha ? 52 : 58;
        }
    }

    /// <summary>
    /// Voltar do aparelho: fecha o painel aberto (sabor, teclado, troco) sem lançar nada; com venda montada, confirma
    /// antes de descartar (nada foi gravado nem pago). Sem nada aberto, o comportamento normal da primeira aba.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is not BalcaoViewModel vm)
        {
            return base.OnBackButtonPressed();
        }

        if (vm.EscolhendoSabor)
        {
            vm.CancelarSaborCommand.Execute(null);
            return true;
        }

        if (vm.DigitandoValor)
        {
            vm.CancelarValorCommand.Execute(null);
            return true;
        }

        if (vm.EscolhendoTroco)
        {
            vm.CancelarTrocoCommand.Execute(null);
            return true;
        }

        if (vm.TemItens)
        {
            Dispatcher.Dispatch(async () =>
            {
                if (await DisplayAlertAsync("Descartar venda", "Esta venda ainda não foi cobrada. Tirar todos os itens?", "Descartar", "Ficar"))
                {
                    vm.LimparVenda();
                }
            });
            return true;
        }

        return base.OnBackButtonPressed();
    }
}
