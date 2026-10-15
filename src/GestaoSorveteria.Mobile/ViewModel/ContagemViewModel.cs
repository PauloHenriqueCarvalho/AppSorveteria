using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Um item na lista da contagem: nome, como contar (caixa de meia em meia, balde inteiro…), o que já foi contado nesta
/// contagem e a contagem anterior só como referência (RN-ES-06: o campo começa vazio).
/// </summary>
public sealed class LinhaContagem : BindableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private decimal? _contado;

    public LinhaContagem(ItemParaContar item)
    {
        Item = item.Item;
        Passo = item.Regra.PassoEmbalagens;
        _contado = item.Contado;
        AnteriorTexto = item.Anterior is { } anterior
            ? $"Contagem anterior: {Formatar(anterior.Embalagens)} {NomeDaUnidade(anterior.Embalagens)} ({DiaDaLoja.HoraLocal(anterior.ConcluidaEm):dd/MM})"
            : string.Empty;
    }

    public ItemEstoqueLocal Item { get; }

    /// <summary>Passo do Domain: 0,5 para caixa, 1 para balde, pote e pacote.</summary>
    public decimal Passo { get; }

    public string Nome => Item.Nome;

    /// <summary>Em embalagens; nulo = ainda não contado (nunca vira 0 sozinho).</summary>
    public decimal? Contado
    {
        get => _contado;
        set
        {
            _contado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Contou));
            OnPropertyChanged(nameof(Nenhum));
            OnPropertyChanged(nameof(ValorTexto));
            OnPropertyChanged(nameof(UnidadeTexto));
        }
    }

    public bool Contou => Contado is not null;

    /// <summary>"Não tem nenhum" (0) fica em vermelho.</summary>
    public bool Nenhum => Contado == 0m;

    public string ValorTexto => Contado is { } valor ? Formatar(valor) : "—";

    public string UnidadeTexto => Contado switch
    {
        null => "toque para digitar",
        0m => "não tem nenhum",
        { } valor => NomeDaUnidade(valor),
    };

    /// <summary>Pergunta ao digitar: "Quantas caixas tem agora?".</summary>
    public string PerguntaQuantidade => Item.Unidade switch
    {
        UnidadeCompra.Caixa => "Quantas caixas tem agora?",
        UnidadeCompra.Balde => "Quantos baldes tem agora?",
        UnidadeCompra.Pacote => "Quantos pacotes tem agora?",
        _ => "Quantas unidades tem agora?",
    };

    /// <summary>Como contar este item ("Caixa com 28 · meia caixa = 0,5").</summary>
    public string Embalagem => Item.Unidade switch
    {
        UnidadeCompra.Caixa when Passo < 1m && Item.UnidadesPorEmbalagem > 1 => $"Em caixas (com {Item.UnidadesPorEmbalagem}) · meia caixa = 0,5",
        UnidadeCompra.Caixa when Passo < 1m => "Em caixas · meia caixa = 0,5",
        UnidadeCompra.Caixa => "Em caixas",
        UnidadeCompra.Balde => "Em baldes",
        UnidadeCompra.Pacote when Item.UnidadesPorEmbalagem > 1 => $"Em pacotes (com {Item.UnidadesPorEmbalagem})",
        UnidadeCompra.Pacote => "Em pacotes",
        _ => "Em unidades",
    };

    public string AnteriorTexto { get; }

    public bool TemAnterior => AnteriorTexto.Length > 0;

    /// <summary>"2,5", "3", "0".</summary>
    public static string Formatar(decimal valor) => valor.ToString("0.#", PtBr);

    private string NomeDaUnidade(decimal valor)
    {
        var uma = valor > 0m && valor <= 1m; // "0,5 caixa", "1 balde", "3 caixas"
        return Item.Unidade switch
        {
            UnidadeCompra.Caixa => uma ? "caixa" : "caixas",
            UnidadeCompra.Balde => uma ? "balde" : "baldes",
            UnidadeCompra.Pacote => uma ? "pacote" : "pacotes",
            _ => uma ? "unidade" : "unidades",
        };
    }
}

