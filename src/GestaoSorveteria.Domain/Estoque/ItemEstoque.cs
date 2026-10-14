using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// RN-ES-01: o que a loja compra (sabor de picolé, pote, balde, copo...). Quantidades guardadas na menor
/// unidade (picolé, pote, balde), exibidas em embalagens com meia caixa (RN-ES-03). Nunca é apagado: desativa.
/// </summary>
public sealed class ItemEstoque : Entity
{
    public const int UnidadesPorEmbalagemMaximo = 1000;

    public Guid GrupoId { get; private set; }
    public Guid FornecedorId { get; private set; }
    public string Nome { get; private set; }

    /// <summary>Para o índice único grupo + nome sem diferenciar maiúsculas (RN-ES-01).</summary>
    public string NomeNormalizado { get; private set; }

    public UnidadeCompra Unidade { get; private set; }

    /// <summary>Ex.: 28 picolés por caixa; 1 para balde, pote e pacote.</summary>
    public int UnidadesPorEmbalagem { get; private set; }

    public decimal CustoEmbalagem { get; private set; }

    /// <summary>Quanto manter, em embalagens (usado enquanto não há histórico de saída — RN-PF-02).</summary>
    public decimal? EstoqueAlvoEmbalagens { get; private set; }

    /// <summary>Potes: chegou neste saldo → aviso Alta automático (RN-AV-04).</summary>
    public int? MinimoUnidades { get; private set; }

    public int Ordem { get; private set; }
    public bool Ativo { get; private set; }

    // EF Core
    private ItemEstoque()
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
    }

    private ItemEstoque(Guid? id) : base(id)
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
        Ativo = true;
    }

    public static ItemEstoque Criar(
        Guid grupoId,
        Guid fornecedorId,
        string nome,
        UnidadeCompra unidade,
        int unidadesPorEmbalagem,
        decimal custoEmbalagem,
        int ordem,
        decimal? estoqueAlvoEmbalagens = null,
        int? minimoUnidades = null,
        Guid? id = null)
    {
        var item = new ItemEstoque(id) { GrupoId = Guard.NaoVazio(grupoId, "o grupo") };
        item.Atualizar(fornecedorId, nome, unidade, unidadesPorEmbalagem, custoEmbalagem, ordem, estoqueAlvoEmbalagens, minimoUnidades);
        return item;
    }

    public void Atualizar(
        Guid fornecedorId,
        string nome,
        UnidadeCompra unidade,
        int unidadesPorEmbalagem,
        decimal custoEmbalagem,
        int ordem,
        decimal? estoqueAlvoEmbalagens,
        int? minimoUnidades)
    {
        // Valida tudo antes de mudar qualquer campo: um erro não deixa o item alterado pela metade.
        Guard.NaoVazio(fornecedorId, "o fornecedor");
        var nomeValido = NomeEstoque.Limpar(Guard.Texto(nome, "o nome do item", 1, 80));
        Guard.Contra(!Enum.IsDefined(unidade), "Unidade de compra inválida.");
        Guard.Contra(unidadesPorEmbalagem < 1 || unidadesPorEmbalagem > UnidadesPorEmbalagemMaximo,
            $"Unidades por embalagem: de 1 a {UnidadesPorEmbalagemMaximo}.");
        Guard.Contra(unidade != UnidadeCompra.Caixa && unidadesPorEmbalagem != 1 && unidade != UnidadeCompra.Pacote,
            "Só caixa e pacote têm mais de uma unidade por embalagem.");
        var custoValido = Guard.Dinheiro(custoEmbalagem, "o custo da embalagem", permiteZero: true);
        if (estoqueAlvoEmbalagens is { } alvo)
        {
            ValidarEmbalagens(alvo, "O estoque alvo", unidade);
        }

        Guard.Contra(minimoUnidades < 0, "O mínimo não pode ser negativo.");

        FornecedorId = fornecedorId;
        Nome = nomeValido;
        NomeNormalizado = NomeEstoque.Normalizar(nomeValido);
        Unidade = unidade;
        UnidadesPorEmbalagem = unidadesPorEmbalagem;
        CustoEmbalagem = custoValido;
        Ordem = ordem;
        EstoqueAlvoEmbalagens = estoqueAlvoEmbalagens;
        MinimoUnidades = minimoUnidades;
    }

    /// <summary>A dona reorganiza o catálogo (ex.: um sabor que estava em "Picolé de fruta" vai para "Picolé ao leite").</summary>
    public void MoverParaGrupo(Guid grupoId) => GrupoId = Guard.NaoVazio(grupoId, "o grupo");

    /// <summary>RN-PF-05/RN-RC-05: novo preço da nota vale para os próximos pedidos.</summary>
    public void AlterarCusto(decimal custoEmbalagem) =>
        CustoEmbalagem = Guard.Dinheiro(custoEmbalagem, "o custo da embalagem", permiteZero: true);

    public void Ativar() => Ativo = true;

    public void Desativar() => Ativo = false;

    /// <summary>
    /// RN-ES-03: embalagens contadas ou pedidas ("2,5" caixas) → unidades (70 picolés).
    /// Meia caixa só para caixa (e com número par de unidades); balde, pote e pacote são inteiros.
    /// </summary>
    public int ParaUnidades(decimal embalagens)
    {
        ValidarEmbalagens(embalagens, "A quantidade", Unidade);
        var unidades = embalagens * UnidadesPorEmbalagem;
        Guard.Contra(unidades != decimal.Truncate(unidades),
            $"Meia caixa de {Nome} não fecha em unidades inteiras ({UnidadesPorEmbalagem} por caixa).");
        return (int)unidades;
    }

    /// <summary>Unidades → embalagens para mostrar (70 picolés → 2,5 caixas). Arredonda a 1 casa só para exibir.</summary>
    public decimal EmEmbalagens(int unidades) =>
        Math.Round((decimal)unidades / UnidadesPorEmbalagem, 1, MidpointRounding.AwayFromZero);

    /// <summary>Passo aceito na contagem e no pedido: 0,5 para caixa, 1 para o resto.</summary>
    public decimal PassoEmbalagens => Passo(Unidade);

    private static decimal Passo(UnidadeCompra unidade) => unidade == UnidadeCompra.Caixa ? 0.5m : 1m;

    private static void ValidarEmbalagens(decimal embalagens, string campo, UnidadeCompra unidade)
    {
        Guard.Contra(embalagens < 0, $"{campo} não pode ser negativa.");
        Guard.Contra(embalagens > 9999, $"{campo} passa do máximo aceito.");
        Guard.Contra(embalagens % Passo(unidade) != 0,
            unidade == UnidadeCompra.Caixa
                ? $"{campo} de caixas vai de meia em meia (ex.: 2,5)."
                : $"{campo} deve ser um número inteiro.");
    }
}
