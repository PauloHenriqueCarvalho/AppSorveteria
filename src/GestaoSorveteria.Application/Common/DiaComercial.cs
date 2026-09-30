namespace GestaoSorveteria.Application.Common;

/// <summary>
/// RN-TD-01 / RN-RL-02: o banco guarda UTC; "hoje", "esta semana" e "este mês" são calculados
/// no fuso da sorveteria (America/Sao_Paulo, UTC−3, sem horário de verão desde 2019).
/// </summary>
public static class DiaComercial
{
    public const string FusoIana = "America/Sao_Paulo";
    private const string FusoWindows = "E. South America Standard Time";

    public static readonly TimeZoneInfo Fuso = ObterFuso();

    /// <summary>Data comercial de um instante UTC.</summary>
    public static DateOnly DataDe(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);
        return DateOnly.FromDateTime(local);
    }

    /// <summary>Intervalo UTC [início, fim) de um dia comercial.</summary>
    public static (DateTime InicioUtc, DateTime FimUtc) IntervaloUtc(DateOnly dia) =>
        IntervaloUtc(dia, dia);

    /// <summary>Intervalo UTC [início do primeiro dia, início do dia seguinte ao último).</summary>
    public static (DateTime InicioUtc, DateTime FimUtc) IntervaloUtc(DateOnly primeiroDia, DateOnly ultimoDia)
    {
        if (ultimoDia < primeiroDia)
        {
            throw new ArgumentException("O último dia não pode ser anterior ao primeiro.", nameof(ultimoDia));
        }

        var inicioLocal = primeiroDia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var fimLocal = ultimoDia.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return (
            TimeZoneInfo.ConvertTimeToUtc(inicioLocal, Fuso),
            TimeZoneInfo.ConvertTimeToUtc(fimLocal, Fuso));
    }

    private static TimeZoneInfo ObterFuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(FusoIana);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(FusoWindows);
            }
            catch (Exception ex2) when (ex2 is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Último recurso: UTC−3 fixo (sem horário de verão), que é o caso do Brasil desde 2019.
                return TimeZoneInfo.CreateCustomTimeZone(FusoIana, TimeSpan.FromHours(-3), "Brasília", "Brasília");
            }
        }
    }
}
