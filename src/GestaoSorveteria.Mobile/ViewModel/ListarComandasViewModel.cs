using System.Collections.ObjectModel;
using System.Windows.Input;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.ViewModel;

public class ListarComandasViewModel : BindableObject
{
    private readonly ComandaAppService _comandas;
    private readonly ComandaRepository _repositorio;
    private readonly IPaymentService _paymentService;
    private readonly SessaoUsuario _sessao;
    private readonly CatalogoService _catalogo;
    private readonly CaixaAppService _caixa;
    private readonly SyncService _sync;
    private readonly CatalogoService _catalogoCiclo;
    private readonly EstoqueService _estoque;
    private readonly AvisoRepository _avisos;
    private readonly ContagemAppService _contagens;

    public ObservableCollection<Comanda> Comandas { get; set; } = new();

    private string _textoEstaAcabando = "📦  Está acabando";
    /// <summary>Fase 2: botão do "Está acabando", com quantos avisos estão abertos.</summary>
    public string TextoEstaAcabando
    {
        get => _textoEstaAcabando;
        set { _textoEstaAcabando = value; OnPropertyChanged(); }
    }

    private string _textoContagem = "📋  Contagem";
    /// <summary>Fase 2: botão da contagem semanal ("Continuar contagem" se há uma salva pela metade).</summary>
    public string TextoContagem
    {
        get => _textoContagem;
        set { _textoContagem = value; OnPropertyChanged(); }
    }

    private bool _lembreteContagem;
    /// <summary>RN-ES-05: "Hoje é dia de contagem" (véspera do pedido), até concluir a contagem de hoje.</summary>
    public bool LembreteContagem
    {
        get => _lembreteContagem;
        set { _lembreteContagem = value; OnPropertyChanged(); }
    }

    private string _textoLembreteContagem = string.Empty;
    public string TextoLembreteContagem
    {
        get => _textoLembreteContagem;
        set { _textoLembreteContagem = value; OnPropertyChanged(); }
    }

    private string _indicadorEnvio = string.Empty;
    /// <summary>RN-SY-05: "X vendas aguardando envio", sempre visível. Toque = enviar agora (ou entrar com o PIN).</summary>
    public string IndicadorEnvio
    {
        get => _indicadorEnvio;
        set { _indicadorEnvio = value; OnPropertyChanged(); }
    }

