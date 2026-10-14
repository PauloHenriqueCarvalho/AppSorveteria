using GestaoSorveteria.Domain.Loja;

namespace GestaoSorveteria.Domain.Repositories;

public interface IConfiguracaoLojaRepository
{
    /// <summary>A única linha de configuração, ou null antes da dona salvar pela primeira vez.</summary>
    Task<ConfiguracaoLoja?> ObterAsync(CancellationToken cancellationToken = default);

    Task AdicionarAsync(ConfiguracaoLoja configuracao, CancellationToken cancellationToken = default);
}
