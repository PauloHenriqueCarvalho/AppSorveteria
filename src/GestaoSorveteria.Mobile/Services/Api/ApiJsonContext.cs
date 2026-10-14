using System.Text.Json.Serialization;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Contracts.Produtos;
using GestaoSorveteria.Contracts.Sync;

namespace SorveteriaMaui.Services.Api;

/// <summary>Corpo de erro da API (ProblemDetails / ValidationProblemDetails).</summary>
public sealed record ProblemaApi(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors);

/// <summary>
/// JSON gerado em tempo de compilação (sem reflexão): funciona com o trimming do build Android em Release
/// e usa o mesmo formato da API (camelCase, sem diferenciar maiúsculas).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(LoginResponse))]
[JsonSerializable(typeof(UsuarioDto))]
[JsonSerializable(typeof(ProblemaApi))]
[JsonSerializable(typeof(CatalogoProdutosResponse))]
[JsonSerializable(typeof(ProdutoDto))]
[JsonSerializable(typeof(SyncCaixasRequest))]
[JsonSerializable(typeof(SyncComandasRequest))]
[JsonSerializable(typeof(SyncResponse))]
// Fase 2: catálogo de estoque, configuração da loja e avisos "Está acabando"
[JsonSerializable(typeof(CatalogoEstoqueAppResponse))]
[JsonSerializable(typeof(GrupoEstoqueDto))]
[JsonSerializable(typeof(ItemEstoqueAppDto))]
[JsonSerializable(typeof(ConfiguracaoLojaDto))]
[JsonSerializable(typeof(List<AvisoDto>))]
[JsonSerializable(typeof(AvisoDto))]
[JsonSerializable(typeof(SyncAvisosRequest))]
[JsonSerializable(typeof(AvisoSyncDto))]
[JsonSerializable(typeof(SyncAvisosResponse))]
[JsonSerializable(typeof(ResultadoSyncAvisoDto))]
[JsonSerializable(typeof(ResumoDoDiaResponse))]
internal sealed partial class ApiJsonContext : JsonSerializerContext;