    private string _mensagemEnvio = string.Empty;
    public string MensagemEnvio
    {
        get => _mensagemEnvio;
        set { _mensagemEnvio = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemMensagemEnvio)); }
    }

    public bool TemMensagemEnvio => !string.IsNullOrWhiteSpace(MensagemEnvio);

    public ICommand EnviarAgoraCommand { get; private set; } = null!;

    private async Task AtualizarIndicador()
    {
        var estado = await _sync.EstadoAsync();
        // Avisos "Está acabando" e contagens na fila entram junto, sem misturar com a contagem de vendas.
        var estoqueNaFila = string.Join(" e ", new[]
        {
            estado.AvisosPendentes > 0 ? $"{estado.AvisosPendentes} aviso(s) de estoque" : null,
            estado.ContagensPendentes > 0 ? $"{estado.ContagensPendentes} contagem(ns)" : null,
        }.Where(t => t is not null));
        var estoqueNoFim = estoqueNaFila.Length > 0 ? $" · {estoqueNaFila}" : string.Empty;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IndicadorEnvio = estado.Sincronizando ? "⏳ Enviando vendas…"
                : estado.PrecisaPin ? "🔒 Sessão vencida — toque para entrar e enviar"
                : estado.Pendentes == 0 ? (estado.Rejeitadas > 0 ? $"⚠ {estado.Rejeitadas} venda(s) rejeitada(s) — veja o histórico"
                    : estoqueNaFila.Length > 0 ? $"📤 {estoqueNaFila} aguardando envio"
                    : "✅ Tudo enviado")
                : estado.ComandasPendentes == 1 && estado.CaixasPendentes == 0 ? $"📤 1 venda aguardando envio{estoqueNoFim}"
                : $"📤 {estado.ComandasPendentes} venda(s) e {estado.CaixasPendentes} caixa(s) aguardando envio{estoqueNoFim}";
            MensagemEnvio = estado.Pendentes > 0 || estoqueNaFila.Length > 0 || estado.PrecisaPin ? estado.UltimaMensagem : string.Empty;
        });
        await AtualizarBotaoEstaAcabando();
        await AtualizarContagem();
    }

    /// <summary>
    /// RN-ES-05: lembrete "Hoje é dia de contagem" e o texto do botão. Como o contador do "Está acabando", se a leitura
    /// falhar fica como estava — a lista de comandas (a venda) nunca depende da Fase 2.
    /// </summary>
    private async Task AtualizarContagem()
    {
        try
        {
            var lembrete = await _contagens.LembreteAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TextoContagem = lembrete.EmAndamento ? "📋  Continuar contagem" : "📋  Contagem";
                LembreteContagem = lembrete.Mostrar;
                TextoLembreteContagem = lembrete.EmAndamento
                    ? "📋  Hoje é dia de contagem do freezer — toque para continuar a contagem"
                    : "📋  Hoje é dia de contagem do freezer — toque aqui para contar";
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sorveteria] Lembrete da contagem não atualizado: {ex.Message}");
        }
    }

    /// <summary>
    /// Quantos avisos estão abertos, no próprio botão. É só um contador: se a leitura falhar, o botão fica sem número
    /// e nada mais muda — a lista de comandas (a venda) nunca depende da Fase 2.
    /// </summary>
    private async Task AtualizarBotaoEstaAcabando()
    {
        try
        {
            var abertos = await _avisos.ContarAbertosAsync();
            MainThread.BeginInvokeOnMainThread(() =>
                TextoEstaAcabando = abertos == 0 ? "📦  Está acabando" : $"📦  Está acabando  ·  {abertos} avisado(s)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sorveteria] Contador do \"Está acabando\" não atualizado: {ex.Message}");
        }
    }

    private async Task EnviarAgora()
    {
        if ((await _sync.EstadoAsync()).PrecisaPin)
        {
            await View.LoginView.PedirPinDeNovoAsync(); // RN-US-06: a fila espera o PIN
            return;
        }

        await _sync.SincronizarAsync(silencioso: false);
    }

    private string _estadoCaixa = string.Empty;
    /// <summary>C3: "Caixa aberto desde HH:mm" ou aviso de caixa fechado.</summary>
    public string EstadoCaixa
    {
        get => _estadoCaixa;
        set { _estadoCaixa = value; OnPropertyChanged(); }
    }

    /// <summary>Quem está no turno (C1). Vazio se ninguém entrou.</summary>
    public string NomeAtendente => _sessao.Nome;

    private int _totalComandasAbertas;
    public int TotalComandasAbertas
    {
        get => _totalComandasAbertas;
        set { _totalComandasAbertas = value; OnPropertyChanged(); }
    }

    public ICommand NovaComandaCommand { get; }
    public ICommand NovoDeliveryCommand { get; }
    public ICommand AdicionarProdutoCommand { get; }
    public ICommand FinalizarComandaCommand { get; }
    public ICommand VendaRapidaCommand { get; }
    public ICommand VerDetalhesCommand { get; }
    public ICommand SairCommand { get; }
    public ICommand EstaAcabandoCommand { get; }
    public ICommand ContagemCommand { get; }

    public ListarComandasViewModel(ComandaAppService comandas, ComandaRepository repositorio, IPaymentService paymentService, SessaoUsuario sessao, CatalogoService catalogo, CaixaAppService caixa, SyncService sync, EstoqueService estoque, AvisoRepository avisos, ContagemAppService contagens)
    {
        _comandas = comandas;
        _repositorio = repositorio;
        _paymentService = paymentService;
        _sessao = sessao;
        _catalogo = catalogo;
        _catalogoCiclo = catalogo;
        _caixa = caixa;
        _sync = sync;
        _estoque = estoque;
        _avisos = avisos;
        _contagens = contagens;

        EnviarAgoraCommand = new Command(async () => await EnviarAgora());
        _sync.Mudou += async (_, _) => await AtualizarIndicador();
        _sync.CatalogoNoCiclo ??= () => _catalogoCiclo.AtualizarSeAntigoAsync(TimeSpan.FromMinutes(5));
        _sync.EstoqueNoCiclo ??= () => _estoque.AtualizarSeAntigoAsync(TimeSpan.FromMinutes(5)); // Fase 2: no máximo a cada 5 min
        _sync.IniciarCiclo(); // C4: a cada 1 min (idempotente)

        // Fase 2 (docs/10 §2.2): não depende do caixa — dá para avisar com o caixa fechado
        EstaAcabandoCommand = new Command(async () => await Shell.Current.GoToAsync("EstaAcabandoView"));
        // Fase 2 (docs/10 §2.1): contagem semanal do freezer, também sem caixa aberto
        ContagemCommand = new Command(async () => await Shell.Current.GoToAsync("ContagemView"));

        SairCommand = new Command(async () => await Sair());
        NovaComandaCommand = new Command(async () => await AbrirNovaComanda());
        NovoDeliveryCommand = new Command(async () => await AbrirDelivery());
        AdicionarProdutoCommand = new Command<Comanda>(async (c) => await Navegar("SelecaoProdutoView", c));
        FinalizarComandaCommand = new Command<Comanda>(async (c) => await FecharComanda(c));
        VendaRapidaCommand = new Command(async () => await ExecutarVendaRapida());
        VerDetalhesCommand = new Command<Comanda>(async (c) => await Navegar("DetalhesComandaView", c));
        // A lista é carregada no OnAppearing da tela.
    }

    public async Task CarregarComandas()
    {
        OnPropertyChanged(nameof(NomeAtendente));
        _ = _catalogo.AtualizarAoAbrirAsync(); // C2: uma vez por execução, em segundo plano; nunca segura a tela
        _ = _estoque.AtualizarAoAbrirAsync();  // Fase 2: catálogo de estoque, WhatsApp da dona e avisos abertos (idem)
        _sync.Disparar();                      // C4: ao abrir o app / voltar à tela
        _ = AtualizarIndicador();
        await Operacao.CarregarAsync(async () =>
        {
            var lista = await _repositorio.ListarAbertasAsync();
            var caixa = await _caixa.ObterAbertoLinhaAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Comandas.Clear();
                foreach (var c in lista) Comandas.Add(c);
                TotalComandasAbertas = Comandas.Count;
                EstadoCaixa = caixa is null
                    ? "⚠ Caixa fechado — abra na aba Caixa"
                    : $"Caixa aberto desde {caixa.AbertoEm.ToLocalTime():HH:mm}";
            });
        });
    }

    /// <summary>RN-CX-03 / C3: sem caixa aberto não há venda; leva o atendente para a aba Caixa.</summary>
    private async Task<bool> GarantirCaixaAberto()
    {
        Caixa? caixa = null;
        if (!await Operacao.CarregarAsync(async () => caixa = await _caixa.ObterAbertoLinhaAsync())) return false;
        if (caixa is not null) return true;

        bool abrir = await Shell.Current.DisplayAlertAsync(
            "Caixa fechado",
            "Não há caixa aberto. Abra o caixa (com o fundo de troco) antes de vender.",
            "Abrir caixa",
            "Agora não");
        if (abrir)
        {
            await Shell.Current.GoToAsync("//CaixaView");
        }

        return false;
    }

    /// <summary>
    /// Trocar de atendente: apaga a sessão do aparelho e volta ao PIN. O próximo login exige internet, então sem
    /// conexão o app recusa — um toque errado não pode deixar o balcão sem vender (Decisão 1).
    /// </summary>
    private async Task Sair()
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await Shell.Current.DisplayAlertAsync(
                "Sem internet",
                $"Sem internet não dá para trocar de atendente agora. As vendas continuam na conta de {_sessao.Nome}.",
                "OK");
            return;
        }

        bool confirmar = await Shell.Current.DisplayAlertAsync(
            "Sair",
            $"Sair da conta de {_sessao.Nome}? Para entrar de novo é preciso digitar o PIN com internet. As comandas ficam guardadas no aparelho.",
            "Sair",
            "Cancelar");
        if (!confirmar) return;

        if (await Operacao.GravarAsync(_sessao.EncerrarAsync))
        {
            await Shell.Current.GoToAsync("//LoginView");
        }
    }

    private static async Task Navegar(string rota, Comanda? comanda)
    {
        if (comanda == null) return;

        await Shell.Current.GoToAsync(rota, new Dictionary<string, object> { { "ComandaSelecionada", comanda } });
    }

    private async Task AbrirNovaComanda()
    {
        if (!await GarantirCaixaAberto()) return;

        string nome = await Shell.Current.DisplayPromptAsync("Nova Comanda", "Nome do Cliente (Opcional):", "Abrir", "Cancelar");

        if (nome != null) // Se não cancelou
        {
            // RN-CM-02 / B8: número sequencial no caixa, calculado ao gravar
            await Executar(() => _comandas.AbrirAsync(nome));
        }
    }

    /// <summary>RN-CM-11: delivery na F1 é marcação + observação (nome/endereço); sem taxa.</summary>
    private async Task AbrirDelivery()
    {
        if (!await GarantirCaixaAberto()) return;

        string nome = await Shell.Current.DisplayPromptAsync("Delivery", "Nome do cliente (opcional):", "Próximo", "Cancelar");
        if (nome == null) return;

        string observacao = await Shell.Current.DisplayPromptAsync("Delivery", "Endereço / observação:", "Abrir", "Cancelar", maxLength: 300);
        if (observacao == null) return;

        await Executar(() => _comandas.AbrirAsync(nome, GestaoSorveteria.Domain.Comandas.TipoComanda.Delivery, observacao));
    }

    private async Task FecharComanda(Comanda comanda)
    {
        bool confirm = await Shell.Current.DisplayAlertAsync("Finalizar", $"Deseja fechar a comanda {comanda.Numero}?", "Sim", "Não");
        if (!confirm) return;

        // Total vem do banco, não do objeto da lista (B4)
        Comanda? atual = null;
        if (!await Operacao.CarregarAsync(async () => atual = await _repositorio.ObterLinhaAsync(comanda.Id)) || atual == null) return;

        var pagamentos = await _paymentService.PerguntarPagamentosAsync(atual.Total);
        if (pagamentos is null) return;

        GestaoSorveteria.Domain.Comandas.Comanda? fechada = null;
        if (await Executar(async () => fechada = await _comandas.FecharAsync(atual.Id, pagamentos))
            && fechada is { TotalTroco: > 0 })
        {
            await _paymentService.MostrarTrocoAsync(fechada.TotalTroco); // B7: só depois de gravado
        }
    }

    private async Task ExecutarVendaRapida()
    {
        if (!await GarantirCaixaAberto()) return;

        string resultado = await Shell.Current.DisplayPromptAsync(
            "Venda Rápida",
            "Valor da Venda:",
            "Confirmar",
            "Cancelar",
            keyboard: Keyboard.Telephone);

        if (string.IsNullOrWhiteSpace(resultado)) return;

        if (!Conversoes.TentarLerDinheiro(resultado, out decimal valor) || valor <= 0) // RN-CM-03: item livre > 0
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Valor inválido.", "OK");
            return;
        }

        var pagamentos = await _paymentService.PerguntarPagamentosAsync(valor);
        if (pagamentos is null) return;

        // RN-CM-10 / B1: abre, lança "Venda avulsa" e fecha numa gravação só
        GestaoSorveteria.Domain.Comandas.Comanda? venda = null;
        if (await Executar(async () => venda = await _comandas.VendaRapidaAsync(valor, pagamentos))
            && venda is { TotalTroco: > 0 })
        {
            await _paymentService.MostrarTrocoAsync(venda.TotalTroco); // B7: só depois de gravado
        }
    }

    /// <summary>Falha aparece para o atendente (B7); depois recarrega a lista com o que está gravado.</summary>
    private async Task<bool> Executar(Func<Task> acao)
    {
        var gravou = await Operacao.GravarAsync(acao);
        await CarregarComandas();
        return gravou;
    }
}
