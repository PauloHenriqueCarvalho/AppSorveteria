using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Application.Sync;

/// <summary>
/// Converte o texto de enum dos Contracts (ex.: "Sangria", "Dinheiro") no enum do Domain.
/// Só aceita o nome; número ("1") ou valor fora da lista vira <see cref="DomainException"/> (item rejeitado no lote).
/// </summary>
internal static class TextoEnum
{
    public static T Converter<T>(string? texto, string campo) where T : struct, Enum
    {
        var valor = (texto ?? string.Empty).Trim();
        if (valor.Length == 0 || char.IsDigit(valor[0]) || valor[0] is '-' or '+'
            || !Enum.TryParse<T>(valor, ignoreCase: true, out var resultado) || !Enum.IsDefined(resultado))
        {
            throw new DomainException($"{campo} desconhecido(a): \"{texto}\". Use: {string.Join(", ", Enum.GetNames<T>())}.");
        }

        return resultado;
    }
}
