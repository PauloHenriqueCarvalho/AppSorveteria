namespace GestaoSorveteria.Domain.Common;

/// <summary>
/// Regras de dinheiro (RN-TD-02): sempre decimal, 2 casas, arredondamento comercial.
/// </summary>
public static class Moeda
{
    public const int Casas = 2;

    /// <summary>Maior valor digitado aceito (preço, pagamento, fundo, sangria...). Folga para subtotal e total.</summary>
    public const decimal ValorMaximo = 9_999_999.99m;

    /// <summary>Maior valor que cabe em numeric(12,2) (RN-TD-02): subtotal e total não podem passar disso.</summary>
    public const decimal LimiteColuna = 9_999_999_999.99m;

    public static decimal Arredondar(decimal valor) =>
        Math.Round(valor, Casas, MidpointRounding.AwayFromZero);

    /// <summary>Verdadeiro quando o valor não tem mais que 2 casas decimais.</summary>
    public static bool TemNoMaximoDuasCasas(decimal valor) => Arredondar(valor) == valor;
}
