using System.Text.Json.Serialization;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Contracts.Produtos;

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
internal sealed partial class ApiJsonContext : JsonSerializerContext;
