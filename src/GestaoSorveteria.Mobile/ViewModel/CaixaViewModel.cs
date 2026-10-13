using System.Collections.ObjectModel;
using System.Windows.Input;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Aba Caixa (C3): abrir com fundo de troco, sangria/suprimento, números do turno e fechamento com conferência.
/// Todos os valores vêm do <c>Caixa</c> do Domain via <see cref="CaixaAppService"/>; a tela só mostra.
/// </summary>
public class CaixaViewModel : BindableObject
{
    private readonly CaixaAppService _caixa;
    private readonly CaixaRepository _caixas;
    private readonly SorveteriaMaui.Services.Api.SyncService _sync;

    private bool _temCaixaAberto;
    private string _fundoTrocoTexto = string.Empty;
    private ResumoCaixa? _resumo;
    private string _ultimoFechamento = string.Empty;
    private string _avisoServidor = string.Empty;

    public CaixaViewModel(CaixaAppService caixa, CaixaRepository caixas, SorveteriaMaui.Services.Api.SyncService sync)
    {
        _caixa = caixa;
        _caixas = caixas;
        _sync = sync;
        AbrirCaixaCommand = new Command(async () => await AbrirCaixa());
        SangriaCommand = new Command(async () => await Movimentar(sangria: true));
        SuprimentoCommand = new Command(async () => await Movimentar(sangria: false));
        FecharCaixaCommand = new Command(async () => await FecharCaixa());
    }

    public ICommand AbrirCaixaCommand { get; }
    public ICommand SangriaCommand { get; }
    public ICommand SuprimentoCommand { get; }
    public ICommand FecharCaixaCommand { get; }

    public ObservableCollection<MovimentoCaixa> Movimentos { get; } = new();

    public bool TemCaixaAberto
    {
        get => _temCaixaAberto;
        set { _temCaixaAberto = value; OnPropertyChanged(); OnPropertyChanged(nameof(SemCaixaAberto)); }
    }

    public bool SemCaixaAberto => !TemCaixaAberto;

    /// <summary>Fundo de troco digitado na abertura (RN-CX-02, pode ser 0).</summary>
    public string FundoTrocoTexto
    {
        get => _fundoTrocoTexto;
        set { _fundoTrocoTexto = value; OnPropertyChanged(); }
    }

    public ResumoCaixa? Resumo
    {
        get => _resumo;
        set { _resumo = value; OnPropertyChanged(); }
    }

