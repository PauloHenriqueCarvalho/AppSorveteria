using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Loja;

/// <summary>
/// Configuração única da loja (uma linha só): nome que aparece nas mensagens e o WhatsApp da dona,
/// para onde vão o aviso urgente e o resumo do dia (RN-AV-06/07).
/// </summary>
public sealed class ConfiguracaoLoja : Entity
{
    /// <summary>Id fixo da única linha: criar de novo nunca duplica.</summary>
    public static readonly Guid IdUnico = new("7a3f2b10-5c1d-4e8a-9b6f-000000000001");

    public const string NomePadrao = "Sorveteria";

    public string NomeLoja { get; private set; }

    /// <summary>Só dígitos (<see cref="Telefone.NormalizarWhatsApp"/>). Sem número, o app abre o WhatsApp para escolher o contato.</summary>
    public string? WhatsAppDona { get; private set; }

    public DateTime AtualizadoEm { get; private set; }

    /// <summary>RN-CL-01: o lugar da loja como o serviço de clima encontrou (ex.: "Ribeirão Preto, SP, Brasil"); nulo = não informado.</summary>
    public string? LocalNome { get; private set; }

    /// <summary>RN-CL-01: latitude do local, 4 casas.</summary>
    public decimal? Latitude { get; private set; }

    /// <summary>RN-CL-01: longitude do local, 4 casas.</summary>
    public decimal? Longitude { get; private set; }

    /// <summary>Onde o clima da loja é coletado (RN-CL-01); nulo enquanto a dona não informou o local.</summary>
    public PontoClima? PontoClima =>
        Latitude is { } latitude && Longitude is { } longitude ? Clima.PontoClima.De(latitude, longitude) : null;

    // EF Core
    private ConfiguracaoLoja()
    {
        NomeLoja = NomePadrao;
    }

    private ConfiguracaoLoja(DateTime agoraUtc) : base(IdUnico)
    {
        NomeLoja = NomePadrao;
        AtualizadoEm = Guard.Utc(agoraUtc, "a data");
    }

    /// <summary>Antes da dona configurar: nome genérico e sem WhatsApp.</summary>
    public static ConfiguracaoLoja Padrao(DateTime agoraUtc) => new(agoraUtc);

    public void Atualizar(string nomeLoja, string? whatsAppDona, DateTime agoraUtc)
    {
        var nome = Guard.Texto(nomeLoja, "o nome da loja", 2, 60);
        var whatsApp = Telefone.NormalizarWhatsApp(whatsAppDona);
        var agora = Guard.Utc(agoraUtc, "a data");

        NomeLoja = nome;
        WhatsAppDona = whatsApp;
        AtualizadoEm = agora;
    }

    /// <summary>
    /// RN-CL-01: o local da loja confirmado pela dona (o que o serviço de clima encontrou). Coordenadas com 4 casas; o clima é
    /// guardado pelo ponto com 2 (<see cref="Clima.PontoClima"/>).
    /// </summary>
    public void DefinirLocal(string nome, decimal latitude, decimal longitude, DateTime agoraUtc)
    {
        var texto = Guard.Texto(nome, "o nome do local", 2, 120);
        Coordenadas.Validar(latitude, longitude);
        var agora = Guard.Utc(agoraUtc, "a data");

        LocalNome = texto;
        Latitude = Math.Round(latitude, 4, MidpointRounding.AwayFromZero);
        Longitude = Math.Round(longitude, 4, MidpointRounding.AwayFromZero);
        AtualizadoEm = agora;
    }
}
