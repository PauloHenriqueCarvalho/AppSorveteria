using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>
/// Cache local do catálogo, baixado de <c>GET /api/produtos</c> (C2). Espelha o <c>Produto</c> do Domain.
/// O cadastro é feito no painel; o app só lê (fim do cadastro local, B16). Categoria é texto (B13).
/// </summary>
[Table("produtos")]
public class Produto
{
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
