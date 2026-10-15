using GestaoSorveteria.Application.Clima;
using GestaoSorveteria.Infrastructure.Clima;

namespace GestaoSorveteria.Server.Clima;

/// <summary>
/// RN-CL-04: enquanto a API está acordada, coleta o clima 1 min depois de ligar e depois a cada <see cref="ClimaOptions.IntervaloHoras"/>.
/// A API grátis dorme; ao acordar, esta rodada busca o que faltou. Erro nunca derruba a API: fica no log e na situação do painel.
/// </summary>
public sealed class ColetaClimaEmSegundoPlano : BackgroundService
{
    private static readonly TimeSpan EsperaAoLigar = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _escopos;
    private readonly ClimaOptions _opcoes;
    private readonly ILogger<ColetaClimaEmSegundoPlano> _log;

    public ColetaClimaEmSegundoPlano(IServiceScopeFactory escopos, ClimaOptions opcoes, ILogger<ColetaClimaEmSegundoPlano> log)
    {
        _escopos = escopos;
        _opcoes = opcoes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var aviso in _opcoes.Correcoes)
        {
            _log.LogWarning("Clima: {Aviso}", aviso);
        }

        try
        {
            await Task.Delay(EsperaAoLigar, stoppingToken);
            using var relogio = new PeriodicTimer(TimeSpan.FromHours(_opcoes.IntervaloHoras));
            do
            {
                await ColetarAsync(stoppingToken);
            }
            while (await relogio.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A API está desligando.
        }
    }

    private async Task ColetarAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var escopo = _escopos.CreateAsyncScope();
            var coleta = escopo.ServiceProvider.GetRequiredService<ColetaClimaService>();
            var resultado = await coleta.ColetarAsync(forcar: false, stoppingToken);
            if (resultado.Falhou)
            {
                _log.LogWarning("Clima: {Mensagem}", resultado.Mensagem);
            }
            else
            {
                _log.LogInformation("Clima: {Mensagem}", resultado.Mensagem);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Clima: a coleta falhou; a próxima rodada tenta de novo.");
        }
    }
}
