using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Auth;

/// <summary>
/// Caso de uso de login (RN-US-*). Devolve null quando as credenciais são inválidas
/// ou o usuário está desativado — a API responde 401 sem dizer qual dos dois.
/// </summary>
public sealed class AuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly ITokenService _tokens;

    public AuthService(IUsuarioRepository usuarios, IPasswordHasher hasher, ITokenService tokens)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var login = Usuario.NormalizarLogin(request.Login);
        if (login.Length == 0 || string.IsNullOrEmpty(request.Senha))
        {
            return null;
        }

        var usuario = await _usuarios.ObterPorLoginAsync(login, cancellationToken);
        if (usuario is null || !usuario.Ativo)
        {
            return null;
        }

        if (!_hasher.Verificar(request.Senha, usuario.SenhaHash))
        {
            return null;
        }

        var token = _tokens.Gerar(usuario);
        return new LoginResponse(token.Token, token.ExpiraEmUtc, usuario.ToDto());
    }
}
