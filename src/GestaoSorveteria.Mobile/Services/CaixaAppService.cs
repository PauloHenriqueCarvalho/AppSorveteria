using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Common;
using SorveteriaMaui.Data;
using SorveteriaMaui.Services.Api;

namespace SorveteriaMaui.Services;

/// <summary>Números do caixa aberto para a tela e para a conferência (RN-CX-06), calculados pelo Domain.</summary>
public sealed record ResumoCaixa(
    Guid CaixaId,
    DateTime AbertoEm,
    decimal FundoTroco,
    decimal VendasDinheiro,
    decimal TotalVendido,
    int ComandasFechadas,
    int ComandasAbertas,
    decimal Suprimentos,
    decimal Sangrias,
    decimal Esperado);

/// <summary>
/// Casos de uso do caixa no aparelho (C3): abrir com fundo de troco, sangria/suprimento, fechar com conferência.
/// Carrega do SQLite → chama o <see cref="Caixa"/> do Domain (mesmo código da API) → grava numa transação.
/// </summary>
public sealed class CaixaAppService(CaixaRepository caixas, ComandaRepository comandas, SessaoUsuario sessao, ApiCliente api, SyncService sync)
{
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    public Task<Caixa?> ObterAbertoAsync() => caixas.ObterAbertoAsync();

    /// <summary>Só a linha (sem remontar movimentos): para a tela saber se há caixa e desde quando.</summary>
    public Task<Model.Caixa?> ObterAbertoLinhaAsync() => caixas.ObterAbertoLinhaAsync();

