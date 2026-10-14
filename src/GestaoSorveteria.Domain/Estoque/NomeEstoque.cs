using System.Text;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// Chave de comparação dos nomes do estoque (fornecedor, grupo, item): sem espaços nas pontas, acentos na forma composta
/// (um "é" digitado no Mac e um vindo do Windows viram o mesmo) e minúsculas. É o que os índices únicos comparam.
/// </summary>
public static class NomeEstoque
{
    public static string Normalizar(string? nome) =>
        (nome ?? string.Empty).Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant();

    /// <summary>Nome para exibir: sem espaços nas pontas e na forma composta.</summary>
    internal static string Limpar(string nome) => nome.Normalize(NormalizationForm.FormC);
}
