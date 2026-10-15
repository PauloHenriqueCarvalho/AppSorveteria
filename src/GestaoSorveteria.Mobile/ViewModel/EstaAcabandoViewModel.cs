using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.ViewModel;

/// <summary>Item do catálogo na busca da tela "Está acabando". <see cref="JaAvisado"/> preenchido = já tem aviso aberto.</summary>
public sealed record ItemParaAvisar(Guid Id, string Nome, string Grupo, string? JaAvisado)
{
    public bool TemAviso => JaAvisado is not null;
}

/// <summary>Botão de grupo (filtro) da tela "Está acabando", na ordem da planilha.</summary>
public sealed record GrupoParaFiltro(string Nome, bool Selecionado);

/// <summary>
/// Tela "Está acabando" (Fase 2, docs/10 §2.2): <b>o quê</b> (busca em todos os grupos, inclusive copos e insumos, ou "Outro"),
/// <b>quanto tem agora</b> (− número +, ou Acabou), <b>prioridade</b> com significado fixo (RN-AV-02) e observação.
/// Embaixo, os avisos abertos ("Já avisados"). Em até 4 toques: grupo → sabor → Acabou → Avisar.
/// Item já avisado vira "Ainda falta", que só sobe a prioridade (RN-AV-03). Urgente abre o WhatsApp da dona (RN-AV-06).
/// Funciona sem internet: grava no tablet e sobe pela fila.
/// </summary>
public class EstaAcabandoViewModel : BindableObject
{
    private const int MaximoResultados = 60;
    private const string AvisarDeNovo = "Avisar de novo";
    private const string TirarDaLista = "Tirar da lista";

    private readonly AvisoAppService _avisos;
    private readonly AvisoRepository _repositorio;
    private readonly ItemEstoqueRepository _itens;
    private readonly EstoqueService _estoque;

    private List<ItemEstoqueLocal> _catalogo = new();
    private List<AvisoLocal> _abertos = new();
    private string? _grupoFiltro;

    // O que está sendo avisado: item do catálogo ou texto livre; _avisoExistente preenchido = "Ainda falta".
    private ItemEstoqueLocal? _item;
    private string? _textoLivre;
    private AvisoLocal? _avisoExistente;
    private Guid? _substituiRecusadoId;
    private PrioridadeAviso? _prioridade;

    private string _filtroNome = string.Empty;
    private string _quantidadeTexto = "1";
    private string _observacao = string.Empty;
    private string _situacao = string.Empty;
    private string _dicaBusca = string.Empty;
    private bool _ocupado;
    private bool _atualizando;

    public EstaAcabandoViewModel(AvisoAppService avisos, AvisoRepository repositorio, ItemEstoqueRepository itens, EstoqueService estoque)
    {
        _avisos = avisos;
        _repositorio = repositorio;
        _itens = itens;
        _estoque = estoque;

        EscolherGrupoCommand = new Command<GrupoParaFiltro>(EscolherGrupo);
        EscolherItemCommand = new Command<ItemParaAvisar>(EscolherItem);
        OutroCommand = new Command(async () => await EscolherOutro());
        TrocarCommand = new Command(() => Selecionar(null, null, null));
        MenosCommand = new Command(() => MudarQuantidade(-1));
        MaisCommand = new Command(() => MudarQuantidade(+1));
        AcabouCommand = new Command(Acabou);
        EscolherPrioridadeCommand = new Command<string>(EscolherPrioridade);
        SalvarCommand = new Command(async () => await Salvar());
        TocarAvisoCommand = new Command<AvisoLocal>(async aviso => await TocarAviso(aviso));
        AtualizarCommand = new Command(async () => await AtualizarDoServidorAsync(manual: true));
    }

    /// <summary>A tela rola até o formulário (ao tocar num aviso da lista lá embaixo).</summary>
    public event EventHandler? RolarParaOInicio;