    /// <summary>
    /// RN-CX-02 / RN-SY-04 / RN-CX-01: a abertura é enviada ao servidor (<c>POST /api/sync/caixas</c>) antes de
    /// ser gravada no aparelho — é o servidor que garante um só caixa aberto; se já houver outro, recusa com o motivo.
    /// Sem conexão não abre; depois de aberto, a venda funciona offline.
    /// </summary>
    public Task<Caixa> AbrirAsync(decimal fundoTroco) =>
        UmaPorVezAsync(async () =>
        {
            if (await caixas.ObterAbertoLinhaAsync() is not null)
            {
                throw new DomainException("Já existe um caixa aberto. Feche-o antes de abrir outro (RN-CX-01).");
            }

            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                throw new DomainException("Para abrir o caixa é preciso internet: o servidor garante que há um caixa só. Depois de aberto, a venda funciona sem internet.");
            }

            // O fechamento do caixa anterior pode estar na fila (feito sem internet ou com o servidor acordando).
            // Sem ele, o servidor ainda vê o caixa anterior aberto e recusa a abertura: envia a fila primeiro.
            await sync.SincronizarAgoraAsync();
            if (await caixas.ObterFechadoComEnvioPendenteAsync() is { } anterior)
            {
                if ((await sync.EstadoAsync()).PrecisaPin)
                {
                    throw new DomainException("Sua sessão venceu e o fechamento do caixa anterior não foi enviado. Saia e entre de novo com o PIN, depois abra o caixa.");
                }

                throw new DomainException(
                    $"O fechamento do caixa anterior ({anterior.FechadoEm?.ToLocalTime():dd/MM 'às' HH:mm}) ainda não chegou ao servidor. " +
                    "Confira a internet e tente abrir de novo em 1 minuto (o servidor pode estar acordando).");
            }

            var caixa = Caixa.Abrir(UsuarioId(), fundoTroco, DateTime.UtcNow);
            var dto = new CaixaSyncDto(caixa.Id, caixa.AbertoPorUsuarioId, caixa.AbertoEm, caixa.FundoTroco, Movimentos: [], Fechamento: null);

            SyncResponse resposta;
            try
            {
                resposta = await api.EnviarCaixasAsync(new SyncCaixasRequest([dto]));
            }
            catch (SessaoExpiradaException)
            {
                throw new DomainException("Sua sessão venceu. Saia e entre de novo com o PIN para abrir o caixa.");
            }
            catch (ApiIndisponivelException ex)
            {
                throw new DomainException($"Não deu para confirmar a abertura com o servidor. {ex.Message}");
            }

            var resultado = resposta.Resultados.FirstOrDefault(r => r.Id == caixa.Id);
            if (resultado is null || resultado.Status == StatusSync.Rejeitada)
            {
                // Ex.: outro caixa aberto no servidor (outro aparelho, ou dados do app limpos) — a dona fecha pelo painel (RN-CX-09)
                throw new DomainException(resultado?.Motivo ?? "O servidor não confirmou a abertura do caixa. Tente de novo.");
            }

            await caixas.GravarAsync(caixa, pendenteEnvio: false); // já está no servidor
            return caixa;
        });

    /// <summary>RN-CX-04.</summary>
    public Task<Caixa> RegistrarSangriaAsync(decimal valor, string motivo) =>
        AlterarAbertoAsync(caixa => caixa.RegistrarSangria(valor, motivo, UsuarioId(), DateTime.UtcNow));

    /// <summary>RN-CX-04.</summary>
    public Task<Caixa> RegistrarSuprimentoAsync(decimal valor, string motivo) =>
        AlterarAbertoAsync(caixa => caixa.RegistrarSuprimento(valor, motivo, UsuarioId(), DateTime.UtcNow));

    /// <summary>Números do caixa aberto (ou <c>null</c> sem caixa). Esperado vem de <see cref="Caixa.CalcularEsperado"/>.</summary>
    public async Task<ResumoCaixa?> ResumoAsync()
    {
        var caixa = await caixas.ObterAbertoAsync();
        if (caixa is null)
        {
            return null;
        }

        var vendas = await comandas.ResumoVendasNoCaixaAsync(caixa.Id);
        return new ResumoCaixa(
            caixa.Id,
            caixa.AbertoEm,
            caixa.FundoTroco,
            vendas.TotalDinheiro,
            vendas.TotalVendido,
            vendas.ComandasFechadas,
            vendas.ComandasAbertas,
            caixa.TotalSuprimentos,
            caixa.TotalSangrias,
            caixa.CalcularEsperado(vendas.TotalDinheiro));
    }

    /// <summary>
    /// RN-CX-05/06: sem comanda aberta; esperado = fundo + vendas em dinheiro + suprimentos − sangrias;
    /// diferença = contado − esperado. Envio pendente não impede (RN-SY-05); sem internet fecha e sobe depois (RN-SY-04).
    /// </summary>
    public Task<Caixa> FecharAsync(decimal valorContado, string? observacao) =>
        AlterarAbertoAsync(async caixa =>
        {
            var vendas = await comandas.ResumoVendasNoCaixaAsync(caixa.Id);
            if (vendas.ComandasAbertas > 0)
            {
                throw new DomainException(
                    $"Ainda há {vendas.ComandasAbertas} comanda(s) aberta(s). Feche ou cancele todas antes de fechar o caixa (RN-CX-05).");
            }

            caixa.Fechar(valorContado, vendas.TotalDinheiro, UsuarioId(), DateTime.UtcNow,
                string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim());
        });

    private Guid UsuarioId() =>
        sessao.UsuarioId ?? throw new DomainException("Nenhum atendente conectado. Entre com o PIN para operar o caixa.");

    private Task<Caixa> AlterarAbertoAsync(Action<Caixa> alteracao) =>
        AlterarAbertoAsync(caixa =>
        {
            alteracao(caixa);
            return Task.CompletedTask;
        });

    private Task<Caixa> AlterarAbertoAsync(Func<Caixa, Task> alteracao) =>
        UmaPorVezAsync(async () =>
        {
            var caixa = await caixas.ObterAbertoAsync()
                ?? throw new DomainException("Não há caixa aberto. Abra o caixa na aba Caixa (RN-CX-03).");
            await alteracao(caixa);
            await caixas.GravarAsync(caixa);
            sync.Disparar(); // movimento ou fechamento: sobe em seguida (sem internet, fica na fila — RN-SY-04)
            return caixa;
        });

    private async Task<T> UmaPorVezAsync<T>(Func<Task<T>> operacao)
    {
        await _umaPorVez.WaitAsync();
        try
        {
            return await operacao();
        }
        finally
        {
            _umaPorVez.Release();
        }
    }
}
