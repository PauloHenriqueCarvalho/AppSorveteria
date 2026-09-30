using System.Security.Claims;
using System.Text;
using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GestaoSorveteria.Server.Security;

/// <summary>Gera o JWT do app (RN-US-06). Claims curtas: sub, name, login, role.</summary>
public sealed class JwtTokenService : ITokenService
{
    public const string ClaimId = "sub";
    public const string ClaimNome = "name";
    public const string ClaimLogin = "login";
    public const string ClaimPerfil = "role";

    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _credenciais;

    public JwtTokenService(JwtOptions options, IClock clock)
    {
        _options = options;
        _clock = clock;
        _credenciais = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)),
            SecurityAlgorithms.HmacSha256);
    }

    public TokenGerado Gerar(Usuario usuario)
    {
        var agora = _clock.UtcNow;
        var expira = agora.AddHours(_options.ExpiracaoHoras);

        var identidade = new ClaimsIdentity(new[]
        {
            new Claim(ClaimId, usuario.Id.ToString()),
            new Claim(ClaimNome, usuario.Nome),
            new Claim(ClaimLogin, usuario.Login),
            new Claim(ClaimPerfil, usuario.Perfil.ToString()),
        });

        var descritor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = identidade,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expira,
            SigningCredentials = _credenciais,
        };

        var handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false };
        var token = handler.CreateToken(descritor);

        return new TokenGerado(token, expira);
    }
}