    /// <summary>Conferência do último caixa fechado, para a dona/atendente conferir depois.</summary>
    public string UltimoFechamento
    {
        get => _ultimoFechamento;
        set { _ultimoFechamento = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemUltimoFechamento)); }
    }

    public bool TemUltimoFechamento => !string.IsNullOrEmpty(UltimoFechamento);

    /// <summary>
    /// Caixa recusado pelo servidor (RN-SY-03: nunca some em silêncio). Ex.: fechamento recusado deixa o caixa
    /// aberto no servidor e impede abrir o próximo — a dona resolve pelo painel (RN-CX-09).
    /// </summary>
    public string AvisoServidor
    {
        get => _avisoServidor;
        set { _avisoServidor = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemAvisoServidor)); }
    }

    public bool TemAvisoServidor => !string.IsNullOrEmpty(AvisoServidor);

    public async Task Carregar()
    {
        await Operacao.CarregarAsync(async () =>
        {
            var resumo = await _caixa.ResumoAsync();
            var movimentos = resumo is null ? new List<MovimentoCaixa>() : await _caixas.ListarMovimentosAsync(resumo.CaixaId);
            var ultimo = resumo is null ? await _caixas.ObterUltimoFechadoAsync() : null;
            var linhaAtual = resumo is null ? ultimo : await _caixas.ObterLinhaAsync(resumo.CaixaId);
            var aviso = linhaAtual?.RejeitadoMotivo is { Length: > 0 } motivo
                ? $"O servidor recusou este caixa: {motivo}\nSe não conseguir abrir o próximo caixa, peça para a dona fechar o caixa preso pelo painel (Caixas)."
                : string.Empty;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Movimentos.Clear();
                foreach (var m in movimentos.OrderByDescending(m => m.Em)) Movimentos.Add(m);
                Resumo = resumo;
                TemCaixaAberto = resumo is not null;
                AvisoServidor = aviso;
                UltimoFechamento = ultimo is null
                    ? string.Empty
                    : $"Último caixa fechado em {ultimo.FechadoEm!.Value.ToLocalTime():dd/MM HH:mm}: " +
                      $"esperado R$ {ultimo.ValorEsperado:F2} · contado R$ {ultimo.ValorContado:F2} · {DescreverDiferenca(ultimo.Diferenca ?? 0)}";
            });
        });
    }

    private async Task AbrirCaixa()
    {
        var texto = string.IsNullOrWhiteSpace(FundoTrocoTexto) ? "0" : FundoTrocoTexto;
        if (!Conversoes.TentarLerDinheiro(texto, out var fundo) || fundo < 0)
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Fundo de troco inválido. Use só números, ex.: 50,00 (pode ser 0).", "OK");
            return;
        }

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Abrir caixa", $"Abrir o caixa com R$ {fundo:F2} de fundo de troco?", "Abrir", "Cancelar");
        if (!confirmar) return;

        if (await Operacao.GravarAsync(() => _caixa.AbrirAsync(fundo)))
        {
            FundoTrocoTexto = string.Empty;
            AvisoRapido.Mostrar($"Caixa aberto com R$ {fundo:F2}");
        }

        await Carregar();
    }

    /// <summary>RN-CX-04: valor > 0 e motivo obrigatório.</summary>
    private async Task Movimentar(bool sangria)
    {
        var titulo = sangria ? "Sangria (retirar dinheiro)" : "Suprimento (colocar dinheiro)";
        var texto = await Shell.Current.DisplayPromptAsync(titulo, "Valor:", "Próximo", "Cancelar", keyboard: Keyboard.Telephone);
        if (texto is null) return;
        if (!Conversoes.TentarLerDinheiro(texto, out var valor) || valor <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Valor inválido. Use só números, ex.: 100,00.", "OK");
            return;
        }

        var motivo = await Shell.Current.DisplayPromptAsync(titulo, $"R$ {valor:F2} — motivo (obrigatório):", "Registrar", "Cancelar", maxLength: 200);
        if (motivo is null) return;

        if (await Operacao.GravarAsync(() => sangria
                ? _caixa.RegistrarSangriaAsync(valor, motivo)
                : _caixa.RegistrarSuprimentoAsync(valor, motivo)))
        {
            AvisoRapido.Mostrar($"{(sangria ? "Sangria" : "Suprimento")} de R$ {valor:F2} registrado");
        }

        await Carregar();
    }

    /// <summary>RN-CX-05/06: confere comandas abertas, mostra esperado × contado × diferença e só então fecha.</summary>
    private async Task FecharCaixa()
    {
        var resumo = Resumo;
        if (resumo is null) return;

        if (resumo.ComandasAbertas > 0)
        {
            await Shell.Current.DisplayAlertAsync("Caixa",
                $"Ainda há {resumo.ComandasAbertas} comanda(s) aberta(s). Feche ou cancele todas antes de fechar o caixa.", "OK");
            return;
        }

        var texto = await Shell.Current.DisplayPromptAsync("Fechar caixa",
            "Conte o dinheiro da gaveta (com o fundo de troco) e digite o valor:", "Conferir", "Cancelar", keyboard: Keyboard.Telephone);
        if (texto is null) return;
        if (!Conversoes.TentarLerDinheiro(texto, out var contado) || contado < 0)
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Valor inválido. Use só números, ex.: 350,00.", "OK");
            return;
        }

        // Mesma conta do Domain (RN-CX-06), só para mostrar antes de gravar; o valor gravado é o do Caixa.Fechar.
        var diferenca = GestaoSorveteria.Domain.Common.Moeda.Arredondar(contado - resumo.Esperado);
        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Conferência",
            $"Esperado na gaveta: R$ {resumo.Esperado:F2}\n" +
            $"  fundo R$ {resumo.FundoTroco:F2} + dinheiro R$ {resumo.VendasDinheiro:F2} + suprimentos R$ {resumo.Suprimentos:F2} − sangrias R$ {resumo.Sangrias:F2}\n\n" +
            $"Contado: R$ {contado:F2}\n" +
            $"{DescreverDiferenca(diferenca)}\n\n" +
            "Fechar o caixa? Depois de fechado ele não muda.",
            "Fechar caixa", "Voltar");
        if (!confirmar) return;

        // RN-CX-05 / RN-SY-05: envio pendente não impede fechar, mas avisa que essas vendas sobem depois.
        var pendentes = (await _sync.EstadoAsync()).ComandasPendentes;
        if (pendentes > 0 && !await Shell.Current.DisplayAlertAsync("Vendas aguardando envio",
                $"{pendentes} venda(s) deste turno ainda não subiram para o painel. Pode fechar o caixa: elas são enviadas depois e entram na conferência da dona. Fechar mesmo assim?",
                "Fechar mesmo assim", "Voltar"))
        {
            return;
        }

        string? observacao = null;
        if (diferenca != 0)
        {
            observacao = await Shell.Current.DisplayPromptAsync("Fechar caixa", "Observação sobre a diferença (opcional):", "Fechar", "Sem observação", maxLength: 500);
        }

        GestaoSorveteria.Domain.Caixas.Caixa? fechado = null;
        if (await Operacao.GravarAsync(async () => fechado = await _caixa.FecharAsync(contado, observacao)) && fechado is not null)
        {
            await Shell.Current.DisplayAlertAsync("Caixa fechado",
                $"Esperado R$ {fechado.ValorEsperado:F2} · Contado R$ {fechado.ValorContado:F2}\n{DescreverDiferenca(fechado.Diferenca ?? 0)}\n\n" +
                "O fechamento será enviado para o painel na próxima sincronização.", "OK");
        }

        await Carregar();
    }

    private static string DescreverDiferenca(decimal diferenca) => diferenca switch
    {
        0 => "Diferença: R$ 0,00 — bateu!",
        < 0 => $"Faltou R$ {Math.Abs(diferenca):F2} na gaveta",
        _ => $"Sobrou R$ {diferenca:F2} na gaveta",
    };
}
