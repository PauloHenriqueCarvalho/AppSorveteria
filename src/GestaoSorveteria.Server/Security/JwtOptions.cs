namespace GestaoSorveteria.Server.Security;

/// <summary>Seção "Jwt" da configuração. Em produção a chave vem da variável de ambiente Jwt__Key.</summary>
public sealed class JwtOptions
{
    public const string Secao = "Jwt";
    public const int TamanhoMinimoChave = 32;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "GestaoSorveteria";
    public string Audience { get; set; } = "GestaoSorveteria";

    /// <summary>RN-US-06: validade de um turno.</summary>
    public int ExpiracaoHoras { get; set; } = 12;

    public void Validar()
    {
        if (string.IsNullOrWhiteSpace(Key) || Key.Length < TamanhoMinimoChave)
        {
            throw new InvalidOperationException(
                $"Configure Jwt:Key com pelo menos {TamanhoMinimoChave} caracteres (appsettings.Development.json em dev; variável Jwt__Key em produção).");
        }

        if (ExpiracaoHoras is < 1 or > 24 * 7)
        {
            throw new InvalidOperationException("Jwt:ExpiracaoHoras deve ficar entre 1 e 168 horas.");
        }
    }
}
