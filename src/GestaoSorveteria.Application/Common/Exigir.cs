using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Application.Common;

/// <summary>
/// Validação de entrada na camada de aplicação (o <c>Guard</c> do Domain é interno a ele).
/// Lança <see cref="DomainException"/>, que vira 400 com a mensagem pronta para a tela.
/// </summary>
internal static class Exigir
{
    public static void Que(bool condicao, string mensagem)
    {
        if (!condicao)
        {
            throw new DomainException(mensagem);
        }
    }

    public static string Texto(string? valor, string campo, int min, int max)
    {
        var texto = (valor ?? string.Empty).Trim();
        Que(texto.Length >= min, texto.Length == 0 ? $"Informe {campo}." : $"{campo} deve ter pelo menos {min} caracteres.");
        Que(texto.Length <= max, $"{campo} deve ter no máximo {max} caracteres.");
        return texto;
    }
}
