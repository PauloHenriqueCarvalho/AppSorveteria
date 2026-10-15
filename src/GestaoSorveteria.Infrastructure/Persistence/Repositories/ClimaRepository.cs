using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class ClimaRepository : IClimaRepository
{
    private readonly AppDbContext _db;

    public ClimaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ClimaDia>> ListarDiasAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
    {
        var (latitude, longitude) = (ponto.Latitude, ponto.Longitude);
        return await _db.ClimaDias
            .Where(c => c.Latitude == latitude && c.Longitude == longitude && c.Data >= de && c.Data <= ate)
            .OrderBy(c => c.Data)
            .ToListAsync(cancellationToken);
    }

    public async Task AdicionarDiaAsync(ClimaDia dia, CancellationToken cancellationToken = default) =>
        await _db.ClimaDias.AddAsync(dia, cancellationToken);

    public Task<DateOnly?> PrimeiroDiaAbertoAsync(PontoClima ponto, CancellationToken cancellationToken = default)
    {
        var (latitude, longitude) = (ponto.Latitude, ponto.Longitude);
        return _db.ClimaDias
            .AsNoTracking()
            .Where(c => c.Latitude == latitude && c.Longitude == longitude && !c.Definitivo)
            .MinAsync(c => (DateOnly?)c.Data, cancellationToken);
    }

    public Task<bool> ExistePrevisaoFeitaNoDiaAsync(PontoClima ponto, DateOnly feitaNoDia, CancellationToken cancellationToken = default)
    {
        var (latitude, longitude) = (ponto.Latitude, ponto.Longitude);
        return _db.PrevisoesClima.AnyAsync(
            p => p.Latitude == latitude && p.Longitude == longitude && p.FeitaNoDia == feitaNoDia, cancellationToken);
    }

    public async Task AdicionarPrevisoesAsync(IReadOnlyCollection<PrevisaoClima> previsoes, CancellationToken cancellationToken = default) =>
        await _db.PrevisoesClima.AddRangeAsync(previsoes, cancellationToken);

    public async Task<IReadOnlyList<PrevisaoClima>> ListarUltimaPrevisaoAsync(
        PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default)
    {
        var (latitude, longitude) = (ponto.Latitude, ponto.Longitude);
        var doPonto = _db.PrevisoesClima
            .AsNoTracking()
            .Where(p => p.Latitude == latitude && p.Longitude == longitude && p.Data >= de && p.Data <= ate);

        var ultima = await doPonto.MaxAsync(p => (DateOnly?)p.FeitaNoDia, cancellationToken);
        if (ultima is null)
        {
            return [];
        }

        return await doPonto
            .Where(p => p.FeitaNoDia == ultima.Value)
            .OrderBy(p => p.Data)
            .ToListAsync(cancellationToken);
    }

    public async Task<ResumoClimaGuardado> ResumoAsync(PontoClima ponto, CancellationToken cancellationToken = default)
    {
        var (latitude, longitude) = (ponto.Latitude, ponto.Longitude);
        var doPonto = _db.ClimaDias.AsNoTracking().Where(c => c.Latitude == latitude && c.Longitude == longitude);
        var dias = await doPonto.CountAsync(cancellationToken);
        if (dias == 0)
        {
            return new ResumoClimaGuardado(null, null, 0);
        }

        return new ResumoClimaGuardado(
            await doPonto.MinAsync(c => (DateOnly?)c.Data, cancellationToken),
            await doPonto.MaxAsync(c => (DateOnly?)c.Data, cancellationToken),
            dias);
    }
}
