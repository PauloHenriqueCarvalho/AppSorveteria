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
    /// <summary>
    /// O tablet não conhece o fornecedor (o atendente não vê custo nem fornecedor). O <see cref="ItemEstoque"/> montado por
    /// <see cref="ParaDominio"/> serve só para aplicar as regras do Domain (quantidade na contagem, sabor na venda); o
    /// fornecedor e o custo dele não são gravados nem enviados.
    /// </summary>
    private static readonly Guid FornecedorDesconhecidoNoTablet = new("5f0e2a51-0c0a-4c55-9a3e-7a3b51c0a7e1");

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

    /// <summary>
    /// O item com as regras do Domain: passo e meia caixa na contagem (RN-ES-03/06), sabor do grupo e ativo na venda do pote
    /// (RN-ES-09). Desativado continua desativado (a venda recusa; a contagem aceita o que já foi contado).
    /// </summary>
    public ItemEstoque ParaDominio()
    {
        var item = ItemEstoque.Criar(GrupoId, FornecedorDesconhecidoNoTablet, Nome, Unidade, UnidadesPorEmbalagem,
            custoEmbalagem: 0m, Ordem, id: Id);
        if (!Ativo)
        {
            item.Desativar();
        }

        return item;
    }

    /// <summary>O grupo do item com o modo de controle baixado (RN-ES-02).</summary>
    public GrupoEstoque GrupoParaDominio() =>
        GrupoEstoque.Criar(GrupoNome is { Length: >= 2 } nome ? nome : "Sem grupo", ModoControleGrupo, GrupoOrdem, GrupoId);
}
