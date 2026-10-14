using SorveteriaMaui.Model;

namespace SorveteriaMaui.Data;

/// <summary>Cache local do catálogo de estoque (Fase 2). Vem da API; o atendente só lê.</summary>
public sealed class ItemEstoqueRepository(BancoLocal banco)
{
    /// <summary>Itens ativos de todos os grupos, na ordem da planilha (grupo, depois item).</summary>
    public async Task<List<ItemEstoqueLocal>> ListarAtivosAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ItemEstoqueLocal>()
            .Where(i => i.Ativo == true)
            .OrderBy(i => i.GrupoOrdem)
            .ThenBy(i => i.GrupoNome)
            .ThenBy(i => i.Ordem)
            .ThenBy(i => i.Nome)
            .ToListAsync();
    }

    public async Task<ItemEstoqueLocal?> ObterAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        return await db.FindAsync<ItemEstoqueLocal>(id);
    }

    /// <summary>
    /// A API devolve o catálogo inteiro (com os desativados, que a tela esconde): troca o cache numa transação.
    /// Os avisos guardam a própria descrição, então nada mais muda.
    /// </summary>
    public async Task SubstituirTudoAsync(IReadOnlyList<ItemEstoqueLocal> itens)
    {
        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(conexao =>
        {
            conexao.DeleteAll<ItemEstoqueLocal>();
            conexao.InsertAll(itens, runInTransaction: false);
        });
    }
}
