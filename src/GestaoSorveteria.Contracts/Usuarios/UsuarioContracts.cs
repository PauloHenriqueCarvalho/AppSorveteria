using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Usuarios;

/// <summary>
/// POST /api/usuarios (Admin). <c>Perfil</c> é "Atendente" ou "Admin" (RN-US-01).
/// <c>Senha</c> é o PIN de 4 a 6 dígitos do atendente ou a senha de 8+ caracteres do Admin (RN-US-03);
/// a validação de verdade é a <c>PoliticaSenha</c> do Domain — aqui só a borda (400 antes de chegar ao domínio).
/// </summary>
public sealed record CriarUsuarioRequest(
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O nome deve ter de 2 a 100 caracteres.")]
    string Nome,
    [Required(ErrorMessage = "Informe o login.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "O login deve ter de 3 a 50 caracteres.")]
    string Login,
    [Required(ErrorMessage = "Informe a senha ou o PIN.")]
    [StringLength(100, ErrorMessage = "A senha deve ter no máximo 100 caracteres.")]
    string Senha,
    [Required(ErrorMessage = "Informe o perfil (Atendente ou Admin).")]
    [StringLength(20, ErrorMessage = "Perfil inválido.")]
    string Perfil);

/// <summary>POST /api/usuarios/{id}/redefinir-senha (Admin, RN-US-08). Troca o PIN/senha sem precisar da antiga.</summary>
public sealed record RedefinirSenhaRequest(
    [Required(ErrorMessage = "Informe a nova senha ou o novo PIN.")]
    [StringLength(100, ErrorMessage = "A senha deve ter no máximo 100 caracteres.")]
    string NovaSenha);
