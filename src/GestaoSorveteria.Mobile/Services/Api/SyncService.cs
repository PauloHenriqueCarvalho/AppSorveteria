using System.Diagnostics;
using GestaoSorveteria.Contracts.Sync;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;

namespace SorveteriaMaui.Services.Api;

/// <summary>Situação da fila para a tela (indicador "X vendas aguardando envio", RN-SY-05).</summary>
public sealed record EstadoSync(
    int ComandasPendentes,
    int CaixasPendentes,
    int Rejeitadas,
    bool Sincronizando,
    bool PrecisaPin,
    string UltimaMensagem,
    DateTime? UltimoEnvioUtc)
{
    public int Pendentes => ComandasPendentes + CaixasPendentes;
}

/// <summary>
/// Sincronização local-first (C4, docs/03 §8, skill <c>sincronizacao-local-first</c>). Envia os documentos
/// prontos — caixas e comandas fechadas/canceladas — para <c>POST /api/sync/*</c>, idempotente pelo Id (RN-SY-02),
/// na ordem caixa aberto → comandas do caixa → caixa fechado (RN-SY-03). Dispara ao fechar/cancelar comanda, ao mexer
/// no caixa, a cada 1 min e ao abrir o app. Falha de rede pausa sem descartar nada; <c>rejeitada</c> fica visível com o motivo.
/// A venda nunca espera este serviço.
/// </summary>
public sealed class SyncService(ApiCliente api, ComandaRepository comandas, CaixaRepository caixas, SessaoUsuario sessao)
{
    public static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _umaPorVez = new(1, 1);
    private bool _sincronizando;
    private bool _precisaPin;
    private string _ultimaMensagem = string.Empty;
    private DateTime? _ultimoEnvioUtc;
    private CancellationTokenSource? _ciclo;

    /// <summary>Avisa as telas que a fila mudou (contagem, mensagem, estado).</summary>
    public event EventHandler? Mudou;

    /// <summary>Tarefa extra do ciclo de 1 min (atualização do catálogo), ligada na composição do app.</summary>
    public Func<Task>? CatalogoNoCiclo { get; set; }

    public async Task<EstadoSync> EstadoAsync() => new(
        await comandas.ContarPendentesEnvioAsync(),
        await caixas.ContarPendentesEnvioAsync(),
        await comandas.ContarRejeitadasAsync(),
        _sincronizando,
        _precisaPin,
        _ultimaMensagem,
        _ultimoEnvioUtc);

