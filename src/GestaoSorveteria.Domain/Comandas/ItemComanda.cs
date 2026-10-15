using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Comandas;

/// <summary>
/// Item já gravado, usado em <see cref="Comanda.Restaurar"/>. ProdutoId nulo = item livre.
/// <paramref name="ItemEstoqueId"/>: sabor do pote (RN-ES-09); nulo no resto.
/// </summary>
public readonly record struct DadosItem(Guid Id, Guid? ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, Guid? ItemEstoqueId);

/// <summary>
/// Item de uma comanda recebida na sincronização, usado em <see cref="Comanda.Remontar"/>: descrição e preço
/// praticados na venda e o subtotal calculado no app, conferido no servidor (RN-SY-06).
/// </summary>
public readonly record struct DadosItemRecebido(
    Guid Id, Guid? ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal, Guid? ItemEstoqueId);

/// <summary>
/// Linha da comanda. Guarda descrição e preço praticado no momento (snapshot, RN-PR-04).
/// ProdutoId nulo = item livre (self-service / venda avulsa, RN-CM-03).
/// Criado e alterado somente pela <see cref="Comanda"/>.
/// </summary>
public sealed class ItemComanda : Entity
{
    public const int QuantidadeMaxima = 999;
    public const int DescricaoMaxima = 120;

    public Guid ComandaId { get; private set; }
    public Guid? ProdutoId { get; private set; }
    public string Descricao { get; private set; }
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }
    public decimal Subtotal { get; private set; }

    /// <summary>
    /// RN-ES-09: sabor do pote — item de estoque de um grupo <c>SaldoPelaVenda</c>. A venda fechada baixa o saldo dele no
    /// servidor. Nulo para o resto (picolé e massa vendem sem sabor).
    /// </summary>
    public Guid? ItemEstoqueId { get; private set; }

    public bool EhItemLivre => ProdutoId is null;

    // EF Core
    private ItemComanda()
    {
        Descricao = string.Empty;
    }

    internal ItemComanda(Guid comandaId, Guid? produtoId, string descricao, int quantidade, decimal precoUnitario, Guid? id, Guid? itemEstoqueId = null)
        : base(id)
    {
        Guard.Contra(itemEstoqueId == Guid.Empty, "Sabor inválido.");
        Guard.Contra(itemEstoqueId is not null && produtoId is null, "Sabor só em item de produto, nunca em item livre (RN-ES-09).");
        ComandaId = comandaId;
        ProdutoId = produtoId;
        ItemEstoqueId = itemEstoqueId;
        Descricao = Guard.Texto(descricao, "a descrição do item", 1, DescricaoMaxima);
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
    private void Recalcular()
    {
        var subtotal = Moeda.Arredondar(Quantidade * PrecoUnitario);
        Guard.Contra(subtotal > Moeda.LimiteColuna, "O subtotal do item passa do máximo aceito. Confira quantidade e preço.");
        Subtotal = subtotal;
    }
}
