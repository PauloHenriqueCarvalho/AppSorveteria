using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Comandas;

/// <summary>Item já gravado, usado em <see cref="Comanda.Restaurar"/>. ProdutoId nulo = item livre.</summary>
public readonly record struct DadosItem(Guid Id, Guid? ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario);

/// <summary>
/// Linha da comanda. Guarda descrição e preço praticado no momento (snapshot, RN-PR-04).
/// ProdutoId nulo = item livre (self-service / venda avulsa, RN-CM-03).
/// Criado e alterado somente pela <see cref="Comanda"/>.
/// </summary>
public sealed class ItemComanda : Entity
{
    public const int QuantidadeMaxima = 999;

    public Guid ComandaId { get; private set; }
    public Guid? ProdutoId { get; private set; }
    public string Descricao { get; private set; }
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal Subtotal { get; private set; }

    public bool EhItemLivre => ProdutoId is null;

    // EF Core
    private ItemComanda()
    {
        Descricao = string.Empty;
    }

    internal ItemComanda(Guid comandaId, Guid? produtoId, string descricao, int quantidade, decimal precoUnitario, Guid? id)
        : base(id)
    {
        ComandaId = comandaId;
        ProdutoId = produtoId;
        Descricao = Guard.Texto(descricao, "a descrição do item", 1, 120);
        PrecoUnitario = Guard.Dinheiro(precoUnitario, "o preço", permiteZero: produtoId is not null);
        Quantidade = ValidarQuantidade(quantidade);
        Recalcular();
    }

    internal void AlterarQuantidade(int quantidade)
    {
        Quantidade = ValidarQuantidade(quantidade);
        Recalcular();
    }

    internal void Somar(int quantidade) => AlterarQuantidade(Quantidade + ValidarQuantidade(quantidade));

    private static int ValidarQuantidade(int quantidade)
    {
        Guard.Contra(quantidade < 1, "A quantidade deve ser pelo menos 1 (RN-CM-04).");
        Guard.Contra(quantidade > QuantidadeMaxima, $"A quantidade deve ser no máximo {QuantidadeMaxima}.");
        return quantidade;
    }

    /// <summary>RN-CM-05.</summary>
    private void Recalcular() => Subtotal = Moeda.Arredondar(Quantidade * PrecoUnitario);
}
