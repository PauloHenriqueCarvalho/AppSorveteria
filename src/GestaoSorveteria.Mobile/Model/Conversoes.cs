using System.Globalization;
using GestaoSorveteria.Domain.Common;

namespace SorveteriaMaui.Model;

/// <summary>
/// Conversões das tabelas do SQLite. O sqlite-net gravaria <c>decimal</c> como REAL (ponto flutuante)
/// e <c>DateTime</c> sem o Kind; por isso dinheiro fica em centavos (INTEGER) e datas voltam sempre como UTC.
/// </summary>
public static class Conversoes
{
    /// <summary>RN-TD-02 / B6: 2 casas, arredondamento comercial.</summary>
    public static long ParaCentavos(decimal valor) => (long)(Moeda.Arredondar(valor) * 100m);

    public static decimal ParaReais(long centavos) => centavos / 100m;

    /// <summary>RN-TD-01 / B14: o que está gravado é UTC; o sqlite-net devolve com Kind Unspecified.</summary>
    public static DateTime ComoUtc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Utc => valor,
        DateTimeKind.Local => valor.ToUniversalTime(),
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
    };

    public static DateTime? ComoUtc(DateTime? valor) => valor is { } data ? ComoUtc(data) : null;

    /// <summary>Lê o valor digitado pelo atendente ("15,50", "15.50" ou "R$ 15,50").</summary>
    public static bool TentarLerDinheiro(string? texto, out decimal valor)
    {
        valor = 0m;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var normalizado = texto.Replace("R$", string.Empty).Trim().Replace(",", ".");
        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var lido))
        {
            return false;
        }

        valor = lido;
        return true;
    }
}
