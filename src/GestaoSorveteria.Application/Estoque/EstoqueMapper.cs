using GestaoSorveteria.Contracts.Estoque;
using GestaoSorveteria.Domain.Estoque;

namespace GestaoSorveteria.Application.Estoque;

public static class EstoqueMapper
{
    public static FornecedorDto ToDto(this Fornecedor f) =>
        new(
            f.Id,
            f.Nome,
            f.WhatsApp,
            f.DiasPedido.Select(d => d.ToString()).ToList(),
            f.PrazoEntregaDias,
            f.AceitaMeiaCaixa,
            f.Envio.ToString(),
            f.MostrarPrecos,
            f.Ativo);

    public static GrupoEstoqueDto ToDto(this GrupoEstoque g) => new(g.Id, g.Nome, g.ModoControle.ToString(), g.Ordem);

    /// <summary>Nomes do grupo e do fornecedor vêm junto para o painel não precisar cruzar listas.</summary>
    public static ItemEstoqueDto ToDto(this ItemEstoque i, GrupoEstoque? grupo, Fornecedor? fornecedor) =>
        new(
            i.Id,
            i.GrupoId,
            grupo?.Nome ?? string.Empty,
            i.FornecedorId,
            fornecedor?.Nome ?? string.Empty,
            i.Nome,
            i.Unidade.ToString(),
            i.UnidadesPorEmbalagem,
            i.CustoEmbalagem,
            i.EstoqueAlvoEmbalagens,
            i.MinimoUnidades,
            i.Ordem,
            i.Ativo);
}
