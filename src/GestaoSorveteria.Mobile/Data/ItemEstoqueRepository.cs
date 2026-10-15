using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Model;

namespace SorveteriaMaui.Data;

/// <summary>Cache local do catálogo de estoque (Fase 2). Vem da API; o atendente só lê ("Está acabando" e contagem semanal).</summary>
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

    /// <summary>
    /// Todos os itens, inclusive os desativados, na ordem da planilha. A contagem semanal precisa dos desativados que já
    /// foram contados na contagem em andamento (como o servidor faz), para não sumirem da tela no meio da contagem.
    /// </summary>
    public async Task<List<ItemEstoqueLocal>> ListarTodosAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ItemEstoqueLocal>()
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
    /// Itens de um grupo (inclusive os desativados, que dizem o modo do grupo), na ordem da planilha. Na venda do pote, os
    /// ativos são os sabores da grade (RN-ES-09).
    /// </summary>
    public async Task<List<ItemEstoqueLocal>> ListarDoGrupoAsync(Guid grupoId)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ItemEstoqueLocal>()
            .Where(i => i.GrupoId == grupoId)
            .OrderBy(i => i.Ordem)
            .ThenBy(i => i.Nome)
            .ToListAsync();
    }

    /// <summary>
    /// RN-ES-02: modo de controle de cada grupo que o tablet conhece (o modo vem em cada item baixado). Grupo sem item
    /// nenhum no tablet não aparece: quem pergunta usa a reserva do produto (<c>PedeSabor</c>).
    /// </summary>
    public async Task<Dictionary<Guid, ModoControle>> ModosDosGruposAsync()
    {
        var db = await banco.ConexaoAsync();
        var itens = await db.Table<ItemEstoqueLocal>().ToListAsync();
        return itens.GroupBy(i => i.GrupoId).ToDictionary(g => g.Key, g => g.First().ModoControleGrupo);
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
