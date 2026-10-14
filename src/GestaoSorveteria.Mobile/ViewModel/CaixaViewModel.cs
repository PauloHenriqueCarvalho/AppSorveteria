using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using GestaoSorveteria.Contracts.Avisos;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Aba Caixa (C3): abrir com fundo de troco, sangria/suprimento, números do turno e fechamento com conferência.
/// Todos os valores vêm do <c>Caixa</c> do Domain via <see cref="CaixaAppService"/>; a tela só mostra.
/// Depois de fechar, oferece o resumo do dia para o WhatsApp da dona (RN-AV-07); o botão fica aqui a qualquer hora.
/// Também lembra a contagem semanal do freezer no dia dela (RN-ES-05), com atalho para a tela.
/// </summary>
public class CaixaViewModel : BindableObject
{
    private readonly CaixaAppService _caixa;
    private readonly CaixaRepository _caixas;
    private readonly SyncService _sync;
    private readonly AvisoAppService _avisos;
    private readonly ContagemAppService _contagens;

    private bool _temCaixaAberto;
    private string _fundoTrocoTexto = string.Empty;
    private ResumoCaixa? _resumo;
    private string _ultimoFechamento = string.Empty;
    private string _avisoServidor = string.Empty;
    private bool _enviandoResumo;
    private bool _resumoPendente;
    private string _mensagemResumo = string.Empty;
    private bool _lembreteContagem;
    private string _textoContagem = "Conte o freezer uma vez por semana, na véspera do pedido.";

    public CaixaViewModel(CaixaAppService caixa, CaixaRepository caixas, SyncService sync, AvisoAppService avisos, ContagemAppService contagens)
    {
        _caixa = caixa;
        _caixas = caixas;
        _sync = sync;
        _avisos = avisos;
        _contagens = contagens;
        AbrirCaixaCommand = new Command(async () => await AbrirCaixa());
        SangriaCommand = new Command(async () => await Movimentar(sangria: true));
        SuprimentoCommand = new Command(async () => await Movimentar(sangria: false));
        FecharCaixaCommand = new Command(async () => await FecharCaixa());
        EnviarResumoCommand = new Command(async () => await EnviarResumo());
        ContagemCommand = new Command(async () => await Shell.Current.GoToAsync("ContagemView"));
    }

    public ICommand AbrirCaixaCommand { get; }
    public ICommand SangriaCommand { get; }
    public ICommand SuprimentoCommand { get; }
    public ICommand FecharCaixaCommand { get; }
    public ICommand EnviarResumoCommand { get; }
    public ICommand ContagemCommand { get; }

    /// <summary>RN-ES-05: "Hoje é dia de contagem" (véspera do pedido), até concluir a contagem de hoje.</summary>
    public bool LembreteContagem
    {
        get => _lembreteContagem;
        set { _lembreteContagem = value; OnPropertyChanged(); }
    }

    public string TextoContagem
    {
        get => _textoContagem;
        set { _textoContagem = value; OnPropertyChanged(); }
    }

    /// <summary>RN-AV-07: o resumo de hoje não saiu no fechamento (sem internet ou erro) — lembrete em destaque.</summary>
    public bool ResumoPendente
    {
        get => _resumoPendente;
        set { _resumoPendente = value; OnPropertyChanged(); }
    }

    public bool EnviandoResumo
    {
        get => _enviandoResumo;
        set { _enviandoResumo = value; OnPropertyChanged(); OnPropertyChanged(nameof(PodeEnviarResumo)); }
    }

    public bool PodeEnviarResumo => !EnviandoResumo;

