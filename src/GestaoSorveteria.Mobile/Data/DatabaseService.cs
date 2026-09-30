using StatusComanda = GestaoSorveteria.Domain.Comandas.StatusComanda;
using SorveteriaMaui.Model;
using SQLite;
using System.Threading;
using System.Threading.Tasks;

namespace SorveteriaMaui.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _db;
        private readonly string _dbPath;
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);

        public DatabaseService()
        {
            // v4: tabelas novas (decimal em centavos, UTC, enums, Guid)
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "sorveteria_v4.db3");
        }

        public async Task PopularBancoSeVazio()
        {
            await Init();
            var contagem = await _db.Table<Produto>().CountAsync();

            if (contagem == 0)
            {
                var agora = DateTime.UtcNow;
                var ordem = 0;
                Produto Novo(string nome, decimal preco, string categoria) =>
                    new Produto { Nome = nome, Preco = preco, Categoria = categoria, Ordem = ordem++, CriadoEm = agora };

                var produtosIniciais = new List<Produto>
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
                };

                try
                {
                    await _db.InsertAllAsync(produtosIniciais);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro populando produtos iniciais: {ex.Message}");
                }
            }
        }

        private async Task Init()
        {
            if (_db is not null) return;

            await _initSemaphore.WaitAsync();
            try
            {
                if (_db is not null) return;

                _db = new SQLiteAsyncConnection(_dbPath);
                try
                {
                    await _db.CreateTableAsync<Comanda>();
                    await _db.CreateTableAsync<Produto>();
                    await _db.CreateTableAsync<ItemComanda>();
                    await _db.CreateTableAsync<Pagamento>();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro criando tabelas SQLite: {ex.Message}");
                    throw;
                }
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        #region Métodos de Comanda

        public async Task<List<Comanda>> GetComandasAbertas()
        {
            await Init();
            return await _db.Table<Comanda>().Where(c => c.Status == StatusComanda.Aberta).ToListAsync();
        }

        public async Task SalvarComandaCompleta(Comanda comanda, List<ItemComanda> itens)
        {
            await Init();
            try
            {
                await _db.RunInTransactionAsync(tran =>
                {
                    tran.InsertOrReplace(comanda);
                    foreach (var item in itens)
                    {
                        item.ComandaId = comanda.Id;
                        tran.InsertOrReplace(item);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar comanda completa: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de Produto

        public async Task<List<Produto>> GetProdutosAtivos()
        {
            await Init();
            return await _db.Table<Produto>().Where(p => p.Ativo == true).OrderBy(p => p.Ordem).ThenBy(p => p.Nome).ToListAsync();
        }

        public async Task<int> SalvarProduto(Produto produto)
        {
            await Init();
            try
            {
                var existente = await _db.Table<Produto>().FirstOrDefaultAsync(p => p.Id == produto.Id);
                if (existente != null)
                    return await _db.UpdateAsync(produto);

                return await _db.InsertAsync(produto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao salvar produto: {ex.Message}");
                return 0;
            }
        }

        #endregion

        #region Métodos de Pagamento

        public async Task RegistrarPagamento(Pagamento pagamento)
        {
            await Init();
            try
            {
                await _db.InsertAsync(pagamento);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao registrar pagamento: {ex.Message}");
            }
        }

        public async Task<Comanda> CriarComanda(int numero, string nomeCliente = "")
        {
            await Init();
            var novaComanda = new Comanda
            {
                Numero = numero,
                NomeCliente = nomeCliente,
                Status = StatusComanda.Aberta,
                CriadaEm = DateTime.UtcNow,
            };

            try
            {
                await _db.InsertAsync(novaComanda);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao criar comanda: {ex.Message}");
            }

            return novaComanda;
        }

        public Task AdicionarProdutoNaComanda(Guid comandaId, Produto produto, int quantidade) =>
            InserirItem(new ItemComanda
            {
                ComandaId = comandaId,
                ProdutoId = produto.Id,
                Descricao = produto.Nome,
                Quantidade = quantidade,
                PrecoUnitario = produto.Preco,
                Subtotal = quantidade * produto.Preco,
            });

        /// <summary>Self-service / venda avulsa: sem produto cadastrado (RN-CM-03, B9).</summary>
        public Task AdicionarItemLivreNaComanda(Guid comandaId, string descricao, decimal valor) =>
            InserirItem(new ItemComanda
            {
                ComandaId = comandaId,
                ProdutoId = null,
                Descricao = descricao,
                Quantidade = 1,
                PrecoUnitario = valor,
                Subtotal = valor,
            });

        private async Task InserirItem(ItemComanda item)
        {
            await Init();

            try
            {
                await _db.RunInTransactionAsync(tran =>
                {
                    tran.Insert(item);

                    var comanda = tran.Table<Comanda>().FirstOrDefault(c => c.Id == item.ComandaId);
                    if (comanda != null)
                    {
                        comanda.Total += item.Subtotal;
                        tran.Update(comanda);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao adicionar item na comanda: {ex.Message}");
            }
        }

        public async Task FinalizarComanda(Guid comandaId)
        {
            await Init();

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
            if (comanda != null)
            {
                try
                {
                    comanda.Status = StatusComanda.Fechada;
                    comanda.FechadaEm = DateTime.UtcNow;
                    comanda.PendenteEnvio = true;

                    await _db.UpdateAsync(comanda);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao finalizar comanda: {ex.Message}");
                }
            }
        }

        public async Task AtualizarNomeComanda(Guid comandaId, string novoNome)
        {
            await Init();
            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
            if (comanda != null)
            {
                comanda.NomeCliente = novoNome;
                try
                {
                    await _db.UpdateAsync(comanda);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao atualizar nome da comanda: {ex.Message}");
                }
            }
        }

        public async Task RemoverProdutoDaComanda(ItemComanda item)
        {
            await Init();

            try
            {
                await _db.DeleteAsync(item);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao remover item da comanda: {ex.Message}");
            }

            var todosItens = await _db.Table<ItemComanda>().Where(i => i.ComandaId == item.ComandaId).ToListAsync();

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == item.ComandaId);
            if (comanda != null)
            {
                comanda.Total = todosItens.Sum(i => i.Subtotal);
                try
                {
                    await _db.UpdateAsync(comanda);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao atualizar total da comanda apos remoção: {ex.Message}");
                }
            }
        }

        public async Task<List<ItemComanda>> GetItensDaComanda(Guid comandaId)
        {
            await Init();
            return await _db.Table<ItemComanda>()
                           .Where(i => i.ComandaId == comandaId)
                           .ToListAsync();
        }

        public async Task<Comanda> GetComandaPorId(Guid id)
        {
            await Init();
            return await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Comanda>> GetComandasPendentesEnvio()
        {
            await Init();
            return await _db.Table<Comanda>()
                           .Where(c => c.PendenteEnvio == true && c.Status != StatusComanda.Aberta)
                           .ToListAsync();
        }

        public async Task MarcarComandaEnviada(Guid comandaId)
        {
            await Init();
            try
            {
                var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
                if (comanda != null)
                {
                    comanda.PendenteEnvio = false;
                    await _db.UpdateAsync(comanda);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao marcar comanda enviada: {ex.Message}");
            }
        }

        #endregion
    }
}
