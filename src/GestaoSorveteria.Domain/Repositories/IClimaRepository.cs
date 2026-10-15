using GestaoSorveteria.Domain.Clima;

namespace GestaoSorveteria.Domain.Repositories;

/// <summary>Clima do dia e previsões guardadas, por ponto (RN-CL-01/02/03).</summary>
public interface IClimaRepository
{
    /// <summary>O clima dos dias do ponto em [<paramref name="de"/>, <paramref name="ate"/>], em ordem. Rastreado: a coleta atualiza os não definitivos.</summary>
    Task<IReadOnlyList<ClimaDia>> ListarDiasAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default);

    Task AdicionarDiaAsync(ClimaDia dia, CancellationToken cancellationToken = default);

    /// <summary>O dia mais antigo do ponto que ainda não é definitivo (a coleta volta até ele mesmo antes do início); nulo = nenhum.</summary>
    Task<DateOnly?> PrimeiroDiaAbertoAsync(PontoClima ponto, CancellationToken cancellationToken = default);

    /// <summary>RN-CL-03: já foi guardada a previsão feita neste dia comercial?</summary>
    Task<bool> ExistePrevisaoFeitaNoDiaAsync(PontoClima ponto, DateOnly feitaNoDia, CancellationToken cancellationToken = default);

    Task AdicionarPrevisoesAsync(IReadOnlyCollection<PrevisaoClima> previsoes, CancellationToken cancellationToken = default);

    /// <summary>
    /// A previsão guardada mais recentemente (o maior <c>feita_no_dia</c> com algum dia em [<paramref name="de"/>,
    /// <paramref name="ate"/>]) para esses dias, em ordem. Só leitura.
    /// </summary>
    Task<IReadOnlyList<PrevisaoClima>> ListarUltimaPrevisaoAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default);

    /// <summary>Primeiro e último dia com clima no ponto e quantos dias há (situação da coleta no painel).</summary>
    Task<ResumoClimaGuardado> ResumoAsync(PontoClima ponto, CancellationToken cancellationToken = default);
}

/// <summary>O que já foi guardado num ponto.</summary>
public sealed record ResumoClimaGuardado(DateOnly? PrimeiroDia, DateOnly? UltimoDia, int Dias);
