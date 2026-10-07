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
public sealed class CaixaAppService(CaixaRepository caixas, ComandaRepository comandas, SessaoUsuario sessao)
{
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    public Task<Caixa?> ObterAbertoAsync() => caixas.ObterAbertoAsync();

    /// <summary>
    /// RN-CX-02 / RN-SY-04: abrir exige internet (garante um só caixa aberto quando o C4 enviar a abertura);
    /// depois disso a venda funciona offline.
    /// </summary>
    public Task<Caixa> AbrirAsync(decimal fundoTroco) =>
        UmaPorVezAsync(async () =>
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                throw new DomainException("Para abrir o caixa é preciso internet (garante um caixa só). Depois de aberto, a venda funciona sem internet.");
            }

            if (await caixas.ObterAbertoLinhaAsync() is not null)
            {
                throw new DomainException("Já existe um caixa aberto. Feche-o antes de abrir outro (RN-CX-01).");
            }

            var caixa = Caixa.Abrir(UsuarioId(), fundoTroco, DateTime.UtcNow);
            await caixas.GravarAsync(caixa);
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
        sessao.UsuarioId ?? throw new InvalidOperationException("Nenhum atendente conectado. Entre com o PIN para operar o caixa.");

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
