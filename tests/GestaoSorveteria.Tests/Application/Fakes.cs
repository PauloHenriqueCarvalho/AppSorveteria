using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Repositório em memória: suficiente para testar casos de uso sem banco.</summary>
internal sealed class UsuarioRepositoryFake : IUsuarioRepository
{
    public List<Usuario> Usuarios { get; } = new();

    public Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == id));

    public Task<Usuario?> ObterPorLoginAsync(string login, CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuarios.FirstOrDefault(u => u.Login == login));

    public Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Usuarios.Count > 0);

    public Task<IReadOnlyList<Usuario>> ListarAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Usuario>>(Usuarios.ToList());

    public Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        Usuarios.Add(usuario);
        return Task.CompletedTask;
    }
}

/// <summary>Hash "transparente" só para testes: nunca use fora daqui.</summary>
internal sealed class PasswordHasherFake : IPasswordHasher
{
    public string Hash(string senha) => "hash:" + senha;

    public bool Verificar(string senha, string hashArmazenado) => hashArmazenado == "hash:" + senha;
}

internal sealed class TokenServiceFake : ITokenService
{
    public static readonly DateTime Expira = new(2026, 9, 18, 3, 0, 0, DateTimeKind.Utc);

    public TokenGerado Gerar(Usuario usuario) => new("token-" + usuario.Login, Expira);
}

internal sealed class ClockFake : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);
}

internal sealed class ProdutoRepositoryFake : IProdutoRepository
{
    public List<Produto> Produtos { get; } = new();

    public Task<Produto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Produtos.FirstOrDefault(p => p.Id == id));

    public Task<IReadOnlyList<Produto>> ListarAsync(bool somenteAtivos, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Produto>>(Ordenar(Produtos.Where(p => !somenteAtivos || p.Ativo)));

    public Task<IReadOnlyList<Produto>> ListarAlteradosDesdeAsync(DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Produto>>(Ordenar(Produtos.Where(p => (p.AtualizadoEm ?? p.CriadoEm) >= desdeUtc)));

    public Task<bool> ExisteComNomeAsync(string nomeNormalizado, Guid? ignorarId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Produtos.Any(p => p.NomeNormalizado == nomeNormalizado && (ignorarId is null || p.Id != ignorarId)));

    public Task AdicionarAsync(Produto produto, CancellationToken cancellationToken = default)
    {
        Produtos.Add(produto);
        return Task.CompletedTask;
    }

    private static List<Produto> Ordenar(IEnumerable<Produto> produtos) =>
        produtos.OrderBy(p => p.Categoria).ThenBy(p => p.Ordem).ThenBy(p => p.Nome).ToList();
}

/// <summary>Conta quantas vezes o caso de uso confirmou a transação.</summary>
internal sealed class UnitOfWorkFake : IUnitOfWork
{
    public int Confirmacoes { get; private set; }

    public int Descartes { get; private set; }

    /// <summary>Número da confirmação (1, 2, 3…) que deve falhar, simulando o banco fora do ar.</summary>
    public int? FalharNaConfirmacao { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FalharNaConfirmacao == Confirmacoes + 1)
        {
            throw new InvalidOperationException("Banco indisponível (simulado).");
        }

        Confirmacoes++;
        return Task.FromResult(1);
    }

    public void DescartarAlteracoes() => Descartes++;
}

internal sealed class CaixaRepositoryFake : ICaixaRepository
{
    public List<Caixa> Caixas { get; } = new();

    public Task<Caixa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Caixas.FirstOrDefault(c => c.Id == id));

    public Task<Caixa?> ObterAbertoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Caixas.FirstOrDefault(c => c.EstaAberto));

    public Task<IReadOnlyList<Caixa>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Caixa>>(Caixas.Where(c => c.AbertoEm >= deUtc && c.AbertoEm < ateUtc).ToList());

    public Task<bool> ExisteMovimentoAsync(Guid movimentoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Caixas.Any(c => c.Movimentos.Any(m => m.Id == movimentoId)));

    public Task AdicionarAsync(Caixa caixa, CancellationToken cancellationToken = default)
    {
        Caixas.Add(caixa);
        return Task.CompletedTask;
    }
}

