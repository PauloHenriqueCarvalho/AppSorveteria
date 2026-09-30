using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Produtos;

/// <summary>Catálogo de produtos (RN-PR-*).</summary>
public sealed class ProdutoService
{
    /// <summary>
    /// Uma alteração gravada com AtualizadoEm anterior ao geradoEm desta chamada, mas confirmada no banco depois do SELECT,
    /// ficaria de fora para sempre na próxima sincronização. Recuar o "desde" alguns minutos fecha essa janela;
    /// receber de novo um produto já conhecido é inofensivo.
    /// </summary>
    public static readonly TimeSpan MargemSeguranca = TimeSpan.FromMinutes(5);

    private readonly IProdutoRepository _produtos;
    private readonly IClock _clock;

    public ProdutoService(IProdutoRepository produtos, IClock clock)
    {
        _produtos = produtos;
        _clock = clock;
    }

    /// <summary>
    /// Catálogo completo (sem <paramref name="desdeUtc"/>) ou só os produtos criados/alterados a partir daquele instante.
    /// Produtos inativos vêm junto: é assim que o app fica sabendo que deve esconder o botão (RN-PR-03).
    /// A comparação é "maior ou igual" e recua <see cref="MargemSeguranca"/>: receber de novo um produto é inofensivo, perder um não.
    /// </summary>
    public async Task<CatalogoProdutosResponse> ListarAsync(DateTime? desdeUtc, CancellationToken cancellationToken = default)
    {
        var geradoEm = _clock.UtcNow;

        var produtos = desdeUtc is null
            ? await _produtos.ListarAsync(somenteAtivos: false, cancellationToken)
            : await _produtos.ListarAlteradosDesdeAsync(ParaUtc(desdeUtc.Value) - MargemSeguranca, cancellationToken);

        return new CatalogoProdutosResponse(geradoEm, produtos.Select(p => p.ToDto()).ToList());
    }

    private static DateTime ParaUtc(DateTime data) => data.Kind switch
    {
        DateTimeKind.Utc => data,
        DateTimeKind.Local => data.ToUniversalTime(),
        _ => DateTime.SpecifyKind(data, DateTimeKind.Utc), // querystring sem "Z" chega Unspecified: assumimos UTC (RN-TD-01)
    };
}
