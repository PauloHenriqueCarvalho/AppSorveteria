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
}