/// <summary>Cabeçalho de grupo da lista (Picolé de fruta, Picolé ao leite, Massa…), na ordem da planilha.</summary>
public sealed class GrupoDaContagem : ObservableCollection<LinhaContagem>
{
    public GrupoDaContagem(string nome, IEnumerable<LinhaContagem> linhas) : base(linhas)
    {
        Nome = nome;
    }

    public string Nome { get; }

    /// <summary>"3 de 16".</summary>
    public string Progresso => $"{this.Count(l => l.Contou)} de {Count}";

    public void AtualizarProgresso() => OnPropertyChanged(new PropertyChangedEventArgs(nameof(Progresso)));
}

/// <summary>
/// Tela da contagem semanal do freezer (Fase 2, RN-ES-05/06, docs/10 §2.1). Lista na ordem da planilha, com − e + grandes
/// (caixa de meia em meia), toque no número para digitar ("2,5") e "Não tem nenhum" em um toque. Cada toque já fica gravado
/// no tablet; "Salvar e continuar depois" sai da tela (e tenta enviar o parcial); "Concluir" fecha a contagem (depois não
/// muda). Funciona sem internet: tudo sobe pela fila de envio.
/// </summary>
public class ContagemViewModel : BindableObject
{
    private readonly ContagemAppService _contagens;
    private readonly ContagemRepository _repositorio;
    private readonly EstoqueService _estoque;
    private readonly SyncService _sync;

    private readonly Dictionary<LinhaContagem, GrupoDaContagem> _grupoDaLinha = new();
    private ObservableCollection<GrupoDaContagem> _grupos = new();
    private ContagemLocal? _contagem;
    private ContagemLocal? _ultimaConcluida;
    private int _total;
    private int _contados;
    private bool _perguntouSobreAntiga;
    private bool _ouvindoFila;
    private bool _ocupado;
    private bool _atualizando;
    private string _situacao = string.Empty;

    public ContagemViewModel(ContagemAppService contagens, ContagemRepository repositorio, EstoqueService estoque, SyncService sync)
    {
        _contagens = contagens;
        _repositorio = repositorio;
        _estoque = estoque;
        _sync = sync;

        MaisCommand = new Command<LinhaContagem>(async linha => await Somar(linha, +1));
        MenosCommand = new Command<LinhaContagem>(async linha => await Menos(linha));
        NenhumCommand = new Command<LinhaContagem>(async linha => await NaoTemNenhum(linha));
        DigitarCommand = new Command<LinhaContagem>(async linha => await Digitar(linha));
        SalvarCommand = new Command(async () => await SalvarParcial());
        ConcluirCommand = new Command(async () => await Concluir());
        ComecarOutraCommand = new Command(async () => await ComecarOutra());
        AtualizarCommand = new Command(async () => await AtualizarDoServidorAsync(manual: true));
    }

    public ICommand MaisCommand { get; }
    public ICommand MenosCommand { get; }
    public ICommand NenhumCommand { get; }
    public ICommand DigitarCommand { get; }
    public ICommand SalvarCommand { get; }
    public ICommand ConcluirCommand { get; }
    public ICommand ComecarOutraCommand { get; }
    public ICommand AtualizarCommand { get; }

    /// <summary>Grupos na ordem da planilha, cada um com os itens na ordem do freezer.</summary>
    public ObservableCollection<GrupoDaContagem> Grupos
    {
        get => _grupos;
        private set { _grupos = value; OnPropertyChanged(); }
    }

    /// <summary>"Contagem de hoje, começada às 15:10 por Ana" ou "Nova contagem".</summary>
    public string Cabecalho => _contagem is { } contagem
        ? $"Contagem começada {Quando(contagem.IniciadaEm)} por {contagem.UsuarioNome}"
        : "Nova contagem";

    /// <summary>"32 de 123 contados · faltam 91".</summary>
    public string Progresso => _total == 0 ? string.Empty
        : _contados == _total ? $"{_contados} de {_total} contados ✓"
        : $"{_contados} de {_total} contados · faltam {_total - _contados}";

    public bool SemContagem => _contagem is null;

    public string SituacaoEnvio => _contagem?.SituacaoEnvio is { Length: > 0 } situacao && _contagem.RejeitadoMotivo is null ? situacao : string.Empty;

