using GestaoSorveteria.Application.Abstractions;
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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Confirmacoes++;
        return Task.FromResult(1);
    }
}
