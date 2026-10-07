namespace GestaoSorveteria.Contracts.Versao;

/// <summary>
/// GET /api/versao (público). O app compara a própria versão com <see cref="VersaoMinimaApp"/>:
/// abaixo dela, pede para atualizar antes de continuar; abaixo de <see cref="VersaoAtualApp"/>, só avisa.
/// Versões no formato "maior.menor" (ex.: "1.0"), como em ApplicationDisplayVersion do app.
/// </summary>
public sealed record VersaoResponse(
    string VersaoApi,
    string VersaoMinimaApp,
    string VersaoAtualApp,
    string? LinkApk);
