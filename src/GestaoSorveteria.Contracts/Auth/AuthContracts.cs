using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Auth;

/// <summary>POST /api/auth/login</summary>
public sealed record LoginRequest(
    [Required(ErrorMessage = "Informe o login.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "O login deve ter de 3 a 50 caracteres.")]
    string Login,
    [Required(ErrorMessage = "Informe a senha ou o PIN.")]
    [StringLength(100, ErrorMessage = "A senha deve ter no máximo 100 caracteres.")]
    string Senha);

public sealed record LoginResponse(string Token, DateTime ExpiraEmUtc, UsuarioDto Usuario);

public sealed record UsuarioDto(Guid Id, string Nome, string Login, string Perfil, bool Ativo);
