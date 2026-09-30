namespace GestaoSorveteria.Domain.Common;

/// <summary>
/// Regras de dinheiro (RN-TD-02): sempre decimal, 2 casas, arredondamento comercial.
/// </summary>
public static class Moeda
{
    public const int Casas = 2;

    public static decimal Arredondar(decimal valor) =>
        Math.Round(valor, Casas, MidpointRounding.AwayFromZero);

    /// <summary>Verdadeiro quando o valor não tem mais que 2 casas decimais.</summary>
    public static bool TemNoMaximoDuasCasas(decimal valor) => Arredondar(valor) == valor;
}
