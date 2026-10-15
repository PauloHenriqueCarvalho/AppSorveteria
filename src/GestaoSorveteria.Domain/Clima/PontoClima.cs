using System.Globalization;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Clima;

/// <summary>
/// RN-CL-01: onde o clima foi medido — latitude e longitude com 2 casas (≈ 1 km). O clima é guardado pelo ponto, não pela
/// loja: mudar o endereço, ou uma segunda loja no futuro, ganha o próprio histórico sem misturar com o antigo.
/// </summary>
public readonly record struct PontoClima
{
    private PontoClima(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public decimal Latitude { get; }

    public decimal Longitude { get; }

    /// <exception cref="DomainException">Latitude fora de −90…90 ou longitude fora de −180…180.</exception>
    public static PontoClima De(decimal latitude, decimal longitude)
    {
        Coordenadas.Validar(latitude, longitude);
        return new PontoClima(
            Math.Round(latitude, 2, MidpointRounding.AwayFromZero),
            Math.Round(longitude, 2, MidpointRounding.AwayFromZero));
    }

    /// <summary>"-21.18,-47.81" (ponto como separador decimal, como os serviços de clima pedem).</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.00},{Longitude:0.00}");
}

/// <summary>Faixas válidas de latitude e longitude (RN-CL-01).</summary>
internal static class Coordenadas
{
    public static void Validar(decimal latitude, decimal longitude)
    {
        Guard.Contra(latitude is < -90m or > 90m, "A latitude deve ficar entre −90 e 90.");
        Guard.Contra(longitude is < -180m or > 180m, "A longitude deve ficar entre −180 e 180.");
    }
}
