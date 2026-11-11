using System.Collections.Concurrent;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Infrastructure.Pix;

/// <summary>RN-PX-01: sem configuração, o Pix integrado fica desligado e o app cobra o Pix à mão, como antes.</summary>
public sealed class DesligadoProvedorPix(int expiracaoSegundos = CobrancaPix.ExpiracaoPadraoSegundos) : IProvedorPix
{
    public string Nome => "Desligado";

    public bool Configurado => false;

    public int ExpiracaoSegundos => expiracaoSegundos;

    public Task<CobrancaNoBanco> CriarAsync(NovaCobrancaPix cobranca, CancellationToken cancellationToken = default) => throw Desligado();

    public Task<CobrancaNoBanco> ConsultarAsync(string txid, CancellationToken cancellationToken = default) => throw Desligado();

    public Task<CobrancaNoBanco> CancelarAsync(string txid, CancellationToken cancellationToken = default) => throw Desligado();

    private static ProvedorPixException Desligado() => new("O Pix integrado não está ligado neste servidor.");
}

/// <summary>
/// Pix de mentira para testar o app sem banco (emulador, tablet de teste): toda cobrança é "paga" depois de
/// <see cref="PixOptions.SimuladoPagaEmSegundos"/>. Nunca roda em produção (<see cref="PixOptions.Normalizar"/>). Guarda em
/// memória: reiniciar a API esquece as cobranças (a consulta de uma esquecida a dá como removida; o app começa outra).
/// </summary>
public sealed class SimuladoProvedorPix(PixOptions opcoes, TimeProvider? relogio = null) : IProvedorPix
{
    private readonly ConcurrentDictionary<string, (decimal Valor, DateTimeOffset Criada, bool Removida)> _cobrancas = new();
    private readonly TimeProvider _relogio = relogio ?? TimeProvider.System;

    public string Nome => PixOptions.ProvedorSimulado;

    public bool Configurado => true;

    public int ExpiracaoSegundos => opcoes.ExpiracaoSegundos;

    public Task<CobrancaNoBanco> CriarAsync(NovaCobrancaPix cobranca, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cobranca);
        var registro = _cobrancas.GetOrAdd(cobranca.Txid, _ => (cobranca.Valor, _relogio.GetUtcNow(), false));
        return Task.FromResult(Montar(cobranca.Txid, registro));
    }

    public Task<CobrancaNoBanco> ConsultarAsync(string txid, CancellationToken cancellationToken = default)
    {
        var registro = _cobrancas.TryGetValue(txid, out var r) ? r : (0.01m, DateTimeOffset.MinValue, true);
        return Task.FromResult(Montar(txid, registro));
    }

    public Task<CobrancaNoBanco> CancelarAsync(string txid, CancellationToken cancellationToken = default)
    {
        if (_cobrancas.TryGetValue(txid, out var r) && !Paga(r))
        {
            r = (r.Valor, r.Criada, true);
            _cobrancas[txid] = r;
        }

        return ConsultarAsync(txid, cancellationToken);
    }

    private bool Paga((decimal Valor, DateTimeOffset Criada, bool Removida) r) =>
        !r.Removida && _relogio.GetUtcNow() - r.Criada >= TimeSpan.FromSeconds(opcoes.SimuladoPagaEmSegundos);

    private CobrancaNoBanco Montar(string txid, (decimal Valor, DateTimeOffset Criada, bool Removida) r)
    {
        var situacao = r.Removida
            ? new SituacaoCobrancaPix(StatusCobrancaPix.Removida)
            : Paga(r)
                ? new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, "ESIMULADO" + txid[..Math.Min(24, txid.Length)], r.Valor, _relogio.GetUtcNow().UtcDateTime)
                : new SituacaoCobrancaPix(StatusCobrancaPix.Ativa);
        return new CobrancaNoBanco(txid, r.Valor, $"00020126580014BR.GOV.BCB.PIX0136SIMULADO-NAO-PAGUE-{txid}5204000053039865802BR6304ABCD", situacao);
    }
}
