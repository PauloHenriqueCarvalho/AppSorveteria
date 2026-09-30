using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
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
        Task.FromResult<IReadOnlyList<Comanda>>(Comandas.Where(c => c.EstaFechada && c.FechadaEm >= deUtc && c.FechadaEm < ateUtc).ToList());

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
        Task.FromResult(Comandas.Where(c => c.CaixaId == caixaId && c.EstaFechada).Sum(c => c.TotalEmDinheiro));

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
