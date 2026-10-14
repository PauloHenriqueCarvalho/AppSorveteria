using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence.Repositories;

internal sealed class ConfiguracaoLojaRepository : IConfiguracaoLojaRepository
{
    private readonly AppDbContext _db;

    public ConfiguracaoLojaRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task<ConfiguracaoLoja?> ObterAsync(CancellationToken cancellationToken = default) =>
        _db.ConfiguracoesLoja.AsQueryable().FirstOrDefaultAsync(c => c.Id == ConfiguracaoLoja.IdUnico, cancellationToken);

    public async Task AdicionarAsync(ConfiguracaoLoja configuracao, CancellationToken cancellationToken = default) =>
        await _db.ConfiguracoesLoja.AddAsync(configuracao, cancellationToken);
}