internal sealed class ComandaRepositoryFake : IComandaRepository
{
    public List<Comanda> Comandas { get; } = new();

    public Task<Comanda?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Comanda>> ListarPorCaixaAsync(Guid caixaId, StatusComanda? status = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comanda>>(Comandas.Where(c => c.CaixaId == caixaId && (status is null || c.Status == status)).ToList());

    public Task<IReadOnlyList<Comanda>> ListarFechadasAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comanda>>(Comandas.Where(c => c.ContaNoCaixa && c.FechadaEm >= deUtc && c.FechadaEm < ateUtc).ToList());

    public Task<(IReadOnlyList<Comanda> Itens, int Total)> ListarPorPeriodoAsync(
        DateTime deUtc, DateTime ateUtc, StatusComanda? status, int pular, int quantidade, CancellationToken cancellationToken = default)
    {
        var filtradas = Comandas
            .Where(c => (c.FechadaEm ?? c.CanceladaEm ?? c.CriadaEm) >= deUtc && (c.FechadaEm ?? c.CanceladaEm ?? c.CriadaEm) < ateUtc)
            .Where(c => status is null || c.Status == status)
            .OrderByDescending(c => c.FechadaEm ?? c.CanceladaEm ?? c.CriadaEm)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Comanda>, int)>((filtradas.Skip(pular).Take(quantidade).ToList(), filtradas.Count));
    }

    public Task<int> ProximoNumeroAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Where(c => c.CaixaId == caixaId).Select(c => c.Numero).DefaultIfEmpty(0).Max() + 1);

    public Task<bool> ExisteAbertaNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Any(c => c.CaixaId == caixaId && c.EstaAberta));

    public Task<decimal> TotalDinheiroFechadasNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Where(c => c.CaixaId == caixaId && c.ContaNoCaixa).Sum(c => c.TotalEmDinheiro));

    public Task<bool> ExisteNumeroNoCaixaAsync(Guid caixaId, int numero, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Any(c => c.CaixaId == caixaId && c.Numero == numero));

    public Task<bool> ExisteItemOuPagamentoAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Any(c => c.Itens.Any(i => ids.Contains(i.Id)) || c.Pagamentos.Any(p => ids.Contains(p.Id))));

    public Task AdicionarAsync(Comanda comanda, CancellationToken cancellationToken = default)
    {
        Comandas.Add(comanda);
        return Task.CompletedTask;
    }
}

/// <summary>Catálogo de estoque da Fase 2 em memória.</summary>
internal sealed class EstoqueRepositoryFake : IEstoqueRepository
{
    public List<Fornecedor> Fornecedores { get; } = new();

    public List<GrupoEstoque> Grupos { get; } = new();

    public List<ItemEstoque> Itens { get; } = new();

    public Task<IReadOnlyList<Fornecedor>> ListarFornecedoresAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Fornecedor>>(Fornecedores.ToList());

