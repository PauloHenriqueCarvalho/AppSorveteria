using SorveteriaMaui.Model;
using SQLite;

namespace SuaSorveteria.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _db;
        private readonly string _dbPath;

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

                await _db.InsertAllAsync(produtosIniciais);
            }
        }
        private async Task Init()
        {
            if (_db is not null) return;

            _db = new SQLiteAsyncConnection(_dbPath);
            // Tente criar individualmente para garantir
            await _db.CreateTableAsync<Comanda>();
            await _db.CreateTableAsync<Produto>();
            await _db.CreateTableAsync<ItemComanda>();
            await _db.CreateTableAsync<Pagamento>();
            await _db.CreateTableAsync<LogSincronizacao>();
            await PopularBancoSeVazio();
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

            // Usamos uma transação para garantir que ou salva tudo, ou nada (evita erro de banco)
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
            if (!string.IsNullOrEmpty(produto.Id))
                return await _db.UpdateAsync(produto);

            return await _db.InsertAsync(produto);
        }

        #endregion

        #region Métodos de Pagamento

        public async Task RegistrarPagamento(Pagamento pagamento)
        {
            await Init();
            await _db.InsertAsync(pagamento);

            // Aqui você poderia adicionar a lógica de fechar a comanda automaticamente
        }


        public async Task<Comanda> CriarComanda(int numero, string nomeCliente = "")
        {
            await Init();
            var novaComanda = new Comanda
            {
                Id = Guid.NewGuid().ToString(),
                Numero = numero,
                NomeCliente = nomeCliente,
                Status = 0, // Aberta
                DataAbertura = DateTime.Now,
                Sincronizado = 0
            };

            await _db.InsertAsync(novaComanda);
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
                ProdutoNome = produto.Nome, // Salva o nome para facilitar exibição depois
                Quantidade = quantidade,
                PrecoUnitario = produto.Preco,
                Total = produto.Preco * quantidade
            };

            // Usamos uma transação para garantir que o item entra E a comanda atualiza o total
            await _db.RunInTransactionAsync(tran =>
            {
                tran.Insert(item);

                // Busca a comanda para atualizar o total
                var comanda = tran.Table<Comanda>().FirstOrDefault(c => c.Id == comandaId);
                if (comanda != null)
                {
                    comanda.Subtotal += item.Total;
                    comanda.Total = (comanda.Subtotal + comanda.AcrescimoManual) - comanda.DescontoManual;
                    tran.Update(comanda);
                }
            });
        }

        public async Task FinalizarComanda(string comandaId)
        {
            await Init();

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
            if (comanda != null)
            {
                comanda.Status = 1; // Fechada
                comanda.DataFechamento = DateTime.Now;
                comanda.Sincronizado = 0; // Marcar para enviar ao servidor depois

                await _db.UpdateAsync(comanda);
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
                Tipo = tipo, // 0=Sorvete, 1=Açaí, etc
                Ativo = 1,
                DataCriacao = DateTime.Now
            };

            await _db.InsertAsync(novoProduto);
        }

        public async Task AdicionarAcrescimoManual(string comandaId, double valorAcrescimo)
        {
            await Init();

            // Buscamos a comanda atual
            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);

            if (comanda != null)
            {
                // Somamos ao acréscimo que já existir (ou substituímos, se preferir)
                comanda.AcrescimoManual += valorAcrescimo;

                // Recalculamos o Total final
                // Total = (Subtotal + Acréscimos) - Descontos
                comanda.Total = (comanda.Subtotal + comanda.AcrescimoManual) - comanda.DescontoManual;

                // Atualizamos apenas a comanda no banco
                await _db.UpdateAsync(comanda);
            }
        }

        #endregion

        // Atualiza o nome do cliente na comanda
        public async Task AtualizarNomeComanda(string comandaId, string novoNome)
        {
            await Init();
            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == comandaId);
            if (comanda != null)
            {
                comanda.NomeCliente = novoNome;
                await _db.UpdateAsync(comanda);
            }
        }

        // Remove um produto específico e abate do total
        // Deleta um item e subtrai o valor do total da comanda
        public async Task RemoverProdutoDaComanda(ItemComanda item)
        {
            await Init();

            // 1. Remove o item
            await _db.DeleteAsync(item);

            // 2. Recalcula o total da comanda
            var todosItens = await _db.Table<ItemComanda>().Where(i => i.ComandaId == item.ComandaId).ToListAsync();
            double novoTotal = todosItens.Sum(i => i.Total);

            var comanda = await _db.Table<Comanda>().FirstOrDefaultAsync(c => c.Id == item.ComandaId);
            if (comanda != null)
            {
                comanda.Total = novoTotal;
                await _db.UpdateAsync(comanda);
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
    }


}