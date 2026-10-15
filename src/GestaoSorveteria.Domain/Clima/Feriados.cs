namespace GestaoSorveteria.Domain.Clima;

/// <summary>Um feriado nacional (RN-CL-05). <paramref name="PontoFacultativo"/>: Carnaval e Corpus Christi.</summary>
public sealed record Feriado(DateOnly Data, string Nome, bool PontoFacultativo);

/// <summary>
/// RN-CL-05: feriados nacionais calculados, sem cadastro — os fixos e os que dependem da Páscoa. Estadual, municipal e evento
/// da cidade ficam para a anotação do dia pela dona.
/// </summary>
public static class Feriados
{
    /// <summary>Domingo de Páscoa (calendário gregoriano; algoritmo de Meeus/Jones/Butcher).</summary>
    public static DateOnly Pascoa(int ano)
    {
        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(ano, mes, dia);
    }

    /// <summary>Os feriados nacionais do ano, em ordem. Dois no mesmo dia viram um só, com os nomes juntos.</summary>
    public static IReadOnlyList<Feriado> Nacionais(int ano)
    {
        var pascoa = Pascoa(ano);
        var feriados = new List<Feriado>
        {
            new(new DateOnly(ano, 1, 1), "Confraternização Universal", false),
            new(pascoa.AddDays(-48), "Carnaval", true),
            new(pascoa.AddDays(-47), "Carnaval", true),
            new(pascoa.AddDays(-2), "Sexta-feira Santa", false),
            new(new DateOnly(ano, 4, 21), "Tiradentes", false),
            new(new DateOnly(ano, 5, 1), "Dia do Trabalho", false),
            new(pascoa.AddDays(60), "Corpus Christi", true),
            new(new DateOnly(ano, 9, 7), "Independência do Brasil", false),
            new(new DateOnly(ano, 10, 12), "Nossa Senhora Aparecida", false),
            new(new DateOnly(ano, 11, 2), "Finados", false),
            new(new DateOnly(ano, 11, 15), "Proclamação da República", false),
            new(new DateOnly(ano, 12, 25), "Natal", false),
        };

        // Lei 14.759/2023: feriado nacional a partir de 2024.
        if (ano >= 2024)
        {
            feriados.Add(new Feriado(new DateOnly(ano, 11, 20), "Dia Nacional de Zumbi e da Consciência Negra", false));
        }

        return feriados
            .GroupBy(f => f.Data)
            .Select(g => new Feriado(g.Key, string.Join(" / ", g.Select(f => f.Nome)), g.All(f => f.PontoFacultativo)))
            .OrderBy(f => f.Data)
            .ToList();
    }

    /// <summary>O feriado nacional do dia, se houver.</summary>
    public static Feriado? Do(DateOnly dia) => Nacionais(dia.Year).FirstOrDefault(f => f.Data == dia);
}
