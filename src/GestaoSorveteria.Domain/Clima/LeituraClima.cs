using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Clima;

/// <summary>RN-CL-02: de onde veio o clima do dia.</summary>
public enum OrigemClima
{
    /// <summary>Medido por estações.</summary>
    Observado,

    /// <summary>Observação completada por satélite/modelo (comum nos dias recentes e onde não há estação perto).</summary>
    Combinado,

    /// <summary>O dia ainda não aconteceu (ou só começou): é previsão.</summary>
    Previsto,
}

/// <summary>
/// O que o serviço de clima informou para um dia (passado, hoje ou futuro). Temperaturas em °C; chuva em mm; percentuais de 0 a
/// 100. Vira <see cref="ClimaDia"/> (hoje ou antes) ou <see cref="PrevisaoClima"/> (dias seguintes), que validam e arredondam.
/// </summary>
public sealed record LeituraClima(
    DateOnly Data,
    decimal TempMax,
    decimal TempMin,
    decimal TempMedia,
    decimal? SensacaoMax,
    decimal? ChuvaMm,
    decimal? ChuvaHorasPct,
    decimal? NuvensPct,
    decimal? UmidadePct,
    decimal? ChanceChuvaPct,
    string? Icone,
    string? Condicao,
    OrigemClima Origem);

/// <summary>Validação e arredondamento dos valores de clima (1 casa, como o painel mostra).</summary>
internal static class ValoresClima
{
    public const int MaximoIcone = 40;
    public const int MaximoCondicao = 120;
    public const int MaximoFonte = 20;

    public static decimal Temperatura(decimal valor, DateOnly dia, string campo)
    {
        Guard.Contra(valor is < -60m or > 60m, $"Temperatura {campo} fora do normal em {dia:dd/MM/yyyy}: {valor} °C.");
        return Arredondar(valor);
    }

    public static decimal? TemperaturaOpcional(decimal? valor, DateOnly dia, string campo) =>
        valor is { } v ? Temperatura(v, dia, campo) : null;

    public static (decimal Max, decimal Min) MaximaEMinima(decimal max, decimal min, DateOnly dia)
    {
        var maxima = Temperatura(max, dia, "máxima");
        var minima = Temperatura(min, dia, "mínima");
        Guard.Contra(maxima < minima, $"A máxima ({maxima} °C) ficou abaixo da mínima ({minima} °C) em {dia:dd/MM/yyyy}.");
        return (maxima, minima);
    }

    public static decimal? Chuva(decimal? mm, DateOnly dia)
    {
        if (mm is not { } valor)
        {
            return null;
        }

        Guard.Contra(valor is < 0m or > 2000m, $"Chuva fora do normal em {dia:dd/MM/yyyy}: {valor} mm.");
        return Arredondar(valor);
    }

    /// <summary>
    /// Percentual opcional (umidade, nuvens, chance de chuva): o modelo às vezes passa um pouco de 100 (100,2) — fica limitado a
    /// 0–100 em vez de recusar o dia inteiro, que seria pedido de novo a cada rodada.
    /// </summary>
    public static decimal? Percentual(decimal? valor) =>
        valor is { } v ? Arredondar(Math.Clamp(v, 0m, 100m)) : null;

    /// <summary>Texto que vem de fora: sem espaços nas pontas e cortado no tamanho da coluna (não recusa o dia por isso).</summary>
    public static string? Texto(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();
        return texto.Length <= maximo ? texto : texto[..maximo];
    }

    public static string Fonte(string fonte) => Guard.Texto(fonte, "a fonte do clima", 2, MaximoFonte);

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 1, MidpointRounding.AwayFromZero);
}