    /// <summary>"Enviando as vendas e montando o resumo…" enquanto espera o servidor (pode estar acordando).</summary>
    public string MensagemResumo
    {
        get => _mensagemResumo;
        set { _mensagemResumo = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemMensagemResumo)); }
    }

    public bool TemMensagemResumo => !string.IsNullOrEmpty(MensagemResumo);

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
        ResumoPendente = _avisos.ResumoPendenteHoje;
        await AtualizarLembreteContagem();
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
        var enviarResumo = false;
        if (await Operacao.GravarAsync(async () => fechado = await _caixa.FecharAsync(contado, observacao)) && fechado is not null)
        {
            // RN-AV-07: o fechamento é o fim do turno — oferece o resumo do dia para a dona na mesma confirmação.
            enviarResumo = await Shell.Current.DisplayAlertAsync("Caixa fechado",
                $"Esperado R$ {fechado.ValorEsperado:F2} · Contado R$ {fechado.ValorContado:F2}\n{DescreverDiferenca(fechado.Diferenca ?? 0)}\n\n" +
                "O fechamento será enviado para o painel na próxima sincronização.\n\n" +
                "Enviar o resumo do dia para a dona pelo WhatsApp?",
                "Enviar resumo", "Agora não");
        }

        await Carregar();
        if (enviarResumo)
        {
            await EnviarResumo();
        }
    }

    /// <summary>
    /// RN-AV-07: envia a fila (o resumo só conta o que chegou ao servidor), pede o resumo de hoje e abre o WhatsApp da dona
    /// com a mensagem pronta. Sem internet ou com erro, avisa e deixa o lembrete com o botão "Enviar resumo do dia".
    /// </summary>
    private async Task EnviarResumo()
    {
        if (EnviandoResumo) return;

        EnviandoResumo = true;
        MensagemResumo = "Enviando as vendas e montando o resumo… se o servidor estava dormindo, pode levar até 1 minuto.";
        try
        {
            ResumoDoDiaResponse? resumo = null;
            string? erro = null;
            var sessaoVencida = false;
            try
            {
                resumo = await _avisos.ResumoDoDiaAsync();
            }
            catch (SessaoExpiradaException ex)
            {
                erro = ex.Message;
                sessaoVencida = true;
            }
            catch (ApiException ex)
            {
                erro = ex.Message;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Sorveteria] Resumo do dia: {ex}");
                erro = $"Falha ao montar o resumo. Detalhe: {ex.Message}";
            }

            MensagemResumo = string.Empty;
            if (resumo is null)
            {
                _avisos.MarcarResumoPendente();
                ResumoPendente = true;
                const string depois = "Quando der, toque em \"Enviar resumo do dia\" aqui na aba Caixa.";
                if (sessaoVencida)
                {
                    if (await Shell.Current.DisplayAlertAsync("Resumo não enviado", $"{erro}\n\n{depois}", "Entrar com PIN", "Depois"))
                    {
                        await View.LoginView.PedirPinDeNovoAsync(); // RN-US-06
                    }
                }
                else
                {
                    await Shell.Current.DisplayAlertAsync("Resumo não enviado", $"{erro}\n\n{depois}", "OK");
                }

                return;
            }

            _avisos.LimparResumoPendente();
            ResumoPendente = false;
            if (await WhatsApp.AbrirAsync(resumo.LinkWhatsApp, resumo.Texto))
            {
                AvisoRapido.Mostrar("Resumo pronto no WhatsApp: toque em enviar."); // RN-AV-08: aberto, não "entregue"
            }
        }
        finally
        {
            MensagemResumo = string.Empty;
            EnviandoResumo = false;
        }
    }

    /// <summary>Só um lembrete: se a leitura falhar, o cartão fica como estava (o caixa nunca depende da Fase 2).</summary>
    private async Task AtualizarLembreteContagem()
    {
        try
        {
            var lembrete = await _contagens.LembreteAsync();
            LembreteContagem = lembrete.Mostrar;
            TextoContagem = lembrete.Mostrar
                ? "📋 Hoje é dia de contagem do freezer. Conte antes de ir embora: a dona usa para fazer o pedido."
                : lembrete.EmAndamento
                    ? "Tem uma contagem salva pela metade. Toque para continuar."
                    : "Conte o freezer uma vez por semana, na véspera do pedido.";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Lembrete da contagem não atualizado: {ex.Message}");
        }
    }

    private static string DescreverDiferenca(decimal diferenca) => diferenca switch
    {
        0 => "Diferença: R$ 0,00 — bateu!",
        < 0 => $"Faltou R$ {Math.Abs(diferenca):F2} na gaveta",
        _ => $"Sobrou R$ {diferenca:F2} na gaveta",
    };
}
