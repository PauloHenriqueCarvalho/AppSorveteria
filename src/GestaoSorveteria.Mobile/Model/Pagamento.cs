using GestaoSorveteria.Domain.Comandas;
using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>Linha da tabela local de pagamentos. Espelha o <c>Pagamento</c> do Domain (RN-PG-03).</summary>
[Table("pagamentos")]
public class Pagamento
{
    private DateTime _pagoEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, Column("comanda_id")]
    public Guid ComandaId { get; set; }

    [Column("forma")]
    public FormaPagamento Forma { get; set; }

    [Column("valor_centavos")]
    public long ValorCentavos { get; set; }

    [Column("valor_recebido_centavos")]
    public long ValorRecebidoCentavos { get; set; }

    [Column("troco_centavos")]
    public long TrocoCentavos { get; set; }

    [Column("pago_em")]
    public DateTime PagoEm { get => _pagoEm; set => _pagoEm = ComoUtc(value); }

    /// <summary>Quanto abate da comanda.</summary>
    [Ignore]
    public decimal Valor
    {
        get => ParaReais(ValorCentavos);
        set => ValorCentavos = ParaCentavos(value);
    }

    /// <summary>Quanto o cliente entregou (só difere de <see cref="Valor"/> em dinheiro).</summary>
    [Ignore]
    public decimal ValorRecebido
    {
        get => ParaReais(ValorRecebidoCentavos);
        set => ValorRecebidoCentavos = ParaCentavos(value);
    }

    [Ignore]
    public decimal Troco
    {
        get => ParaReais(TrocoCentavos);
        set => TrocoCentavos = ParaCentavos(value);
    }
}