    public Task<Fornecedor?> ObterFornecedorAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Fornecedores.FirstOrDefault(f => f.Id == id));

    public Task AdicionarFornecedorAsync(Fornecedor fornecedor, CancellationToken cancellationToken = default)
    {
        Fornecedores.Add(fornecedor);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GrupoEstoque>> ListarGruposAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GrupoEstoque>>(Grupos.OrderBy(g => g.Ordem).ThenBy(g => g.Nome).ToList());

    public Task<GrupoEstoque?> ObterGrupoAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Grupos.FirstOrDefault(g => g.Id == id));

    public Task AdicionarGrupoAsync(GrupoEstoque grupo, CancellationToken cancellationToken = default)
    {
        Grupos.Add(grupo);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ItemEstoque>> ListarItensAsync(bool somenteAtivos, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ItemEstoque>>(Itens
            .Where(i => !somenteAtivos || i.Ativo)
            .OrderBy(i => Grupos.FirstOrDefault(g => g.Id == i.GrupoId)?.Ordem ?? int.MaxValue)
            .ThenBy(i => i.Ordem)
            .ThenBy(i => i.Nome)
            .ToList());

    public Task<ItemEstoque?> ObterItemAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.FirstOrDefault(i => i.Id == id));

    public Task<bool> ExisteItemComNomeAsync(Guid grupoId, string nomeNormalizado, Guid? ignorarId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult(Itens.Any(i => i.GrupoId == grupoId && i.NomeNormalizado == nomeNormalizado && i.Id != ignorarId));

    public Task AdicionarItemAsync(ItemEstoque item, CancellationToken cancellationToken = default)
    {
        Itens.Add(item);
        return Task.CompletedTask;
    }
}

/// <summary>Avisos "Está acabando" em memória.</summary>
internal sealed class AvisoRepositoryFake : IAvisoRepository
{
    public List<Aviso> Avisos { get; } = new();

    public Task<Aviso?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Avisos.FirstOrDefault(a => a.Id == id));

    public Task<Aviso?> ObterAbertoDoItemAsync(Guid itemEstoqueId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Avisos.FirstOrDefault(a => a.ItemEstoqueId == itemEstoqueId && a.Aberto));

    public Task<IReadOnlyList<Aviso>> ListarAsync(DateTime? fechadosDesdeUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Aviso>>(Avisos
            .Where(a => a.Aberto || (fechadosDesdeUtc is { } desde && a.FechadoEm >= desde))
            .OrderByDescending(a => a.AtualizadoEm)
            .ToList());

    public Task<Aviso?> ObterUltimoFechadoDoItemAsync(Guid itemEstoqueId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Avisos.Where(a => a.ItemEstoqueId == itemEstoqueId && !a.Aberto).OrderByDescending(a => a.FechadoEm).FirstOrDefault());

    public Task<IReadOnlyList<Aviso>> ListarAtualizadosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Aviso>>(Avisos
            .Where(a => (a.CriadoEm >= deUtc && a.CriadoEm < ateUtc)
                || (a.AtualizadoEm >= deUtc && a.AtualizadoEm < ateUtc)
                || (a.FechadoEm >= deUtc && a.FechadoEm < ateUtc))
            .ToList());

    public Task AdicionarAsync(Aviso aviso, CancellationToken cancellationToken = default)
    {
        Avisos.Add(aviso);
        return Task.CompletedTask;
    }
}

internal sealed class ConfiguracaoLojaRepositoryFake : IConfiguracaoLojaRepository
{
    public ConfiguracaoLoja? Configuracao { get; set; }

    public Task<ConfiguracaoLoja?> ObterAsync(CancellationToken cancellationToken = default) => Task.FromResult(Configuracao);

    public Task AdicionarAsync(ConfiguracaoLoja configuracao, CancellationToken cancellationToken = default)
    {
        Configuracao = configuracao;
        return Task.CompletedTask;
    }
}

/// <summary>Contagens semanais em memória.</summary>
internal sealed class ContagemRepositoryFake : IContagemRepository
{
    public List<Contagem> Contagens { get; } = new();

    public Task<Contagem?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Contagens.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Contagem>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Contagem>>(Contagens
            .Where(c => !c.Concluida || (c.IniciadaEm >= deUtc && c.IniciadaEm < ateUtc))
            .OrderByDescending(c => c.IniciadaEm)
            .ToList());

    public Task<IReadOnlyDictionary<Guid, UltimaContagemDoItem>> UltimaContagemPorItemAsync(DateTime antesDeUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, UltimaContagemDoItem>>(Contagens
            .Where(c => c.ConcluidaEm < antesDeUtc)
            .SelectMany(c => c.Itens.Where(i => i.ContadoUnidades is not null).Select(i => (c.ConcluidaEm, i.ItemEstoqueId, i.ContadoUnidades, i.ContadoEm)))
            .GroupBy(x => x.ItemEstoqueId)
            .ToDictionary(g => g.Key, g =>
            {
                var ultima = g.OrderByDescending(x => x.ConcluidaEm).First();
                return new UltimaContagemDoItem(ultima.ContadoUnidades!.Value, ultima.ConcluidaEm!.Value, ultima.ContadoEm);
            }));

    public Task AdicionarAsync(Contagem contagem, CancellationToken cancellationToken = default)
    {
        Contagens.Add(contagem);
        return Task.CompletedTask;
    }
}

/// <summary>Pedidos aos fornecedores em memória.</summary>
internal sealed class PedidoFornecedorRepositoryFake : IPedidoFornecedorRepository
{
    public List<PedidoFornecedor> Pedidos { get; } = new();

    public Task<PedidoFornecedor?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pedidos.FirstOrDefault(p => p.Id == id));

    public Task<PedidoFornecedor?> ObterRascunhoAsync(Guid fornecedorId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pedidos.FirstOrDefault(p => p.FornecedorId == fornecedorId && p.Status == StatusPedido.Rascunho));

    public Task<IReadOnlyList<PedidoFornecedor>> ListarAsync(Guid? fornecedorId, DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PedidoFornecedor>>(Pedidos
            .Where(p => fornecedorId is null || p.FornecedorId == fornecedorId)
            .Where(p => p.CriadoEm >= desdeUtc || p.Status is StatusPedido.Rascunho or StatusPedido.Enviado)
            .OrderByDescending(p => p.CriadoEm)
            .ToList());

    public Task<IReadOnlyList<PedidoFornecedor>> ListarRecebidosEntreAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PedidoFornecedor>>(Pedidos
            .Where(p => p.Status == StatusPedido.Recebido && p.RecebidoEm >= deUtc && p.RecebidoEm < ateUtc)
            .ToList());

    public Task<int> ProximoNumeroAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Pedidos.Count == 0 ? 1 : Pedidos.Max(p => p.Numero) + 1);

    public Task AdicionarAsync(PedidoFornecedor pedido, CancellationToken cancellationToken = default)
    {
        Pedidos.Add(pedido);
        return Task.CompletedTask;
    }
}

internal sealed class MovimentoEstoqueRepositoryFake : IMovimentoEstoqueRepository
{
    public List<MovimentoEstoque> Movimentos { get; } = new();

    public Task AdicionarAsync(MovimentoEstoque movimento, CancellationToken cancellationToken = default)
    {
        // Mesmos índices únicos do banco: uma baixa/estorno por linha de venda, uma entrada por item e pedido.
        if (Movimentos.Any(m => m.Tipo == movimento.Tipo
                && ((movimento.ItemComandaId is not null && m.ItemComandaId == movimento.ItemComandaId)
                    || (movimento.PedidoId is not null && m.PedidoId == movimento.PedidoId && m.ItemEstoqueId == movimento.ItemEstoqueId))))
        {
            throw new InvalidOperationException("Movimento repetido (índice único).");
        }

        Movimentos.Add(movimento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<MovimentoEstoque>> ListarDosItensAsync(
        IReadOnlyCollection<Guid> itemEstoqueIds, DateTime? desdeUtc = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimentoEstoque>>(Movimentos
            .Where(m => itemEstoqueIds.Contains(m.ItemEstoqueId) && (desdeUtc is null || m.Em > desdeUtc))
            .OrderBy(m => m.Em)
            .ToList());

    public Task<IReadOnlyList<MovimentoEstoque>> ListarDaComandaAsync(Guid comandaId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MovimentoEstoque>>(Movimentos.Where(m => m.ComandaId == comandaId).ToList());
}

/// <summary>Saldo dos potes com os fakes: passe os que o teste olha, o resto nasce vazio.</summary>
internal static class SaldosFake
{
    public static SaldoEstoqueService Novo(
        IClock clock,
        IEstoqueRepository? estoque = null,
        IContagemRepository? contagens = null,
        IAvisoRepository? avisos = null,
        IMovimentoEstoqueRepository? movimentos = null) =>
        new(
            movimentos ?? new MovimentoEstoqueRepositoryFake(),
            contagens ?? new ContagemRepositoryFake(),
            estoque ?? new EstoqueRepositoryFake(),
            avisos ?? new AvisoRepositoryFake(),
            clock);
}
