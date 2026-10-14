using SorveteriaMaui.Model;
using SQLite;

namespace SorveteriaMaui.Data;

/// <summary>Conexão única com o SQLite do aparelho. Cria as tabelas no primeiro uso.</summary>
public sealed class BancoLocal
{
    // Nunca trocar o nome do arquivo: um banco novo deixaria para trás as vendas que ainda aguardam envio.
    // Tabela nova entra no CreateTablesAsync abaixo (o sqlite-net cria a tabela e acrescenta colunas novas sozinho).
    private readonly string _caminho = Path.Combine(FileSystem.AppDataDirectory, "sorveteria_v5.db3");
    private readonly SemaphoreSlim _inicializacao = new(1, 1);
    private SQLiteAsyncConnection? _conexao;

    public async Task<SQLiteAsyncConnection> ConexaoAsync()
    {
        if (_conexao is not null)
        {
            return _conexao;
        }

        await _inicializacao.WaitAsync();
        try
        {
            if (_conexao is null)
            {
                var conexao = new SQLiteAsyncConnection(_caminho);
                await conexao.CreateTablesAsync<Comanda, ItemComanda, Pagamento, Produto>();
                await conexao.CreateTablesAsync<Caixa, MovimentoCaixa>(); // C3
                await conexao.CreateTablesAsync<ItemEstoqueLocal, AvisoLocal>(); // Fase 2: "Está acabando"
                await conexao.CreateTablesAsync<ContagemLocal, ItemContagemLocal>(); // Fase 2: contagem semanal
                _conexao = conexao;
            }

            return _conexao;
        }
        finally
        {
            _inicializacao.Release();
        }
    }
}
