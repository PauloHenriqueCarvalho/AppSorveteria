using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Abstractions;

public sealed record TokenGerado(string Token, DateTime ExpiraEmUtc);

/// <summary>Gera o token de acesso do app (JWT no Server).</summary>
public interface ITokenService
{
    TokenGerado Gerar(Usuario usuario);
}
