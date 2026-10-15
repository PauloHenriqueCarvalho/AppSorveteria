using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Clima guardado em memória (RN-CL-02/03).</summary>
internal sealed class ClimaRepositoryFake : IClimaRepository
{
    public List<ClimaDia> Dias { get; } = new();

    public List<PrevisaoClima> Previsoes { get; } = new();

    public Task<IReadOnlyList<ClimaDia>> ListarDiasAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ClimaDia>>(Dias.Where(d => d.Ponto == ponto && d.Data >= de && d.Data <= ate).OrderBy(d => d.Data).ToList());

    public Task AdicionarDiaAsync(ClimaDia dia, CancellationToken cancellationToken = default)
    {
        if (Dias.Any(d => d.Ponto == dia.Ponto && d.Data == dia.Data))
        {
            throw new InvalidOperationException("Índice único (ponto, dia) violado.");
        }

        Dias.Add(dia);
        return Task.CompletedTask;
    }

    public Task<bool> ExistePrevisaoFeitaNoDiaAsync(PontoClima ponto, DateOnly feitaNoDia, CancellationToken cancellationToken = default) =>
        Task.FromResult(Previsoes.Any(p => PontoDe(p) == ponto && p.FeitaNoDia == feitaNoDia));

    public Task AdicionarPrevisoesAsync(IReadOnlyCollection<PrevisaoClima> previsoes, CancellationToken cancellationToken = default)
    {
        foreach (var previsao in previsoes)
        {
            if (Previsoes.Any(p => PontoDe(p) == PontoDe(previsao) && p.FeitaNoDia == previsao.FeitaNoDia && p.Data == previsao.Data))
            {
                throw new InvalidOperationException("Índice único (ponto, feita no dia, dia) violado.");
            }

            Previsoes.Add(previsao);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PrevisaoClima>> ListarUltimaPrevisaoAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
    {
        var doPonto = Previsoes.Where(p => PontoDe(p) == ponto && p.Data >= de && p.Data <= ate).ToList();
        if (doPonto.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PrevisaoClima>>([]);
        }

        var ultima = doPonto.Max(p => p.FeitaNoDia);
        return Task.FromResult<IReadOnlyList<PrevisaoClima>>(doPonto.Where(p => p.FeitaNoDia == ultima).OrderBy(p => p.Data).ToList());
    }

    public Task<ResumoClimaGuardado> ResumoAsync(PontoClima ponto, CancellationToken cancellationToken = default)
    {
        var doPonto = Dias.Where(d => d.Ponto == ponto).ToList();
        return Task.FromResult(doPonto.Count == 0
            ? new ResumoClimaGuardado(null, null, 0)
            : new ResumoClimaGuardado(doPonto.Min(d => d.Data), doPonto.Max(d => d.Data), doPonto.Count));
    }

    private static PontoClima PontoDe(PrevisaoClima p) => PontoClima.De(p.Latitude, p.Longitude);
}

/// <summary>
/// Serviço de clima falso: devolve uma leitura por dia pedido (máxima = <see cref="MaximaPadrao"/>, ou a de
/// <see cref="Maximas"/>), passado como <see cref="OrigemDoPassado"/> e futuro como previsto. Guarda as chamadas.
/// </summary>
internal sealed class FonteClimaFake : IFonteClima
{
    public string Nome => "Teste";

    public string Atribuicao => "Dados do clima: Teste";

    public string AtribuicaoLink => "https://exemplo.test/";

    public bool Configurada { get; set; } = true;

    /// <summary>"Hoje" para decidir o que é passado e o que é previsão nas leituras.</summary>
    public DateOnly Hoje { get; set; }

    public decimal MaximaPadrao { get; set; } = 28m;

    public Dictionary<DateOnly, decimal> Maximas { get; } = new();

    public Dictionary<DateOnly, decimal> Chuvas { get; } = new();

    public OrigemClima OrigemDoPassado { get; set; } = OrigemClima.Observado;

    public List<(PontoClima Ponto, DateOnly De, DateOnly Ate)> Chamadas { get; } = new();

    /// <summary>Número da chamada (1, 2, 3…) que falha como o serviço fora do ar.</summary>
    public int? FalharNaChamada { get; set; }

    /// <summary>Dias que voltam com a máxima abaixo da mínima (dado estranho).</summary>
    public HashSet<DateOnly> DiasEstranhos { get; } = new();

    public LocalEncontrado? Local { get; set; }

    public string? ProcuradoPor { get; private set; }

    public bool FalharAoProcurar { get; set; }

    public Task<IReadOnlyList<LeituraClima>> LerDiasAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
    {
        Chamadas.Add((ponto, de, ate));
        if (FalharNaChamada == Chamadas.Count)
        {
            throw new FonteClimaException("O serviço de clima não respondeu. A próxima tentativa é automática.");
        }

        var leituras = Enumerable.Range(0, ate.DayNumber - de.DayNumber + 1)
            .Select(i => de.AddDays(i))
            .Select(dia =>
            {
                var maxima = Maximas.GetValueOrDefault(dia, MaximaPadrao);
                var minima = DiasEstranhos.Contains(dia) ? maxima + 5m : maxima - 10m;
                return new LeituraClima(
                    dia, maxima, minima, (maxima + minima) / 2m, maxima + 1m, Chuvas.GetValueOrDefault(dia, 0m), 0m, 40m, 60m, 10m,
                    "clear-day", "Céu limpo", dia > Hoje ? OrigemClima.Previsto : OrigemDoPassado);
            })
            .ToList();
        return Task.FromResult<IReadOnlyList<LeituraClima>>(leituras);
    }

    public Task<LocalEncontrado?> ProcurarLocalAsync(string texto, CancellationToken cancellationToken = default)
    {
        ProcuradoPor = texto;
        return FalharAoProcurar
            ? throw new FonteClimaException("A chave do serviço de clima foi recusada. Confira Clima__ChaveApi no servidor.")
            : Task.FromResult(Local);
    }
}
