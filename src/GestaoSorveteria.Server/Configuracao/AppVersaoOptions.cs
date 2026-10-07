namespace GestaoSorveteria.Server.Configuracao;

/// <summary>
/// Seção "App" da configuração: versão mínima e atual do app do atendente e o link fixo do APK.
/// Em produção vêm das variáveis App__VersaoMinima, App__VersaoAtual e App__LinkApk (docs/07).
/// </summary>
public sealed class AppVersaoOptions
{
    public const string Secao = "App";

    /// <summary>Abaixo desta versão o app deve exigir atualização (ex.: mudança no protocolo de sincronização).</summary>
    public string VersaoMinima { get; set; } = "1.0";

    /// <summary>Última versão publicada; o app só avisa que existe uma nova.</summary>
    public string VersaoAtual { get; set; } = "1.0";

    /// <summary>Link fixo de download do APK assinado (docs/03 seção 10). Vazio enquanto não houver.</summary>
    public string? LinkApk { get; set; }

    public void Validar()
    {
        if (!Version.TryParse(VersaoMinima, out var minima))
        {
            throw new InvalidOperationException("App:VersaoMinima deve ser uma versão no formato maior.menor (ex.: 1.0).");
        }

        if (!Version.TryParse(VersaoAtual, out var atual))
        {
            throw new InvalidOperationException("App:VersaoAtual deve ser uma versão no formato maior.menor (ex.: 1.0).");
        }

        if (minima > atual)
        {
            throw new InvalidOperationException("App:VersaoMinima não pode ser maior que App:VersaoAtual.");
        }

        if (!string.IsNullOrWhiteSpace(LinkApk) && !Uri.TryCreate(LinkApk, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("App:LinkApk deve ser uma URL completa (https://...).");
        }
    }
}
