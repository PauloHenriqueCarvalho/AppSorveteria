using System.Collections.ObjectModel;
using System.Windows.Input;
using FormaPagamento = GestaoSorveteria.Domain.Comandas.FormaPagamento;
using StatusComanda = GestaoSorveteria.Domain.Comandas.StatusComanda;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Aba Turno (C5): comandas pagas e canceladas do caixa aberto (ou do último caixa fechado), mais recentes
/// primeiro, com a situação do envio de cada uma. Rejeitada nunca some em silêncio (RN-SY-03): fica em destaque
/// e o toque mostra o motivo. Só lê: nada aqui grava venda.
/// </summary>
public class HistoricoTurnoViewModel : BindableObject
{
    private readonly ComandaRepository _comandas;
    private readonly CaixaRepository _caixas;
    private readonly SyncService _sync;

    private string _titulo = string.Empty;
    private string _resumo = string.Empty;
    private string _indicadorEnvio = string.Empty;
    private bool _vazio;
    private bool _atualizando;

    public HistoricoTurnoViewModel(ComandaRepository comandas, CaixaRepository caixas, SyncService sync)
    {
        _comandas = comandas;
        _caixas = caixas;
        _sync = sync;
        EnviarAgoraCommand = new Command(async () => await EnviarAgora());
        VerComandaCommand = new Command<Comanda>(async c => await VerComanda(c));
    }

    public ObservableCollection<Comanda> Comandas { get; } = new();

    public ICommand EnviarAgoraCommand { get; }
    public ICommand VerComandaCommand { get; }

    /// <summary>"Caixa aberto desde 08:02" ou "Último caixa (fechado em 13/10 18:40)".</summary>
    public string Titulo
    {
        get => _titulo;
        set { _titulo = value; OnPropertyChanged(); }
    }

    /// <summary>Contagens e total pago do turno (soma das comandas pagas, já calculadas pelo Domain).</summary>
    public string Resumo
    {
        get => _resumo;
        set { _resumo = value; OnPropertyChanged(); }
    }

    public string IndicadorEnvio
    {
        get => _indicadorEnvio;
        set { _indicadorEnvio = value; OnPropertyChanged(); }
    }

    public bool Vazio
    {
        get => _vazio;
        set { _vazio = value; OnPropertyChanged(); }
    }

    /// <summary>Puxar para atualizar: envia a fila e recarrega; desliga o indicador no fim.</summary>
    public bool Atualizando
    {
        get => _atualizando;
        set { _atualizando = value; OnPropertyChanged(); }
    }

    /// <summary>A tela liga ao aparecer e desliga ao sair, para a lista acompanhar a fila sem vazar o ViewModel.</summary>
    public void Acompanhar(bool ligado)
    {
        if (ligado)
        {
            _sync.Mudou += AoMudarFila;
        }
        else
        {
            _sync.Mudou -= AoMudarFila;
        }
    }

    private async void AoMudarFila(object? sender, EventArgs e) => await Carregar();

