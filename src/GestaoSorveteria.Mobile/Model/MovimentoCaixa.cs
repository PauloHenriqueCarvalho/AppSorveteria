using GestaoSorveteria.Domain.Caixas;
using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>Sangria ou suprimento (RN-CX-04) gravado no aparelho. Nunca é apagado nem alterado (RN-TD-03).</summary>
[Table("movimentos_caixa")]
public class MovimentoCaixa
{
    private DateTime _em;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, Column("caixa_id")]
    public Guid CaixaId { get; set; }

    [Column("tipo")]
    public TipoMovimentoCaixa Tipo { get; set; }

    [Column("valor_centavos")]
    public long ValorCentavos { get; set; }

    [Column("motivo")]
    public string Motivo { get; set; } = string.Empty;

    [Column("usuario_id")]
    public Guid UsuarioId { get; set; }

    [Column("em")]
    public DateTime Em { get => _em; set => _em = ComoUtc(value); }

    [Ignore]
    public decimal Valor { get => ParaReais(ValorCentavos); set => ValorCentavos = ParaCentavos(value); }

    /// <summary>Para a tela.</summary>
    [Ignore]
    public bool EhSangria => Tipo == TipoMovimentoCaixa.Sangria;

    [Ignore]
    public string TipoTexto => EhSangria ? "Sangria" : "Suprimento";

    [Ignore]
    public DateTime EmLocal => Em.ToLocalTime();
}
