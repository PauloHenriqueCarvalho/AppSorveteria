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
            // Caminho automático que funciona em Android, iOS e Windows
            _dbPath = Path.Combine(FileSystem.AppDataDirectory, "sorveteria_v3.db3");
        }

        public async Task PopularBancoSeVazio()
        {
            await Init();
            var contagem = await _db.Table<Produto>().CountAsync();

            if (contagem == 0)
            {
                var produtosIniciais = new List<Produto>
                {
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Sorvete de Fruta", Preco = 2.00, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Sorvete de Leite", Preco = 2.50, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Sorvete Especial", Preco = 8.00, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Sorvete Skimo", Preco = 5.00, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Sorvete Moreninha", Preco = 7.00, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Promoção de Picoles", Preco = 37.00, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Promoção de Potes 1L", Preco = 48.00, Tipo = 0, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Açaí Montado 400ml", Preco = 23.00, Tipo = 1, Ativo = 1, DataCriacao = DateTime.Now },
                    new Produto { Id = Guid.NewGuid().ToString(), Nome = "Água Mineral", Preco = 3.50, Tipo = 2, Ativo = 1, DataCriacao = DateTime.Now }
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
                    await _db.CreateTableAsync<LogSincronizacao>();
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
            return await _db.Table<Comanda>().Where(c => c.Status == 0).ToListAsync();
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
            return await _db.Table<Produto>().Where(p => p.Ativo == 1).OrderBy(p => p.Nome).ToListAsync();
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
                Id = Guid.NewGuid().ToString(),
                Numero = numero,
                NomeCliente = nomeCliente,
                Status = 0,
                DataAbertura = DateTime.Now,
                Sincronizado = 0,
                PendenteSincronizacao = false
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

        public async Task AdicionarProdutoNaComanda(string comandaId, Produto produto, double quantidade)
        {
            await Init();

            var item = new ItemComanda
            {
                Id = Guid.NewGuid().ToString(),
                ComandaId = comandaId,
                ProdutoId = produto.Id,
                ProdutoNome = produto.Nome,
                Quantidade = quantidade,
                PrecoUnitario = produto.Preco,
                Total = produto.Preco * quantidade
            };

            try
            {
                await _db.RunInTransactionAsync(tran =>
                {
                    tran.Insert(item);

                    var comanda = tran.Table<Comanda>().FirstOrDefault(c => c.Id == comandaId);
                    if (comanda != null)
                    {
                        comanda.Subtotal += item.Total;
                        comanda.Total = (comanda.Subtotal + comanda.AcrescimoManual) - comanda.DescontoManual;
                        tran.Update(comanda);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao adicionar produto na comanda: {ex.Message}");
            }
        }

        public async Task FinalizarComanda(string comandaId)
        {
            await Init();

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
            if (comanda != null)
            {
                try
                {
                    comanda.Status = 1;
                    comanda.DataFechamento = DateTime.Now;
                    comanda.Sincronizado = 0;
                    comanda.PendenteSincronizacao = true;

                    await _db.UpdateAsync(comanda);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao finalizar comanda: {ex.Message}");
                }
            }
        }

        public async Task CriarProduto(string nome, double preco, int tipo)
        {
            await Init();

            var novoProduto = new Produto
            {
                Id = Guid.NewGuid().ToString(),
                Nome = nome,
                Preco = preco,
                Tipo = tipo,
                Ativo = 1,
                DataCriacao = DateTime.Now
            };

            try
            {
                await _db.InsertAsync(novoProduto);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao criar produto: {ex.Message}");
            }
        }

        public async Task AdicionarAcrescimoManual(string comandaId, double valorAcrescimo)
        {
            await Init();

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);

            if (comanda != null)
            {
                comanda.AcrescimoManual += valorAcrescimo;
                comanda.Total = (comanda.Subtotal + comanda.AcrescimoManual) - comanda.DescontoManual;

                try
                {
                    await _db.UpdateAsync(comanda);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao adicionar acréscimo manual: {ex.Message}");
                }
            }
        }

        public async Task AtualizarNomeComanda(string comandaId, string novoNome)
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
            double novoTotal = todosItens.Sum(i => i.Total);

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == item.ComandaId);
            if (comanda != null)
            {
                comanda.Total = novoTotal;
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

        public async Task<List<ItemComanda>> GetItensDaComanda(string comandaId)
        {
            await Init();
            return await _db.Table<ItemComanda>()
                           .Where(i => i.ComandaId == comandaId)
                           .ToListAsync();
        }

        public async Task<Comanda> GetComandaPorId(string id)
        {
            await Init();
            return await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == id);
        }

        public async Task<List<Comanda>> GetComandasPendentesSincronizacao()
        {
            await Init();
            return await _db.Table<Comanda>()
                           .Where(c => c.PendenteSincronizacao == true && c.Status == 1)
                           .ToListAsync();
        }

        public async Task MarcarComandaSincronizada(string comandaId)
        {
            await Init();
            try
            {
                var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
                if (comanda != null)
                {
                    comanda.PendenteSincronizacao = false;
                    comanda.Sincronizado = 1;
                    await _db.UpdateAsync(comanda);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao marcar comanda sincronizada: {ex.Message}");
            }
        }

        #endregion
    }
}
