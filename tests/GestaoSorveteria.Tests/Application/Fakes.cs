using GestaoSorveteria.Application.Abstractions;
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
