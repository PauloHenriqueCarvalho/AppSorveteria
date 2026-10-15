namespace GestaoSorveteria.Domain.Common;

/// <summary>Número de WhatsApp guardado só com dígitos: DDD + número (10 ou 11) ou com o 55 do Brasil (12 ou 13).</summary>
public static class Telefone
{
    /// <summary>"(11) 98765-4321" → "11987654321"; vazio → null; tamanho errado → <see cref="DomainException"/>.</summary>
    public static string? NormalizarWhatsApp(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        Guard.Contra(digitos.Length is < 10 or > 13, "WhatsApp inválido: use DDD + número, ex.: (11) 98765-4321.");
        // 12 ou 13 dígitos só com o 55 do Brasil na frente; "011 98765-4321" (zero de operadora) não é aceito.
        Guard.Contra(digitos.Length >= 12 && !digitos.StartsWith("55", StringComparison.Ordinal),
            "WhatsApp inválido: use DDD + número sem o zero da operadora, ex.: (11) 98765-4321.");
        return digitos;
    }

    /// <summary>Número completo para o link do WhatsApp (wa.me exige o código do país): sem o 55, ele é acrescentado.</summary>
    public static string ParaLinkWhatsApp(string digitos) => digitos.Length <= 11 ? $"55{digitos}" : digitos;
}
