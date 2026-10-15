using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Clima;

/// <summary>
/// RN-CL-03: a previsão de um dia como estava no dia em que foi guardada. Nunca é sobrescrita: cada dia de coleta guarda a sua,
/// para medir quanto a previsão erra e para a estimativa futura usar a mesma informação que a dona tem na hora de pedir.
/// </summary>
public sealed class PrevisaoClima : Entity
{
    /// <summary>Quantos dias à frente são guardados.</summary>
    public const int DiasAFrente = 14;

    /// <summary>Dia comercial em que a previsão foi guardada.</summary>
    public DateOnly FeitaNoDia { get; private set; }

    public DateTime FeitaEm { get; private set; }

    /// <summary>Dia previsto.</summary>
    public DateOnly Data { get; private set; }

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public decimal TempMax { get; private set; }

    public decimal TempMin { get; private set; }

    public decimal? ChuvaMm { get; private set; }

    public decimal? ChanceChuvaPct { get; private set; }

    public string? Icone { get; private set; }

    public string? Condicao { get; private set; }

    public string Fonte { get; private set; }

    /// <summary>Quantos dias antes do dia previsto (1 = véspera).</summary>
    public int Antecedencia => Data.DayNumber - FeitaNoDia.DayNumber;

    // EF Core
    private PrevisaoClima()
    {
        Fonte = string.Empty;
    }

    private PrevisaoClima(PontoClima ponto, DateOnly feitaNoDia, DateTime feitaEm, DateOnly data, string fonte) : base(null)
    {
        Latitude = ponto.Latitude;
        Longitude = ponto.Longitude;
        FeitaNoDia = feitaNoDia;
        FeitaEm = feitaEm;
        Data = data;
        Fonte = fonte;
    }

    /// <exception cref="DomainException">Dia previsto que não é dos próximos 14 dias ou valores fora do normal.</exception>
    public static PrevisaoClima Registrar(PontoClima ponto, DateOnly feitaNoDia, DateTime feitaEmUtc, LeituraClima leitura, string fonte)
    {
        Guard.Contra(leitura.Data <= feitaNoDia, "A previsão guardada é só dos dias seguintes; hoje e antes ficam no clima do dia.");
        Guard.Contra(leitura.Data.DayNumber > feitaNoDia.DayNumber + DiasAFrente, $"A previsão guardada vai até {DiasAFrente} dias à frente.");
        var feitaEm = Guard.Utc(feitaEmUtc, "a hora da previsão");
        var (maxima, minima) = ValoresClima.MaximaEMinima(leitura.TempMax, leitura.TempMin, leitura.Data);

        return new PrevisaoClima(ponto, feitaNoDia, feitaEm, leitura.Data, ValoresClima.Fonte(fonte))
        {
            TempMax = maxima,
            TempMin = minima,
            ChuvaMm = ValoresClima.Chuva(leitura.ChuvaMm, leitura.Data),
            ChanceChuvaPct = ValoresClima.Percentual(leitura.ChanceChuvaPct),
            Icone = ValoresClima.Texto(leitura.Icone, ValoresClima.MaximoIcone),
            Condicao = ValoresClima.Texto(leitura.Condicao, ValoresClima.MaximoCondicao),
        };
    }
}
