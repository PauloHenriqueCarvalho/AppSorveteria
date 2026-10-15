using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Clima;

/// <summary>
/// RN-CL-02: o clima de um dia comercial (America/Sao_Paulo) num ponto. Coletado de novo enquanto não é definitivo; fica
/// definitivo quando o dia terminou há pelo menos <see cref="DiasParaDefinitivo"/> dias e não é previsão — depois nunca muda.
/// </summary>
public sealed class ClimaDia : Entity
{
    /// <summary>Os serviços corrigem os dados recentes: o dia só fecha 2 dias depois.</summary>
    public const int DiasParaDefinitivo = 2;

    /// <summary>RN-CL-04: dia ainda não definitivo coletado há menos que isso espera a próxima rodada.</summary>
    public static readonly TimeSpan IntervaloRecoleta = TimeSpan.FromHours(3);

    public DateOnly Data { get; private set; }

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public decimal TempMax { get; private set; }

    public decimal TempMin { get; private set; }

    public decimal TempMedia { get; private set; }

    public decimal? SensacaoMax { get; private set; }

    /// <summary>Chuva do dia em mm; nulo = o serviço não informou.</summary>
    public decimal? ChuvaMm { get; private set; }

    /// <summary>Parte do dia com chuva (%).</summary>
    public decimal? ChuvaHorasPct { get; private set; }

    public decimal? NuvensPct { get; private set; }

    public decimal? UmidadePct { get; private set; }

    /// <summary>Código estável da condição (ex.: "rain", "clear-day"), para o ícone do painel.</summary>
    public string? Icone { get; private set; }

    /// <summary>Texto da condição como o serviço escreveu (ex.: "Chuva, Parcialmente nublado").</summary>
    public string? Condicao { get; private set; }

    public OrigemClima Origem { get; private set; }

    public bool Definitivo { get; private set; }

    public string Fonte { get; private set; }

    public DateTime ColetadoEm { get; private set; }

    public PontoClima Ponto => PontoClima.De(Latitude, Longitude);

    // EF Core
    private ClimaDia()
    {
        Fonte = string.Empty;
    }

    private ClimaDia(PontoClima ponto, DateOnly data) : base(null)
    {
        Latitude = ponto.Latitude;
        Longitude = ponto.Longitude;
        Data = data;
        Fonte = string.Empty;
    }

    /// <param name="hoje">Dia comercial da coleta: decide se o dia já é definitivo.</param>
    /// <exception cref="DomainException">Dia futuro (é previsão, RN-CL-03) ou valores fora do normal.</exception>
    public static ClimaDia Registrar(PontoClima ponto, LeituraClima leitura, string fonte, DateTime coletadoEmUtc, DateOnly hoje)
    {
        var dia = new ClimaDia(ponto, leitura.Data);
        dia.Aplicar(leitura, fonte, coletadoEmUtc, hoje);
        return dia;
    }

    /// <summary>Nova coleta do mesmo dia (o serviço corrigiu os dados ou o dia terminou).</summary>
    /// <exception cref="DomainException">Dia já definitivo, leitura de outro dia ou valores fora do normal.</exception>
    public void Atualizar(LeituraClima leitura, string fonte, DateTime coletadoEmUtc, DateOnly hoje)
    {
        Guard.Contra(Definitivo, $"O clima de {Data:dd/MM/yyyy} já é definitivo e não muda mais.");
        Guard.Contra(leitura.Data != Data, "A leitura do clima é de outro dia.");
        Aplicar(leitura, fonte, coletadoEmUtc, hoje);
    }

    /// <summary>RN-CL-04: precisa coletar de novo? Só se não é definitivo e a última coleta tem 3 h ou mais (ou se forçado).</summary>
    public bool PrecisaColetar(DateTime agoraUtc, bool forcar) =>
        !Definitivo && (forcar || agoraUtc - ColetadoEm >= IntervaloRecoleta);

    private void Aplicar(LeituraClima leitura, string fonte, DateTime coletadoEmUtc, DateOnly hoje)
    {
        Guard.Contra(leitura.Data > hoje, $"{leitura.Data:dd/MM/yyyy} ainda não chegou: o que vem para os próximos dias é previsão.");
        var (maxima, minima) = ValoresClima.MaximaEMinima(leitura.TempMax, leitura.TempMin, leitura.Data);
        var media = ValoresClima.Temperatura(leitura.TempMedia, leitura.Data, "média");
        var sensacao = ValoresClima.TemperaturaOpcional(leitura.SensacaoMax, leitura.Data, "de sensação");
        var chuva = ValoresClima.Chuva(leitura.ChuvaMm, leitura.Data);
        var chuvaHoras = ValoresClima.Percentual(leitura.ChuvaHorasPct, leitura.Data, "Parte do dia com chuva");
        var nuvens = ValoresClima.Percentual(leitura.NuvensPct, leitura.Data, "Nuvens");
        var umidade = ValoresClima.Percentual(leitura.UmidadePct, leitura.Data, "Umidade");
        var nomeFonte = ValoresClima.Fonte(fonte);
        var coletadoEm = Guard.Utc(coletadoEmUtc, "a hora da coleta");

        TempMax = maxima;
        TempMin = minima;
        TempMedia = media;
        SensacaoMax = sensacao;
        ChuvaMm = chuva;
        ChuvaHorasPct = chuvaHoras;
        NuvensPct = nuvens;
        UmidadePct = umidade;
        Icone = ValoresClima.Texto(leitura.Icone, ValoresClima.MaximoIcone);
        Condicao = ValoresClima.Texto(leitura.Condicao, ValoresClima.MaximoCondicao);
        Origem = leitura.Origem;
        Fonte = nomeFonte;
        ColetadoEm = coletadoEm;
        Definitivo = leitura.Origem != OrigemClima.Previsto && Data.DayNumber <= hoje.DayNumber - DiasParaDefinitivo;
    }
}
