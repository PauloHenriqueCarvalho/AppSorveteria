using SorveteriaMaui.Model;

namespace SorveteriaMaui.Data;

/// <summary>Cache local do catálogo de produtos (vem da API, C2). Nunca é editado pelo atendente.</summary>
public sealed class ProdutoRepository(BancoLocal banco)
{
    /// <summary>RN-PR-03/05: só ativos, na ordem dos botões.</summary>
    public async Task<List<Produto>> ListarAtivosAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Produto>()
            .Where(p => p.Ativo == true)
            .OrderBy(p => p.Ordem)
            .ThenBy(p => p.Nome)
            .ToListAsync();
    }

    public async Task<Produto?> ObterAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        return await db.FindAsync<Produto>(id);
    }

    public async Task<int> ContarAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Produto>().CountAsync();
    }

    /// <summary>
    /// Primeira carga: troca o cache inteiro pelo catálogo da API, numa transação. Some o que era local
    /// (produtos do seed do MVP e os cadastrados no aparelho, B16); as comandas não mudam, porque o item
    /// guarda descrição e preço praticados (RN-PR-04).
    /// </summary>
    public async Task SubstituirTudoAsync(IReadOnlyList<Produto> produtos)
    {
        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(conexao =>
        {
            conexao.DeleteAll<Produto>();
            conexao.InsertAll(produtos, runInTransaction: false);
        });
    }

    /// <summary>Cargas seguintes: grava só o que mudou (inclusive os desativados, que a tela esconde).</summary>
    public async Task GravarAsync(IReadOnlyList<Produto> produtos)
    {
        if (produtos.Count == 0)
        {
            return;
        }

        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(conexao =>
        {
            foreach (var produto in produtos)
            {
                conexao.InsertOrReplace(produto);
            }
        });
    }
}
