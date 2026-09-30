using System.Text.RegularExpressions;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Usuarios;

/// <summary>
/// Atendente ou Admin (a dona). Nunca é apagado: desativa (RN-US-07).
/// </summary>
public sealed class Usuario : Entity
{
    private static readonly Regex LoginValido = new("^[a-z0-9._]+$", RegexOptions.Compiled);

    public string Nome { get; private set; }
    public string Login { get; private set; }
    public string SenhaHash { get; private set; }
    public PerfilUsuario Perfil { get; private set; }
    public bool Ativo { get; private set; }
    public DateTime CriadoEm { get; private set; }

    // EF Core
    private Usuario()
    {
        Nome = string.Empty;
        Login = string.Empty;
        SenhaHash = string.Empty;
    }

    private Usuario(Guid? id, string nome, string login, string senhaHash, PerfilUsuario perfil, DateTime criadoEmUtc)
        : base(id)
    {
        Nome = nome;
        Login = login;
        SenhaHash = senhaHash;
        Perfil = perfil;
        Ativo = true;
        CriadoEm = criadoEmUtc;
    }

    /// <param name="senhaHash">Hash já calculado (RN-US-04). A validação da senha em texto é feita com <see cref="PoliticaSenha"/> antes do hash.</param>
    public static Usuario Criar(string nome, string login, string senhaHash, PerfilUsuario perfil, DateTime criadoEmUtc, Guid? id = null)
    {
        var nomeValido = Guard.Texto(nome, "o nome", 2, 100);
        var loginValido = ValidarLogin(login);
        Guard.Contra(string.IsNullOrWhiteSpace(senhaHash), "Hash de senha inválido.");
        Guard.Contra(!Enum.IsDefined(perfil), "Perfil de usuário desconhecido.");

        return new Usuario(id, nomeValido, loginValido, senhaHash, perfil, Guard.Utc(criadoEmUtc, "a data de criação"));
    }

    /// <summary>RN-US-02: login minúsculo, sem espaços, 3–50 caracteres.</summary>
    public static string NormalizarLogin(string? login) => (login ?? string.Empty).Trim().ToLowerInvariant();

    public static string ValidarLogin(string? login)
    {
        var normalizado = NormalizarLogin(login);
        Guard.Contra(normalizado.Length < 3 || normalizado.Length > 50, "O login deve ter de 3 a 50 caracteres.");
        Guard.Contra(!LoginValido.IsMatch(normalizado), "O login só pode ter letras minúsculas, números, ponto e sublinhado.");
        return normalizado;
    }

    public void Renomear(string nome) => Nome = Guard.Texto(nome, "o nome", 2, 100);

    public void DefinirSenhaHash(string senhaHash)
    {
        Guard.Contra(string.IsNullOrWhiteSpace(senhaHash), "Hash de senha inválido.");
        SenhaHash = senhaHash;
    }

    public void Ativar() => Ativo = true;

    public void Desativar() => Ativo = false;
}
