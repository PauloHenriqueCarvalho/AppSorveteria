using GestaoSorveteria.Domain.Estoque;
using SQLite;

namespace SorveteriaMaui.Model;

/// <summary>
/// Cache local do catálogo de estoque (Fase 2), baixado de <c>GET /api/estoque/itens</c> — sem custo nem fornecedor.
/// Serve à busca do "Está acabando" (todos os grupos, inclusive copos e insumos) e, depois, à contagem semanal.
/// O grupo vem junto em cada linha (nome, ordem e modo de controle, RN-ES-02) para a tela não precisar de outra tabela.
/// </summary>
[Table("itens_estoque")]
public class ItemEstoqueLocal
{
    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, Column("grupo_id")]
    public Guid GrupoId { get; set; }

    [Column("grupo_nome")]
    public string GrupoNome { get; set; } = string.Empty;

    /// <summary>Ordem do grupo como na planilha (Picolés de fruta, ao leite, Skimo, Massas…).</summary>
    [Column("grupo_ordem")]
    public int GrupoOrdem { get; set; }

    /// <summary>RN-ES-02: Contagem, SaldoPelaVenda ou SoAviso (a contagem semanal usa).</summary>
    [Column("modo_controle")]
    public ModoControle ModoControleGrupo { get; set; } = ModoControle.Contagem;

    [Column("nome")]
    public string Nome { get; set; } = string.Empty;

    /// <summary>RN-ES-01: como o fornecedor vende (caixa, balde, unidade, pacote).</summary>
    [Column("unidade")]
    public UnidadeCompra Unidade { get; set; } = UnidadeCompra.Unidade;

    [Column("unidades_por_embalagem")]
    public int UnidadesPorEmbalagem { get; set; } = 1;

    [Column("ordem")]
    public int Ordem { get; set; }

    [Indexed, Column("ativo")]
    public bool Ativo { get; set; } = true;

    /// <summary>"Picolé ao leite Coco preto": o mesmo texto que a API usa nas mensagens (Domain).</summary>
    [Ignore]
    public string Descricao => MensagensWhatsApp.DescricaoDoItem(GrupoNome, Nome);

    /// <summary>RN-ES-03: o aviso conta na menor unidade (picolé, pote, balde, copo).</summary>
    [Ignore]
    public string DicaQuantidade => Unidade switch
    {
        UnidadeCompra.Balde => "Conte em baldes.",
        UnidadeCompra.Caixa when UnidadesPorEmbalagem > 1 => $"Conte as unidades (1 caixa = {UnidadesPorEmbalagem}).",
        UnidadeCompra.Pacote when UnidadesPorEmbalagem > 1 => $"Conte as unidades (1 pacote = {UnidadesPorEmbalagem}).",
        _ => "Conte as unidades.",
    };
}
