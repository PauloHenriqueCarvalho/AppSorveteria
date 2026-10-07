using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>Linha da tabela local de itens. Espelha o <c>ItemComanda</c> do Domain.</summary>
[Table("itens_comanda")]
public class ItemComanda
{
    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, Column("comanda_id")]
    public Guid ComandaId { get; set; }

    /// <summary>Nulo = item livre: self-service, venda avulsa (RN-CM-03, B9).</summary>
    [Column("produto_id")]
    public Guid? ProdutoId { get; set; }

    /// <summary>Nome do produto no momento da venda ou texto livre (RN-PR-04).</summary>
    [Column("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [Column("quantidade")]
    public int Quantidade { get; set; } = 1;

    [Column("preco_unitario_centavos")]
    public long PrecoUnitarioCentavos { get; set; }

    [Column("subtotal_centavos")]
    public long SubtotalCentavos { get; set; }

    [Ignore]
    public decimal PrecoUnitario
    {
        get => ParaReais(PrecoUnitarioCentavos);
        set => PrecoUnitarioCentavos = ParaCentavos(value);
    }

    [Ignore]
    public decimal Subtotal
    {
        get => ParaReais(SubtotalCentavos);
        set => SubtotalCentavos = ParaCentavos(value);
    }

    [Ignore]
    public bool EhItemLivre => ProdutoId is null;
}
