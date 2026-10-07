using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>Cache local do catálogo. Espelha o <c>Produto</c> do Domain.</summary>
[Table("produtos")]
public class Produto
{
    /// <summary>
    /// B13: categoria gravada como texto (antes era índice, com significados diferentes no seed e no cadastro).
    /// Lista provisória até a dona confirmar as categorias (docs/02, seção 10, pergunta 2).
    /// </summary>
    public static readonly IReadOnlyList<string> Categorias =
        ["Sorvete", "Picolé", "Pote", "Açaí", "Bebida", "Acompanhamento", "Self-Service"];

    private DateTime _criadoEm;
    private DateTime? _atualizadoEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    [Column("categoria")]
    public string Categoria { get; set; } = string.Empty;

    [Column("preco_centavos")]
    public long PrecoCentavos { get; set; }

    [Ignore]
    public decimal Preco
    {
        get => ParaReais(PrecoCentavos);
        set => PrecoCentavos = ParaCentavos(value);
    }

    /// <summary>RN-PR-02.</summary>
    [Column("permite_valor_livre")]
    public bool PermiteValorLivre { get; set; }

    [Indexed, Column("ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>RN-PR-05: posição do botão (menor primeiro).</summary>
    [Column("ordem")]
    public int Ordem { get; set; }

    [Column("criado_em")]
    public DateTime CriadoEm { get => _criadoEm; set => _criadoEm = ComoUtc(value); }

    [Column("atualizado_em")]
    public DateTime? AtualizadoEm { get => _atualizadoEm; set => _atualizadoEm = ComoUtc(value); }
}
