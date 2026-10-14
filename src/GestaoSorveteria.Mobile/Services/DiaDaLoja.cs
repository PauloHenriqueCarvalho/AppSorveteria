namespace SorveteriaMaui.Services;

/// <summary>
/// RN-TD-01: "hoje" é o dia da loja (America/Sao_Paulo), não o fuso que estiver configurado no tablet. Mesma conta do
/// <c>DiaComercial</c> da API (que fica na camada Application, que o app não referencia).
/// </summary>
public static class DiaDaLoja
{
    public const string FusoIana = "America/Sao_Paulo";

    public static readonly TimeZoneInfo Fuso = ObterFuso();

    /// <summary>Hora na loja de um instante UTC.</summary>
    public static DateTime HoraLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso);

    /// <summary>Dia da semana na loja agora (para "Hoje é dia de contagem", RN-ES-05).</summary>
    public static DayOfWeek DiaDaSemanaHoje() => HoraLocal(DateTime.UtcNow).DayOfWeek;

    /// <summary>Data de hoje na loja.</summary>
    public static DateOnly Hoje() => DateOnly.FromDateTime(HoraLocal(DateTime.UtcNow));

    /// <summary>Data na loja de um instante UTC.</summary>
    public static DateOnly DataDe(DateTime utc) => DateOnly.FromDateTime(HoraLocal(utc));

    /// <summary>Início (00:00 na loja) do dia de hoje, em UTC.</summary>
    public static DateTime InicioDeHojeUtc()
    {
        var meiaNoite = Hoje().ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(meiaNoite, Fuso);
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
                return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time"); // Windows (teste no PC)
            }
            catch (Exception ex2) when (ex2 is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // Último recurso: UTC−3 fixo (sem horário de verão), o caso do Brasil desde 2019.
                return TimeZoneInfo.CreateCustomTimeZone(FusoIana, TimeSpan.FromHours(-3), "Brasília", "Brasília");
            }
        }
    }
}
