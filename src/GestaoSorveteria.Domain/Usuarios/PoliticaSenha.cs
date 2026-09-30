using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Usuarios;

/// <summary>
/// RN-US-03: atendente usa PIN numérico de 4 a 6 dígitos; Admin usa senha de no mínimo 8 caracteres.
/// Validada antes de gerar o hash (Application/seed).
/// </summary>
public static class PoliticaSenha
{
    public const int PinTamanhoMinimo = 4;
    public const int PinTamanhoMaximo = 6;
    public const int SenhaAdminTamanhoMinimo = 8;
    public const int TamanhoMaximo = 100;

    public static void Validar(PerfilUsuario perfil, string? senha)
    {
        Guard.Contra(string.IsNullOrWhiteSpace(senha), "Informe a senha ou o PIN.");
        var valor = senha!;
        Guard.Contra(valor.Length > TamanhoMaximo, $"A senha deve ter no máximo {TamanhoMaximo} caracteres.");

        switch (perfil)
        {
            case PerfilUsuario.Atendente:
                Guard.Contra(
                    valor.Length < PinTamanhoMinimo || valor.Length > PinTamanhoMaximo || !valor.All(char.IsAsciiDigit),
                    $"O PIN do atendente deve ter de {PinTamanhoMinimo} a {PinTamanhoMaximo} dígitos numéricos.");
                break;

            case PerfilUsuario.Admin:
                Guard.Contra(
                    valor.Trim().Length < SenhaAdminTamanhoMinimo,
                    $"A senha do administrador deve ter no mínimo {SenhaAdminTamanhoMinimo} caracteres.");
                break;

            default:
                throw new DomainException("Perfil de usuário desconhecido.");
        }
    }
}