    public async Task Carregar()
    {
        await Operacao.CarregarAsync(async () =>
        {
            var aberto = await _caixas.ObterAbertoLinhaAsync();
            var caixa = aberto ?? await _caixas.ObterUltimoFechadoAsync();
            var doCaixa = caixa is null ? new List<Comanda>() : await _comandas.ListarEncerradasDoCaixaAsync(caixa.Id);
            // RN-SY-03: rejeitada de um caixa anterior não some quando abre o próximo; fica no topo.
            var rejeitadasAntigas = (await _comandas.ListarRejeitadasAsync()).Where(r => doCaixa.All(c => c.Id != r.Id));
            var lista = rejeitadasAntigas.Concat(doCaixa).ToList();
            var estado = await _sync.EstadoAsync();

            var pagas = doCaixa.Where(c => c.Status == StatusComanda.Fechada).ToList();
            var canceladas = doCaixa.Count(c => c.Status == StatusComanda.Cancelada);

            var titulo = caixa is null ? "Nenhum caixa ainda"
                : aberto is not null ? $"Caixa aberto desde {aberto.AbertoEm.ToLocalTime():dd/MM HH:mm}"
                : $"Último caixa (fechado em {caixa.FechadoEm?.ToLocalTime():dd/MM HH:mm})";
            var resumo = caixa is null ? "Abra o caixa na aba Caixa para começar a vender."
                : $"{pagas.Count} paga(s) · R$ {pagas.Sum(c => c.Total):N2}" +
                  (canceladas > 0 ? $" · {canceladas} cancelada(s)" : string.Empty);
            // Mesmos textos do indicador da aba Comandas (fila inteira, não só este caixa).
            var indicador = estado.Sincronizando ? "⏳ Enviando…"
                : estado.PrecisaPin ? "🔒 Sessão vencida — toque para entrar e enviar"
                : estado.Rejeitadas > 0 ? $"⚠ {estado.Rejeitadas} venda(s) rejeitada(s) — toque na venda em laranja para ver o motivo"
                : estado.Pendentes > 0 ? $"📤 {estado.ComandasPendentes} venda(s) e {estado.CaixasPendentes} caixa(s) aguardando envio — toque para enviar"
                : lista.Count > 0 ? "✅ Tudo enviado para o painel"
                : string.Empty;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Comandas.Clear();
                foreach (var c in lista)
                {
                    Comandas.Add(c);
                }

                Titulo = titulo;
                Resumo = resumo;
                IndicadorEnvio = indicador;
                Vazio = lista.Count == 0;
            });
        });
    }

    private async Task EnviarAgora()
    {
        try
        {
            // B7: nenhuma falha silenciosa nem exceção solta no comando.
            await Operacao.CarregarAsync(async () =>
            {
                if ((await _sync.EstadoAsync()).PrecisaPin)
                {
                    await View.LoginView.PedirPinDeNovoAsync(); // RN-US-06: a fila espera o PIN
                    return;
                }

                await _sync.SincronizarAsync(silencioso: false);
            });
            await Carregar();
        }
        finally
        {
            Atualizando = false;
        }
    }

    /// <summary>Detalhe curto da venda: itens, pagamentos e situação do envio (motivo, se rejeitada).</summary>
    private async Task VerComanda(Comanda? comanda)
    {
        if (comanda is null)
        {
            return;
        }

        string texto = string.Empty;
        if (!await Operacao.CarregarAsync(async () =>
            {
                var itens = await _comandas.ListarItensAsync(comanda.Id);
                var pagamentos = await _comandas.ListarPagamentosAsync(comanda.Id);
                var linhas = itens.Select(i => $"{i.Quantidade}× {i.Descricao} — R$ {i.Subtotal:N2}").ToList();
                if (pagamentos.Count > 0)
                {
                    linhas.Add(string.Empty);
                    linhas.AddRange(pagamentos.Select(p => p.Troco > 0
                        ? $"{NomeForma(p.Forma)}: R$ {p.Valor:N2} (recebido R$ {p.ValorRecebido:N2}, troco R$ {p.Troco:N2})"
                        : $"{NomeForma(p.Forma)}: R$ {p.Valor:N2}"));
                }

                if (comanda.EhCancelada && !string.IsNullOrWhiteSpace(comanda.MotivoCancelamento))
                {
                    linhas.Add(string.Empty);
                    linhas.Add($"Motivo do cancelamento: {comanda.MotivoCancelamento}");
                }

                linhas.Add(string.Empty);
                linhas.Add($"Envio: {comanda.SituacaoEnvio}");
                if (comanda.EhRejeitada)
                {
                    linhas.Add("A venda continua guardada no tablet. Mostre esta mensagem para a dona conferir no painel.");
                }
                else if (comanda.PendenteEnvio && !string.IsNullOrWhiteSpace(comanda.UltimoErroEnvio))
                {
                    linhas.Add($"Última tentativa: {comanda.UltimoErroEnvio}");
                }

                texto = string.Join('\n', linhas);
            }))
        {
            return;
        }

        await Shell.Current.DisplayAlertAsync(
            $"Comanda {comanda.Numero} · {comanda.StatusTexto} · R$ {comanda.Total:N2}", texto, "OK");
    }

    private static string NomeForma(FormaPagamento forma) => forma switch
    {
        FormaPagamento.Dinheiro => "Dinheiro",
        FormaPagamento.Pix => "Pix",
        FormaPagamento.CartaoDebito => "Cartão débito",
        FormaPagamento.CartaoCredito => "Cartão crédito",
        _ => forma.ToString(),
    };
}