    public bool TemSituacaoEnvio => SituacaoEnvio.Length > 0;

    /// <summary>A contagem em andamento foi recusada pelo servidor: motivo em laranja e a opção de começar outra.</summary>
    public bool Recusada => _contagem?.RejeitadoMotivo is not null;

    public string MotivoRecusa => _contagem?.RejeitadoMotivo is { } motivo ? $"⚠ O sistema recusou esta contagem: {motivo}" : string.Empty;

    /// <summary>Antes de começar: quando foi a última contagem concluída neste tablet (e se ela foi recusada).</summary>
    public string UltimaConcluidaTexto => _ultimaConcluida is not { ConcluidaEm: { } concluidaEm } ultima
        ? "Nenhuma contagem concluída neste tablet ainda. O primeiro número que você marcar começa a contagem."
        : ultima.RejeitadoMotivo is { } motivo
            ? $"⚠ A contagem de {DiaDaLoja.HoraLocal(concluidaEm):dd/MM} foi recusada pelo sistema: {motivo} Faça uma nova contagem."
            : $"Última contagem concluída: {Quando(concluidaEm)} por {ultima.UsuarioNome}. {ultima.SituacaoEnvio}".Trim();

    public bool UltimaRecusada => _ultimaConcluida?.RejeitadoMotivo is not null;

    /// <summary>"Lista de 14/10 15:40" ou o motivo de estar usando a lista guardada no tablet.</summary>
    public string Situacao
    {
        get => _situacao;
        set { _situacao = value; OnPropertyChanged(); }
    }

    public bool Ocupado
    {
        get => _ocupado;
        set { _ocupado = value; OnPropertyChanged(); OnPropertyChanged(nameof(Livre)); }
    }

    public bool Livre => !Ocupado;

    // ---------------------------------------------------------------- abrir a tela

    /// <summary>Ao aparecer: escuta a fila (o "aguardando envio" muda sozinho) e mostra o que está no tablet.</summary>
    public async Task CarregarAsync()
    {
        if (!_ouvindoFila)
        {
            _sync.Mudou += AoMudarFila;
            _ouvindoFila = true;
        }

        await RecarregarDoAparelhoAsync();
        if (Situacao.Length == 0)
        {
            Situacao = _estoque.UltimaAtualizacaoUtc is { } quando
                ? $"Lista de {DiaDaLoja.HoraLocal(quando):dd/MM HH:mm}."
                : "A lista de itens ainda não chegou neste tablet.";
        }

        await PerguntarSobreContagemAntigaAsync();
        _ = AtualizarDoServidorAsync(manual: false); // só se a lista estiver velha; nunca segura a tela
    }

    /// <summary>Ao sair da tela: para de escutar a fila (a tela é criada de novo na próxima vez).</summary>
    public void Parar()
    {
        if (_ouvindoFila)
        {
            _sync.Mudou -= AoMudarFila;
            _ouvindoFila = false;
        }
    }

