using GestaoSorveteria.Domain.Estoque;
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

    /// <summary>Para a tela do balcão (RN-CM-13): mostra o preço no botão; valor livre mostra "digitar valor". Nunca gravado.</summary>
    [Ignore]
    public bool TemPrecoFixo => !PermiteValorLivre;

    [Indexed, Column("ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>RN-PR-05: posição do botão (menor primeiro).</summary>
    [Column("ordem")]
    public int Ordem { get; set; }

    [Column("criado_em")]
    public DateTime CriadoEm { get => _criadoEm; set => _criadoEm = ComoUtc(value); }

    [Column("atualizado_em")]
    public DateTime? AtualizadoEm { get => _atualizadoEm; set => _atualizadoEm = ComoUtc(value); }

    // ---------------------------------------------------------------- Fase 2: potes com sabor (Sprint 7)

    /// <summary>
    /// RN-PR-06: grupo de estoque que a venda deste produto movimenta (ex.: "Pote 1L" → grupo Pote 1L). Nulo = não mexe no
    /// estoque. Coluna nova: em aparelho que já tinha o catálogo fica nula até a carga cheia (CatalogoService).
    /// </summary>
    [Column("grupo_estoque_id")]
    public Guid? GrupoEstoqueId { get; set; }

    /// <summary>Quantas unidades do grupo saem a cada unidade vendida (padrão 1). Quem baixa o saldo é o servidor.</summary>
    [Column("unidades_por_venda")]
    public int UnidadesPorVenda { get; set; } = 1;

    /// <summary>
    /// Reserva (RN-ES-09): o servidor disse que o grupo é de potes. Só vale enquanto o tablet não conhece o grupo
    /// (catálogo de estoque ainda não baixado); quem decide é o modo do grupo (<see cref="PedeSaborNoModo"/>).
    /// </summary>
    [Column("pede_sabor")]
    public bool PedeSabor { get; set; }

    /// <summary>Para a tela: preenchido ao montar a lista (aba Produtos e "Adicionar Produto"), nunca gravado.</summary>
    [Ignore]
    public bool PedeSaborNaVenda { get; set; }

    /// <summary>
    /// RN-PR-06 / RN-ES-09: pede o sabor na venda só se o produto está ligado a um grupo <see cref="ModoControle.SaldoPelaVenda"/>
    /// (potes); picolé e massa vendem sem sabor. Vale o modo do grupo baixado de <c>GET /api/estoque/itens</c>
    /// (<paramref name="modoDoGrupo"/>), que muda junto quando a dona troca o modo; sem ele, o <see cref="PedeSabor"/> da API.
    /// </summary>
    public bool PedeSaborNoModo(ModoControle? modoDoGrupo) =>
        GrupoEstoqueId is not null
        && !PermiteValorLivre
        && (modoDoGrupo is { } modo ? modo == ModoControle.SaldoPelaVenda : PedeSabor);
}
