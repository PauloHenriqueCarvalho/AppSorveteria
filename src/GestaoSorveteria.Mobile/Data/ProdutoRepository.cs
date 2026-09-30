using SorveteriaMaui.Model;

namespace SorveteriaMaui.Data;

/// <summary>Cache local do catálogo de produtos.</summary>
public sealed class ProdutoRepository(BancoLocal banco)
{
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

    public async Task SalvarAsync(Produto produto)
    {
        var db = await banco.ConexaoAsync();
        await db.InsertOrReplaceAsync(produto);
    }

    /// <summary>Produtos iniciais do MVP até o app baixar o catálogo da API (Etapa C).</summary>
    public async Task PopularSeVazioAsync()
    {
        var db = await banco.ConexaoAsync();
        if (await db.Table<Produto>().CountAsync() > 0)
        {
            return;
        }

        var agora = DateTime.UtcNow;
        var ordem = 0;
        Produto Novo(string nome, decimal preco, string categoria) =>
            new() { Nome = nome, Preco = preco, Categoria = categoria, Ordem = ordem++, CriadoEm = agora };

        await db.InsertAllAsync(new[]
        {
            Novo("Sorvete de Fruta", 2.00m, "Sorvete"),
            Novo("Sorvete de Leite", 2.50m, "Sorvete"),
            Novo("Sorvete Especial", 8.00m, "Sorvete"),
            Novo("Sorvete Skimo", 5.00m, "Sorvete"),
            Novo("Sorvete Moreninha", 7.00m, "Sorvete"),
            Novo("Promoção de Picoles", 37.00m, "Sorvete"),
            Novo("Promoção de Potes 1L", 48.00m, "Sorvete"),
            Novo("Açaí Montado 400ml", 23.00m, "Açaí"),
            Novo("Água Mineral", 3.50m, "Bebida"),
        });
    }
}
