using System.Text.Json;
using System.Text.Json.Serialization;
using QRCoder;

namespace SorveteriaMaui.Services;

/// <summary>
/// RN-PX-02/03: cobrança do Pix integrado que ainda não virou venda. Fica guardada no aparelho enquanto o cliente paga: se o
/// app fechar (bateria, travou), ao voltar ao Balcão o app pergunta ao banco — paga, a venda é gravada com os itens daqui;
/// senão, a cobrança é cancelada. O dinheiro nunca entra sem a venda.
/// <paramref name="Abandonado"/>: o atendente saiu do QR Code sem o banco confirmar o cancelamento (ele ainda pode valer) —
/// a retomada tenta cancelar e, se ele aparecer pago, grava a venda. <paramref name="CobradoAMao"/>: a venda já foi gravada
/// como Pix à mão — se o QR Code aparecer pago, o cliente pagou duas vezes e a dona é avisada (nada é gravado de novo).
/// </summary>
public sealed record PixPendente(
    Guid CobrancaId,
    decimal Total,
    List<ItemDoBalcao> Itens,
    DateTime CriadoEmUtc,
    bool Abandonado = false,
    bool CobradoAMao = false)
{
    public string Txid => CobrancaId.ToString("N");
}

[JsonSerializable(typeof(PixPendente))]
internal sealed partial class PixPendenteJsonContext : JsonSerializerContext;

/// <summary>Guarda no máximo uma cobrança pendente (o balcão cobra um cliente por vez).</summary>
public static class PixPendenteGuardado
{
    private const string Chave = "pix_pendente";

    public static PixPendente? Ler()
    {
        try
        {
            var json = Preferences.Default.Get(Chave, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize(json, PixPendenteJsonContext.Default.PixPendente);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Sorveteria] Pix pendente ilegível, descartado: {ex.Message}");
            Limpar();
            return null;
        }
    }

    public static void Guardar(PixPendente pendente) =>
        Preferences.Default.Set(Chave, JsonSerializer.Serialize(pendente, PixPendenteJsonContext.Default.PixPendente));

    public static void Limpar() => Preferences.Default.Remove(Chave);
}

/// <summary>RN-PX-02: o QR Code é desenhado no aparelho a partir do "copia e cola" que o banco devolveu.</summary>
public static class GeradorQrCode
{
    public static ImageSource Gerar(string copiaECola)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(copiaECola, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(dados).GetGraphic(12);
        return ImageSource.FromStream(() => new MemoryStream(png));
    }
}