    public ICommand EscolherGrupoCommand { get; }
    public ICommand EscolherItemCommand { get; }
    public ICommand OutroCommand { get; }
    public ICommand TrocarCommand { get; }
    public ICommand MenosCommand { get; }
    public ICommand MaisCommand { get; }
    public ICommand AcabouCommand { get; }
    public ICommand EscolherPrioridadeCommand { get; }
    public ICommand SalvarCommand { get; }
    public ICommand TocarAvisoCommand { get; }
    public ICommand AtualizarCommand { get; }

    public ObservableCollection<GrupoParaFiltro> Grupos { get; } = new();
    public ObservableCollection<ItemParaAvisar> Resultados { get; } = new();
    public ObservableCollection<AvisoLocal> Avisos { get; } = new();

    /// <summary>"Lista atualizada às 15:40" ou o motivo de estar usando a lista guardada no tablet.</summary>
    public string Situacao
    {
        get => _situacao;
        set { _situacao = value; OnPropertyChanged(); }
    }

    /// <summary>Busca por nome (sem acento, todas as palavras), em todos os grupos.</summary>
    public string FiltroNome
    {
        get => _filtroNome;
        set
        {
            var novo = value ?? string.Empty;
            if (_filtroNome == novo) return;
            _filtroNome = novo;
            OnPropertyChanged();
            AplicarFiltro(); // filtra enquanto digita
        }
    }