    private async Task RecarregarDoAparelhoAsync()
    {
        TelaContagem? tela = null;
        if (!await Operacao.CarregarAsync(async () => tela = await _contagens.CarregarAsync()) || tela is null)
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => Montar(tela));
    }

    private void Montar(TelaContagem tela)
    {
        _contagem = tela.EmAndamento;
        _ultimaConcluida = tela.UltimaConcluida;
        _grupoDaLinha.Clear();

        var grupos = new ObservableCollection<GrupoDaContagem>();
        foreach (var itensDoGrupo in tela.Itens.GroupBy(i => i.Item.GrupoId))
        {
            var linhas = itensDoGrupo.Select(i => new LinhaContagem(i)).ToList();
            var nome = itensDoGrupo.First().Item.GrupoNome;
            var grupo = new GrupoDaContagem(string.IsNullOrWhiteSpace(nome) ? "Sem grupo" : nome, linhas);
            foreach (var linha in linhas)
            {
                _grupoDaLinha[linha] = grupo;
            }

            grupos.Add(grupo);
        }

        Grupos = grupos;
        RecontarProgresso();
        NotificarContagem();
    }

    /// <summary>
    /// Contagem começada em outro dia e não concluída (alguém esqueceu): continuar com aqueles números ou começar do zero.
    /// Pergunta uma vez por abertura da tela.
    /// </summary>
    private async Task PerguntarSobreContagemAntigaAsync()
    {
        if (_perguntouSobreAntiga || _contagem is not { } antiga || DiaDaLoja.DataDe(antiga.IniciadaEm) >= DiaDaLoja.Hoje())
        {
            return;
        }

        _perguntouSobreAntiga = true;
        var comecarNova = await Shell.Current.DisplayAlertAsync(
            "Contagem não concluída",
            $"Tem uma contagem começada {Quando(antiga.IniciadaEm)} por {antiga.UsuarioNome}, com {_contados} item(ns) contado(s), " +
            "que não foi concluída.\n\nContinuar essa ou começar uma nova? Se começar uma nova, a antiga fica de lado e não vale.",
            "Começar nova", "Continuar");
        if (!comecarNova)
        {
            return;
        }

        if (await Operacao.GravarAsync(() => _contagens.DescartarAsync(antiga.Id)))
        {
            AvisoRapido.Mostrar("Contagem nova: a lista começa vazia");
            await RecarregarDoAparelhoAsync();
        }
    }

    // ---------------------------------------------------------------- contar

    private async Task Somar(LinhaContagem? linha, int sentido)
    {
        if (linha is null)
        {
            return;
        }

        ContagemLocal? contagem = null;
        decimal? contado = null;
        if (await Operacao.GravarAsync(async () => (contagem, contado) = await _contagens.SomarAsync(_contagem?.Id, linha.Item, sentido))
            && contagem is not null)
        {
            Aplicar(linha, contagem, contado);
        }
    }

    /// <summary>− no campo vazio não faz nada (vazio é "ainda não contado"); no zero também não.</summary>
    private async Task Menos(LinhaContagem? linha)
    {
        if (linha is null)
        {
            return;
        }

        if (linha.Contado is null)
        {
            AvisoRapido.Mostrar("Toque em + ou no número para digitar");
            return;
        }

        if (linha.Contado > 0m)
        {
            await Somar(linha, -1);
        }
    }

    /// <summary>RN-ES-06: "Não tem nenhum" grava 0 em um toque.</summary>
    private async Task NaoTemNenhum(LinhaContagem? linha)
    {
        if (linha is null || linha.Contado == 0m)
        {
            return;
        }

        await Definir(linha, 0m);
    }

    /// <summary>Toque no número: digita ("2,5"). Em branco apaga (volta a "não contado").</summary>
    private async Task Digitar(LinhaContagem? linha)
    {
        if (linha is null)
        {
            return;
        }

        var meia = linha.Passo < 1m;
        var texto = await Shell.Current.DisplayPromptAsync(
            linha.Nome,
            $"{linha.PerguntaQuantidade}{(meia ? " Meia caixa = 0,5." : string.Empty)}\nDeixe em branco para apagar.",
            "OK", "Cancelar",
            placeholder: meia ? "Ex.: 2,5" : "Ex.: 3",
            maxLength: 6,
            keyboard: Keyboard.Telephone,
            initialValue: linha.Contado is { } atual ? LinhaContagem.Formatar(atual) : string.Empty);
        if (texto is null)
        {
            return; // cancelou
        }

        decimal? valor = null;
        if (!string.IsNullOrWhiteSpace(texto))
        {
            if (!TentarLer(texto, out var lido))
            {
                await Shell.Current.DisplayAlertAsync("Quantidade",
                    meia ? "Digite só o número de caixas, de meia em meia (ex.: 2 ou 2,5)." : "Digite só o número, sem vírgula (ex.: 3).", "OK");
                return;
            }

            valor = lido;
        }

        if (valor == linha.Contado)
        {
            return; // nada mudou
        }

        await Definir(linha, valor);
    }

    private async Task Definir(LinhaContagem linha, decimal? valor)
    {
        ContagemLocal? contagem = null;
        decimal? contado = null;
        if (await Operacao.GravarAsync(async () => (contagem, contado) = await _contagens.DefinirAsync(_contagem?.Id, linha.Item, valor))
            && contagem is not null)
        {
            Aplicar(linha, contagem, contado);
        }
    }

    /// <summary>Gravou no tablet: mostra o número, o progresso e a contagem (que nasce no primeiro item contado).</summary>
    private void Aplicar(LinhaContagem linha, ContagemLocal contagem, decimal? contado)
    {
        _contagem = contagem;
        linha.Contado = contado;
        if (_grupoDaLinha.TryGetValue(linha, out var grupo))
        {
            grupo.AtualizarProgresso();
        }

        RecontarProgresso();
        NotificarContagem();
    }

    // ---------------------------------------------------------------- salvar, concluir, começar outra

    /// <summary>Tudo já está gravado no tablet: tenta enviar o parcial e volta. Dá para continuar depois.</summary>
    private async Task SalvarParcial()
    {
        if (Ocupado)
        {
            return;
        }

        if (_contagem is not null)
        {
            _contagens.EnviarParcial();
            AvisoRapido.Mostrar(Connectivity.Current.NetworkAccess == NetworkAccess.Internet
                ? "Contagem salva. Dá para continuar depois."
                : "Contagem salva no tablet. Dá para continuar depois; sobe para o painel quando a internet voltar.");
        }

        await Shell.Current.GoToAsync("..");
    }

    /// <summary>RN-ES-05/06: concluir (depois não muda). Itens não contados ficam "não contado", não viram 0.</summary>
    private async Task Concluir()
    {
        if (Ocupado)
        {
            return;
        }

        if (_contagem is not { } contagem || _contados == 0)
        {
            await Shell.Current.DisplayAlertAsync("Concluir", "Conte pelo menos um item antes de concluir.", "OK");
            return;
        }

        var faltam = _total - _contados;
        var mensagem = faltam > 0
            ? $"{_contados} item(ns) contado(s). {faltam} sem número ficam como \"não contado\" (não viram zero: se acabou, toque em \"Não tem nenhum\").\n\nDepois de concluir não dá para mudar."
            : $"Todos os {_total} itens contados.\n\nDepois de concluir não dá para mudar.";
        if (!await Shell.Current.DisplayAlertAsync("Concluir a contagem?", mensagem, "Concluir", "Voltar"))
        {
            return;
        }

        Ocupado = true;
        try
        {
            ContagemLocal? concluida = null;
            if (!await Operacao.GravarAsync(async () => concluida = await _contagens.ConcluirAsync(contagem.Id)) || concluida is null)
            {
                return;
            }

            AvisoRapido.Mostrar(Connectivity.Current.NetworkAccess == NetworkAccess.Internet
                ? "Contagem concluída. Ela vai para o painel sozinha."
                : "Contagem concluída e guardada no tablet. Sobe para o painel quando a internet voltar.");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>A contagem em andamento foi recusada pelo servidor: deixar de lado e começar com a lista vazia.</summary>
    private async Task ComecarOutra()
    {
        if (Ocupado || _contagem is not { } atual)
        {
            return;
        }

        if (!await Shell.Current.DisplayAlertAsync("Começar outra contagem",
                "Esta contagem fica de lado (não vale) e a lista volta vazia. Começar outra?", "Começar outra", "Voltar"))
        {
            return;
        }

        if (await Operacao.GravarAsync(() => _contagens.DescartarAsync(atual.Id)))
        {
            AvisoRapido.Mostrar("Contagem nova: a lista começa vazia");
            await RecarregarDoAparelhoAsync();
        }
    }

    // ---------------------------------------------------------------- lista do servidor e fila de envio

    /// <summary>
    /// Baixa o catálogo de novo. Sozinho (ao abrir) só se a lista tiver mais de 5 min; no meio da contagem a tela não
    /// pula — a lista nova aparece quando a tela vazia recebe itens ou quando a pessoa toca em Atualizar.
    /// </summary>
    private async Task AtualizarDoServidorAsync(bool manual)
    {
        if (_atualizando)
        {
            return;
        }

        if (!manual && _total > 0 && _estoque.UltimaAtualizacaoUtc is { } ultima && DateTime.UtcNow - ultima < TimeSpan.FromMinutes(5))
        {
            return;
        }

        _atualizando = true;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Situacao = DescreverLista("Sem internet");
                if (manual)
                {
                    AvisoRapido.Mostrar("Sem internet: a contagem fica guardada no tablet e sobe depois");
                }

                return;
            }

            Situacao = "Atualizando a lista… se o servidor estava dormindo, pode levar até 1 minuto.";
            await _estoque.AtualizarAsync();
            Situacao = $"Lista atualizada às {DiaDaLoja.HoraLocal(DateTime.UtcNow):HH:mm}.";
            if (manual || _total == 0)
            {
                await RecarregarDoAparelhoAsync();
            }
        }
        catch (SessaoExpiradaException)
        {
            Situacao = DescreverLista("Sessão vencida (toque em Atualizar para entrar com o PIN)");
            if (manual)
            {
                await View.LoginView.PedirPinDeNovoAsync(); // RN-US-06: ao voltar, a tela carrega de novo
            }
        }
        catch (ApiException ex)
        {
            Situacao = DescreverLista(ex.Message); // sem internet / servidor dormindo: segue com a lista do tablet
        }
        catch (Exception ex)
        {
            // B7: falha ao gravar a lista no aparelho não fica em silêncio
            Debug.WriteLine($"[Sorveteria] Lista da contagem não atualizada: {ex}");
            Situacao = DescreverLista($"Não foi possível atualizar a lista ({ex.Message})");
        }
        finally
        {
            _atualizando = false;
        }
    }

    private string DescreverLista(string motivo) => _estoque.UltimaAtualizacaoUtc is { } quando
        ? $"{motivo.TrimEnd('.')}. Usando a lista guardada no tablet ({DiaDaLoja.HoraLocal(quando):dd/MM HH:mm})."
        : $"{motivo.TrimEnd('.')}. A lista de itens ainda não chegou neste tablet: conecte à internet e toque em Atualizar.";

    /// <summary>A fila andou (enviada, recusada): atualiza o "aguardando envio" e a recusa, sem mexer na lista.</summary>
    private async void AoMudarFila(object? sender, EventArgs e)
    {
        try
        {
            var id = _contagem?.Id;
            var atual = id is { } contagemId ? await _repositorio.ObterAsync(contagemId) : null;
            var ultima = await _repositorio.ObterUltimaConcluidaAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Leitura mais velha que um toque feito enquanto isso não pode apagar o "aguardando envio" da tela.
                if (atual is not null && _contagem is { } naTela && naTela.Id == atual.Id && atual.EstaEmAndamento && atual.Versao >= naTela.Versao)
                {
                    _contagem = atual;
                }

                _ultimaConcluida = ultima;
                NotificarContagem();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Situação da contagem não atualizada: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- apoio

    private void RecontarProgresso()
    {
        var linhas = Grupos.SelectMany(g => g).ToList();
        _total = linhas.Count;
        _contados = linhas.Count(l => l.Contou);
    }

    private void NotificarContagem()
    {
        foreach (var nome in new[]
                 {
                     nameof(Cabecalho), nameof(Progresso), nameof(SemContagem), nameof(SituacaoEnvio), nameof(TemSituacaoEnvio),
                     nameof(Recusada), nameof(MotivoRecusa), nameof(UltimaConcluidaTexto), nameof(UltimaRecusada),
                 })
        {
            OnPropertyChanged(nome);
        }
    }

    /// <summary>"hoje às 15:10" ou "em 13/10 às 18:20", no horário da loja.</summary>
    private static string Quando(DateTime utc)
    {
        var local = DiaDaLoja.HoraLocal(utc);
        return DiaDaLoja.DataDe(utc) == DiaDaLoja.Hoje() ? $"hoje às {local:HH:mm}" : $"em {local:dd/MM} às {local:HH:mm}";
    }

    /// <summary>"2,5", "2.5" ou "3"; sem sinal. Passo, máximo e meia caixa quem confere é o Domain.</summary>
    private static bool TentarLer(string texto, out decimal valor) =>
        decimal.TryParse(texto.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor);
}
