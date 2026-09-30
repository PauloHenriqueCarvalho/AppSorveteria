using SorveteriaMaui.Model;
using SQLite;

namespace SorveteriaMaui.Data;

/// <summary>Conexão única com o SQLite do aparelho. Cria as tabelas no primeiro uso.</summary>
public sealed class BancoLocal
{
    private readonly string _caminho = Path.Combine(FileSystem.AppDataDirectory, "sorveteria_v4.db3");
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