    public string DicaBusca
    {
        get => _dicaBusca;
        set { _dicaBusca = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemDicaBusca)); }
    }

    public bool TemDicaBusca => DicaBusca.Length > 0;

    // ---------------------------------------------------------------- 1. o quê

    public bool TemSelecao => _item is not null || _textoLivre is not null;
    public bool SemSelecao => !TemSelecao;
    public string SelecionadoNome => _item?.Nome ?? _textoLivre ?? string.Empty;
    public string SelecionadoGrupo => _item?.GrupoNome ?? _avisoExistente?.GrupoNome ?? "Outro (escrito à mão)";

    /// <summary>RN-AV-03: "Já avisado às 14:10 por Ana — Acabou · Urgente".</summary>
    public bool TemAvisoExistente => _avisoExistente is not null;
    public string JaAvisadoTexto => _avisoExistente?.JaAvisadoTexto ?? string.Empty;
    public string TextoSalvar => _avisoExistente is null ? "Avisar" : "Ainda falta";

    // ---------------------------------------------------------------- 2. quanto tem agora

    public string DicaQuantidade => _item?.DicaQuantidade ?? "Na menor unidade: picolé, pote, balde ou copo.";

    /// <summary>Número digitado ou pelo − / +. Zero já escolhe Urgente (RN-AV-02).</summary>
    public string QuantidadeTexto
    {
        get => _quantidadeTexto;
        set
        {
            var novo = value ?? string.Empty;
            if (_quantidadeTexto == novo) return;
            _quantidadeTexto = novo;
            OnPropertyChanged();
            if (TentarLerQuantidade(out var quantidade) && quantidade == 0)
            {
                DefinirPrioridade(PrioridadeAviso.Urgente);
            }
        }
    }

    // ---------------------------------------------------------------- 3. prioridade (RN-AV-02/03)

    public bool UrgenteEscolhida => _prioridade == PrioridadeAviso.Urgente;
    public bool AltaEscolhida => _prioridade == PrioridadeAviso.Alta;
    public bool MediaEscolhida => _prioridade == PrioridadeAviso.Media;

    /// <summary>"Ainda falta" não baixa a prioridade: abaixo da atual fica apagado (RN-AV-03).</summary>
    public bool PodeAlta => PodeEscolher(PrioridadeAviso.Alta);
    public bool PodeMedia => PodeEscolher(PrioridadeAviso.Media);

    // ---------------------------------------------------------------- 4. observação e salvar

    public string Observacao
    {
        get => _observacao;
        set { _observacao = value ?? string.Empty; OnPropertyChanged(); }
    }

    public bool Ocupado
    {
        get => _ocupado;
        set { _ocupado = value; OnPropertyChanged(); OnPropertyChanged(nameof(Livre)); }
    }

    public bool Livre => !Ocupado;

    // ---------------------------------------------------------------- já avisados

    public string TituloAvisos => Avisos.Count == 0 ? "Nenhum aviso aberto" : $"Já avisados ({Avisos.Count})";
    public bool TemAvisos => Avisos.Count > 0;

    /// <summary>Ao abrir a tela: mostra o que está no tablet na hora e atualiza do servidor em segundo plano.</summary>
    public async Task CarregarAsync()
    {
        await RecarregarDoAparelhoAsync();
        if (Situacao.Length == 0)
        {
            Situacao = _estoque.UltimaAtualizacaoUtc is { } quando
                ? $"Lista de {quando.ToLocalTime():dd/MM HH:mm}."
                : "A lista de itens ainda não chegou neste tablet.";
        }

        _ = AtualizarDoServidorAsync(manual: false); // nunca segura a tela; sem internet fica a lista do tablet
    }

    private async Task RecarregarDoAparelhoAsync()
    {
        var catalogo = new List<ItemEstoqueLocal>();
        var abertos = new List<AvisoLocal>();
        if (!await Operacao.CarregarAsync(async () =>
            {
                catalogo = await _itens.ListarAtivosAsync();
                abertos = await _repositorio.ListarAbertosAsync();
            }))
        {
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _catalogo = catalogo;
            _abertos = abertos;
            Avisos.Clear();
            foreach (var aviso in abertos)
            {
                Avisos.Add(aviso);
            }

            OnPropertyChanged(nameof(TituloAvisos));
            OnPropertyChanged(nameof(TemAvisos));
            MontarGrupos();
            AplicarFiltro();
            AtualizarAvisoDaSelecao();
        });
    }

    private async Task AtualizarDoServidorAsync(bool manual)
    {
        if (_atualizando) return;
        _atualizando = true;
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Situacao = DescreverCache("Sem internet");
                if (manual)
                {
                    AvisoRapido.Mostrar("Sem internet: os avisos ficam guardados no tablet e sobem depois");
                }

                return;
            }

            Situacao = "Atualizando a lista… se o servidor estava dormindo, pode levar até 1 minuto.";
            await _estoque.AtualizarAsync();
            Situacao = $"Lista atualizada às {DateTime.Now:HH:mm}.";
            await RecarregarDoAparelhoAsync();
        }
        catch (SessaoExpiradaException)
        {
            Situacao = DescreverCache("Sessão vencida (toque em Atualizar para entrar com o PIN)");
            if (manual)
            {
                await View.LoginView.PedirPinDeNovoAsync(); // RN-US-06: ao voltar, a tela atualiza de novo
            }
        }
        catch (ApiException ex)
        {
            Situacao = DescreverCache(ex.Message); // sem internet / servidor dormindo: segue com a lista do tablet
        }
        catch (Exception ex)
        {
            // B7: falha ao gravar a lista no aparelho não fica em silêncio
            Debug.WriteLine($"[Sorveteria] Lista do \"Está acabando\" não atualizada: {ex}");
            Situacao = DescreverCache($"Não foi possível atualizar a lista ({ex.Message})");
        }
        finally
        {
            _atualizando = false;
        }
    }

    private string DescreverCache(string motivo) => _estoque.UltimaAtualizacaoUtc is { } quando
        ? $"{motivo.TrimEnd('.')}. Usando a lista guardada no tablet ({quando.ToLocalTime():dd/MM HH:mm})."
        : $"{motivo.TrimEnd('.')}. A lista de itens ainda não chegou neste tablet: use \"Outro\".";

    // ---------------------------------------------------------------- escolher o quê

    private void MontarGrupos()
    {
        var nomes = _catalogo.Select(i => i.GrupoNome).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (_grupoFiltro is not null && !nomes.Contains(_grupoFiltro))
        {
            _grupoFiltro = null;
        }

        Grupos.Clear();
        foreach (var nome in nomes)
        {
            Grupos.Add(new GrupoParaFiltro(nome, nome == _grupoFiltro));
        }
    }

    private void AplicarFiltro()
    {
        var palavras = SemAcento(FiltroNome).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        IEnumerable<ItemEstoqueLocal> encontrados = palavras.Length > 0
            ? _catalogo.Where(i =>
            {
                var texto = SemAcento(i.Descricao);
                return palavras.All(p => texto.Contains(p, StringComparison.OrdinalIgnoreCase));
            })
            : _grupoFiltro is { } grupo
                ? _catalogo.Where(i => i.GrupoNome == grupo)
                : Enumerable.Empty<ItemEstoqueLocal>();

        var lista = encontrados.Take(MaximoResultados + 1).ToList();
        Resultados.Clear();
        foreach (var item in lista.Take(MaximoResultados))
        {
            var aviso = AvisoAbertoDo(item.Id);
            Resultados.Add(new ItemParaAvisar(item.Id, item.Nome, item.GrupoNome, aviso is null ? null : $"Já avisado · {aviso.QuantidadeTexto}"));
        }

        DicaBusca = _catalogo.Count == 0
            ? "A lista de itens ainda não chegou neste tablet (precisa de internet uma vez). Use \"Outro\" e escreva o que está acabando."
            : palavras.Length == 0 && _grupoFiltro is null ? "Toque no grupo ou digite o nome."
            : lista.Count == 0 ? "Nada encontrado. Use \"Outro\" e escreva o que está acabando."
            : lista.Count > MaximoResultados ? $"Mostrando os {MaximoResultados} primeiros: digite mais do nome."
            : string.Empty;
    }

    private void EscolherGrupo(GrupoParaFiltro? grupo)
    {
        if (grupo is null) return;

        _grupoFiltro = grupo.Nome;
        if (_filtroNome.Length > 0)
        {
            _filtroNome = string.Empty; // a busca por nome passaria na frente do grupo
            OnPropertyChanged(nameof(FiltroNome));
        }

        MontarGrupos();
        AplicarFiltro();
    }

    private void EscolherItem(ItemParaAvisar? opcao)
    {
        var item = opcao is null ? null : _catalogo.FirstOrDefault(i => i.Id == opcao.Id);
        if (item is null) return;

        Selecionar(item, null, AvisoAbertoDo(item.Id)); // RN-AV-03: já avisado → "Ainda falta"
    }

    /// <summary>RN-AV-01: "Outro" com texto livre (o que não está no catálogo).</summary>
    private async Task EscolherOutro()
    {
        var texto = await Shell.Current.DisplayPromptAsync(
            "Outro", "O que está acabando?", "OK", "Cancelar",
            placeholder: "Ex.: tampa do pote 2L", maxLength: 120, initialValue: FiltroNome.Trim());
        if (string.IsNullOrWhiteSpace(texto)) return;

        if (texto.Trim().Length < 2)
        {
            await Shell.Current.DisplayAlertAsync("Atenção", "Escreva pelo menos 2 letras.", "OK");
            return;
        }

        Selecionar(null, texto.Trim(), null);
    }

    /// <summary>Começa um aviso (ou "Ainda falta" em <paramref name="existente"/>); tudo nulo volta para a busca.</summary>
    private void Selecionar(ItemEstoqueLocal? item, string? textoLivre, AvisoLocal? existente, Guid? substituiRecusadoId = null)
    {
        _item = item;
        _textoLivre = textoLivre;
        _avisoExistente = existente;
        _substituiRecusadoId = substituiRecusadoId;
        _quantidadeTexto = (existente?.QuantidadeAtual ?? 1).ToString(CultureInfo.InvariantCulture);
        _prioridade = existente?.Prioridade; // aviso novo: a pessoa escolhe (não vira tudo urgente)
        _observacao = string.Empty;
        OnPropertyChanged(nameof(QuantidadeTexto));
        OnPropertyChanged(nameof(Observacao));
        NotificarSelecao();
    }

    /// <summary>A lista mudou (servidor ou fila): o item escolhido pode ter ganhado (ou perdido) aviso aberto.</summary>
    private void AtualizarAvisoDaSelecao()
    {
        if (!TemSelecao) return;

        _avisoExistente = _item is { } item
            ? AvisoAbertoDo(item.Id)
            : _avisoExistente is { } existente ? _abertos.FirstOrDefault(a => a.Id == existente.Id && !a.EhRecusado) : null;
        if (_avisoExistente is { } aviso && _prioridade is { } escolhida && escolhida < aviso.Prioridade)
        {
            _prioridade = aviso.Prioridade;
        }

        NotificarSelecao();
    }

    private AvisoLocal? AvisoAbertoDo(Guid itemId) =>
        _abertos.Where(a => a.ItemEstoqueId == itemId && !a.EhRecusado).OrderByDescending(a => a.AtualizadoEm).FirstOrDefault();

    private void NotificarSelecao()
    {
        foreach (var nome in new[]
                 {
                     nameof(TemSelecao), nameof(SemSelecao), nameof(SelecionadoNome), nameof(SelecionadoGrupo),
                     nameof(TemAvisoExistente), nameof(JaAvisadoTexto), nameof(TextoSalvar), nameof(DicaQuantidade),
                     nameof(UrgenteEscolhida), nameof(AltaEscolhida), nameof(MediaEscolhida), nameof(PodeAlta), nameof(PodeMedia),
                 })
        {
            OnPropertyChanged(nome);
        }
    }

    // ---------------------------------------------------------------- quantidade e prioridade

    private void MudarQuantidade(int delta)
    {
        var atual = TentarLerQuantidade(out var lida) ? lida : 0;
        QuantidadeTexto = Math.Clamp(atual + delta, 0, Aviso.QuantidadeMaxima).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>RN-AV-02: acabou = 0 e Urgente.</summary>
    private void Acabou()
    {
        QuantidadeTexto = "0";
        DefinirPrioridade(PrioridadeAviso.Urgente);
    }

    private void EscolherPrioridade(string? nome)
    {
        if (!Enum.TryParse<PrioridadeAviso>(nome, out var prioridade)) return;

        if (!PodeEscolher(prioridade))
        {
            AvisoRapido.Mostrar($"\"Ainda falta\" não baixa a prioridade (está {_avisoExistente?.PrioridadeNome})");
            return;
        }

        DefinirPrioridade(prioridade);
    }

    private void DefinirPrioridade(PrioridadeAviso prioridade)
    {
        _prioridade = prioridade;
        OnPropertyChanged(nameof(UrgenteEscolhida));
        OnPropertyChanged(nameof(AltaEscolhida));
        OnPropertyChanged(nameof(MediaEscolhida));
    }

    private bool PodeEscolher(PrioridadeAviso prioridade) => _avisoExistente is null || prioridade >= _avisoExistente.Prioridade;

    private bool TentarLerQuantidade(out int quantidade) =>
        int.TryParse(QuantidadeTexto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out quantidade)
        && quantidade <= Aviso.QuantidadeMaxima;

    // ---------------------------------------------------------------- salvar

    private async Task Salvar()
    {
        if (Ocupado || !TemSelecao) return;

        if (!TentarLerQuantidade(out var quantidade))
        {
            await Shell.Current.DisplayAlertAsync("Quanto tem agora?",
                $"Digite um número de 0 a {Aviso.QuantidadeMaxima}. Se não tem nenhum, toque em Acabou.", "OK");
            return;
        }

        if (_prioridade is not { } prioridade)
        {
            await Shell.Current.DisplayAlertAsync("Prioridade", "Escolha a prioridade: Urgente, Alta ou Média.", "OK");
            return;
        }

        Ocupado = true;
        try
        {
            var observacao = string.IsNullOrWhiteSpace(Observacao) ? null : Observacao.Trim();
            var existente = _avisoExistente;
            var item = _item;
            var textoLivre = _textoLivre ?? string.Empty;
            var substitui = _substituiRecusadoId;

            // B7: gravação que falha mostra o motivo e não segue (nem abre o WhatsApp)
            AvisoSalvo? salvo = null;
            if (!await Operacao.GravarAsync(async () => salvo = existente is not null
                    ? await _avisos.AindaFaltaAsync(existente.Id, quantidade, prioridade, observacao, substitui)
                    : item is not null
                        ? await _avisos.AvisarItemAsync(item.Id, quantidade, prioridade, observacao, substitui)
                        : await _avisos.AvisarOutroAsync(textoLivre, quantidade, prioridade, observacao, substitui))
                || salvo is null)
            {
                return;
            }

            // Pronto para o próximo aviso
            Selecionar(null, null, null);
            _grupoFiltro = null;
            FiltroNome = string.Empty;
            await RecarregarDoAparelhoAsync();
            RolarParaOInicio?.Invoke(this, EventArgs.Empty);

            if (salvo.FicouUrgente && salvo.Link is { } link && salvo.Mensagem is { } mensagem)
            {
                await EnviarUrgenteAsync(salvo.Linha.Id, link, mensagem);
            }
            else
            {
                AvisoRapido.Mostrar(salvo.JaExistia ? "Aviso atualizado" : "Aviso salvo"); // B11: sem toque extra
            }
        }
        finally
        {
            Ocupado = false;
        }
    }

    /// <summary>RN-AV-06/08: abre o WhatsApp com a mensagem pronta; se abriu, anota (nunca "entregue").</summary>
    private async Task EnviarUrgenteAsync(Guid avisoId, string link, string mensagem)
    {
        if (!await WhatsApp.AbrirAsync(link, mensagem))
        {
            return; // a mensagem já apareceu na tela com a opção de copiar; o aviso está salvo e sobe pela fila
        }

        AvisoRapido.Mostrar(_estoque.WhatsAppDona is null
            ? "Urgente salvo. Escolha o contato da dona no WhatsApp e toque em enviar."
            : "Urgente salvo. No WhatsApp, toque em enviar.");
        await Operacao.GravarAsync(() => _avisos.MarcarWhatsAppAbertoAsync(avisoId));
        await RecarregarDoAparelhoAsync();
    }

    // ---------------------------------------------------------------- lista "Já avisados"

    /// <summary>Aviso aberto: "Ainda falta" nele. Recusado (laranja): avisar de novo ou tirar da lista.</summary>
    private async Task TocarAviso(AvisoLocal? aviso)
    {
        if (aviso is null || Ocupado) return;

        var item = aviso.ItemEstoqueId is { } itemId ? _catalogo.FirstOrDefault(i => i.Id == itemId) : null;
        if (!aviso.EhRecusado)
        {
            Selecionar(item, item is null ? aviso.TextoLivre ?? aviso.Descricao : null, aviso);
            RolarParaOInicio?.Invoke(this, EventArgs.Empty);
            return;
        }

        // O motivo já aparece no aviso em laranja; aqui a pessoa decide o que fazer.
        var escolha = await Shell.Current.DisplayActionSheetAsync($"{aviso.Descricao} — recusado", "Voltar", null, AvisarDeNovo, TirarDaLista);
        if (escolha == AvisarDeNovo)
        {
            if (item is not null)
            {
                Selecionar(item, null, AvisoAbertoDo(item.Id), aviso.Id);
            }
            else
            {
                Selecionar(null, aviso.TextoLivre ?? aviso.Descricao, null, aviso.Id);
            }

            RolarParaOInicio?.Invoke(this, EventArgs.Empty);
        }
        else if (escolha == TirarDaLista)
        {
            if (await Operacao.GravarAsync(() => _avisos.DispensarRecusadoAsync(aviso.Id)))
            {
                AvisoRapido.Mostrar("Aviso tirado da lista");
                await RecarregarDoAparelhoAsync();
            }
        }
    }

    /// <summary>"picole coco" encontra "Picolé ao leite Coco preto".</summary>
    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(c);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