    /// <summary>Liga o ciclo de 1 min (RN-SY-03). Chamado uma vez, ao abrir o app; idempotente.</summary>
    public void IniciarCiclo()
    {
        if (_ciclo is not null)
        {
            return;
        }

        _ciclo = new CancellationTokenSource();
        var token = _ciclo.Token;
        _ = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(Intervalo);
            try
            {
                while (await timer.WaitForNextTickAsync(token))
                {
                    await SincronizarAsync(silencioso: true);
                    if (CatalogoNoCiclo is { } catalogo)
                    {
                        await catalogo(); // C2: preço mudado no painel chega ao balcão sem reabrir o app
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    /// <summary>Dispara um envio em segundo plano (depois de fechar/cancelar comanda, mexer no caixa, abrir o app).</summary>
    public void Disparar() => _ = SincronizarAsync(silencioso: true);

    /// <summary>
    /// Envia tudo que está pendente. Devolve <c>false</c> se parou por rede/servidor/sessão (nada foi descartado).
    /// Uma execução por vez: chamada concorrente volta na hora.
    /// </summary>
    public async Task<bool> SincronizarAsync(bool silencioso = false)
    {
        if (!await _umaPorVez.WaitAsync(0))
        {
            return false;
        }

        try
        {
            if (sessao.UsuarioId is null)
            {
                return false; // ninguém logado: nada a enviar com nome de ninguém
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                Mensagem("Sem internet — as vendas sobem quando a conexão voltar.");
                return false;
            }

            _sincronizando = true;
            Mudou?.Invoke(this, EventArgs.Empty);

            var completo = await EnviarTudoAsync();
            if (completo)
            {
                _precisaPin = false;
                _ultimoEnvioUtc = DateTime.UtcNow;
                Mensagem(await comandas.ContarRejeitadasAsync() > 0
                    ? "Tudo enviado; há venda rejeitada pelo servidor (veja o histórico do turno)."
                    : "Tudo enviado.");
            }

            return completo;
        }
        catch (SessaoExpiradaException)
        {
            _precisaPin = true;
            Mensagem("Sessão vencida — toque para entrar com o PIN e enviar as vendas.");
            return false;
        }
        catch (ApiIndisponivelException ex)
        {
            Mensagem(ex.Message);
            return false;
        }
        catch (ApiException ex)
        {
            // 400 do lote inteiro (ex.: mais de 100) ou outro erro de contrato: não deve acontecer; fica visível (B7)
            Debug.WriteLine($"[Sorveteria] Sync recusado: {ex}");
            Mensagem($"O servidor recusou o envio: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Falha na sincronização: {ex}");
            Mensagem($"Falha ao sincronizar: {ex.Message}");
            return false;
        }
        finally
        {
            _sincronizando = false;
            _umaPorVez.Release();
            Mudou?.Invoke(this, EventArgs.Empty);
            if (!silencioso && _ultimaMensagem.Length > 0)
            {
                AvisoRapido.Mostrar(_ultimaMensagem);
            }
        }
    }

    /// <summary>RN-SY-03: por caixa (mais antigo primeiro): abertura/movimentos → comandas → fechamento.</summary>
    private async Task<bool> EnviarTudoAsync()
    {
        var pendentes = await caixas.ListarComPendenciaAsync(await comandas.ListarCaixasComPendenteEnvioAsync());
        foreach (var caixa in pendentes.OrderBy(c => c.AbertoEm))
        {
            // 1. Caixa (sem fechamento): garante que ele existe no servidor antes das comandas; movimentos novos vão junto.
            if (caixa.PendenteEnvio)
            {
                if (await EnviarCaixaAsync(caixa, comFechamento: false) is null)
                {
                    continue; // rejeitado (motivo gravado): as comandas dele seriam rejeitadas também; próximo caixa
                }
            }

            // 2. Comandas fechadas/canceladas do caixa, em lotes de até 100.
            while (true)
            {
                var lote = await comandas.ListarPendentesEnvioAsync(caixa.Id, LimitesSync.MaximoPorLote);
                if (lote.Count == 0)
                {
                    break;
                }

                var resposta = await api.EnviarComandasAsync(new SyncComandasRequest(
                    await Task.WhenAll(lote.Select(ParaDtoAsync))));
                await AplicarResultadosComandasAsync(lote, resposta);
            }

            // 3. Fechamento, só quando não resta comanda do caixa aguardando (RN-CX-10: o servidor soma as já recebidas).
            if (!caixa.EstaAberto && caixa.PendenteEnvio)
            {
                if (await EnviarCaixaAsync(caixa, comFechamento: true) is true)
                {
                    await caixas.MarcarEnviadoAsync(caixa.Id);
                }
            }
            else if (caixa.PendenteEnvio)
            {
                await caixas.MarcarEnviadoAsync(caixa.Id);
            }
        }

        return true;
    }

    /// <summary><c>true</c> aceito/já recebido, <c>null</c> rejeitado (motivo gravado). Rede/sessão lançam.</summary>
    private async Task<bool?> EnviarCaixaAsync(Caixa caixa, bool comFechamento)
    {
        var movimentos = await caixas.ListarMovimentosAsync(caixa.Id);
        var dto = new CaixaSyncDto(
            caixa.Id,
            caixa.AbertoPorUsuarioId,
            caixa.AbertoEm,
            caixa.FundoTroco,
            movimentos.Select(m => new MovimentoCaixaSyncDto(m.Id, m.Tipo.ToString(), m.Valor, m.Motivo, m.UsuarioId, m.Em)).ToList(),
            comFechamento && !caixa.EstaAberto
                ? new FechamentoCaixaSyncDto(
                    caixa.FechadoPorUsuarioId!.Value, caixa.FechadoEm!.Value, caixa.ValorContado!.Value,
                    caixa.TotalVendasDinheiro!.Value, caixa.ValorEsperado!.Value, caixa.Diferenca!.Value, caixa.Observacao)
                : null);

        var resposta = await api.EnviarCaixasAsync(new SyncCaixasRequest([dto]));
        var resultado = resposta.Resultados.FirstOrDefault(r => r.Id == caixa.Id);
        if (resultado is null || resultado.Status == StatusSync.Rejeitada)
        {
            await caixas.MarcarRejeitadoAsync(caixa.Id, resultado?.Motivo ?? "O servidor não devolveu resultado para este caixa.");
            return null;
        }

        return true;
    }

    private async Task AplicarResultadosComandasAsync(IReadOnlyList<Comanda> lote, SyncResponse resposta)
    {
        var porId = resposta.Resultados.ToDictionary(r => r.Id);
        foreach (var comanda in lote)
        {
            if (!porId.TryGetValue(comanda.Id, out var resultado))
            {
                await comandas.RegistrarFalhaEnvioAsync(comanda.Id, "O servidor não devolveu resultado para esta comanda.");
                continue;
            }

            if (resultado.Status == StatusSync.Rejeitada)
            {
                await comandas.MarcarRejeitadaAsync(comanda.Id, resultado.Motivo ?? "Rejeitada pelo servidor sem motivo.");
            }
            else
            {
                await comandas.MarcarEnviadaAsync(comanda.Id); // aceita ou ja_recebida
            }
        }
    }

    private async Task<ComandaSyncDto> ParaDtoAsync(Comanda c)
    {
        var itens = await comandas.ListarItensAsync(c.Id);
        var pagamentos = await comandas.ListarPagamentosAsync(c.Id);
        return new ComandaSyncDto(
            c.Id, c.CaixaId, c.UsuarioId, c.Numero, c.Tipo.ToString(), c.Status.ToString(), c.Total, c.CriadaEm,
            itens.Select(i => new ItemComandaSyncDto(i.Id, i.ProdutoId, i.Descricao, i.Quantidade, i.PrecoUnitario, i.Subtotal)).ToList(),
            pagamentos.Select(p => new PagamentoSyncDto(p.Id, p.Forma.ToString(), p.Valor, p.ValorRecebido, p.Troco)).ToList(),
            c.Observacao, c.FechadaEm, c.CanceladaEm, c.MotivoCancelamento);
    }

    private void Mensagem(string texto)
    {
        _ultimaMensagem = texto;
        Debug.WriteLine($"[Sorveteria] Sync: {texto}");
    }
}
