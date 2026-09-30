namespace GestaoSorveteria.Domain.Common;

/// <summary>
/// Validações curtas usadas pelas entidades. Toda falha vira DomainException
/// com mensagem pronta para o usuário.
/// </summary>
internal static class Guard
{
    public static void Contra(bool condicao, string mensagem)
    {
        if (condicao)
        {
            throw new DomainException(mensagem);
        }
    }

    public static string Texto(string? valor, string campo, int min, int max)
    {
        var texto = (valor ?? string.Empty).Trim();
        Contra(texto.Length < min, min <= 1
            ? $"Informe {campo}."
            : $"{Capitalizar(campo)} deve ter pelo menos {min} caracteres.");
        Contra(texto.Length > max, $"{Capitalizar(campo)} deve ter no máximo {max} caracteres.");
        return texto;
    }

    public static string? TextoOpcional(string? valor, string campo, int max)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();
        Contra(texto.Length > max, $"{Capitalizar(campo)} deve ter no máximo {max} caracteres.");
        return texto;
    }

    public static decimal Dinheiro(decimal valor, string campo, bool permiteZero)
    {
        Contra(valor < 0, $"{Capitalizar(campo)} não pode ser negativo.");
        Contra(!permiteZero && valor == 0, $"{Capitalizar(campo)} deve ser maior que zero.");
        Contra(!Moeda.TemNoMaximoDuasCasas(valor), $"{Capitalizar(campo)} deve ter no máximo 2 casas decimais.");
        return valor;
    }

    public static DateTime Utc(DateTime data, string campo)
    {
        Contra(data.Kind != DateTimeKind.Utc, $"{Capitalizar(campo)} deve estar em UTC (RN-TD-01).");
        return data;
    }

    public static Guid NaoVazio(Guid id, string campo)
    {
        Contra(id == Guid.Empty, $"Informe {campo}.");
        return id;
    }

    private static string Capitalizar(string texto) =>
        string.IsNullOrEmpty(texto) ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
