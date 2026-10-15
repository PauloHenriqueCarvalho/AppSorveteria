using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Produtos;

/// <summary>
/// Item vendável (picolé, milk-shake, casquinha, self-service...).
/// Nunca é apagado depois de vendido: desativa (RN-PR-03).
/// </summary>
public sealed class Produto : Entity
{
    public string Nome { get; private set; }

    /// <summary>Nome em minúsculas para índice único case-insensitive (RN-PR-01).</summary>
    public string NomeNormalizado { get; private set; }

    public string Categoria { get; private set; }
    public decimal Preco { get; private set; }

    /// <summary>RN-PR-02: o atendente digita o valor na venda (ex.: self-service por kg).</summary>
    public bool PermiteValorLivre { get; private set; }

    public bool Ativo { get; private set; }

    /// <summary>RN-PR-05: posição do botão no app (menor primeiro).</summary>
    public int Ordem { get; private set; }

    /// <summary>
    /// RN-PR-06: grupo de estoque que a venda deste produto movimenta (ex.: "Pote 1L" → grupo Pote 1L). Se o grupo for
    /// <c>SaldoPelaVenda</c>, o app pede o sabor e a venda baixa o saldo (RN-ES-09). Nulo = não mexe no estoque.
    /// </summary>
    public Guid? GrupoEstoqueId { get; private set; }

    /// <summary>Quantas unidades do grupo saem a cada unidade vendida (padrão 1; ex.: "2 potes de 1L" = 2).</summary>
    public int UnidadesPorVenda { get; private set; } = 1;

    public const int UnidadesPorVendaMaximo = 50;

    /// <summary>
    /// RN-PR-07: custo de uma unidade vendida informado à mão — para o produto sem vínculo de estoque, ou cujo grupo não tem
    /// custo no catálogo. Nulo = sem custo (a margem fica "n/d", nunca zero — RN-RL-04). Não vai para o app.
    /// </summary>
    public decimal? CustoInformado { get; private set; }

    public DateTime CriadoEm { get; private set; }
    public DateTime? AtualizadoEm { get; private set; }

    // EF Core
    private Produto()
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
        Categoria = string.Empty;
    }

    private Produto(Guid? id, DateTime criadoEmUtc) : base(id)
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
        Categoria = string.Empty;
        Ativo = true;
        CriadoEm = criadoEmUtc;
    }

    public static Produto Criar(string nome, string categoria, decimal preco, bool permiteValorLivre, int ordem, DateTime agoraUtc, Guid? id = null)
    {
        var produto = new Produto(id, Guard.Utc(agoraUtc, "a data"));
        produto.Aplicar(nome, categoria, preco, permiteValorLivre, ordem);
        return produto;
    }

    public void Atualizar(string nome, string categoria, decimal preco, bool permiteValorLivre, int ordem, DateTime agoraUtc)
    {
        Aplicar(nome, categoria, preco, permiteValorLivre, ordem);
        AtualizadoEm = Guard.Utc(agoraUtc, "a data");
    }

    /// <summary>Sem efeito se já estiver ativo: não marca AtualizadoEm, então o app não baixa o produto de novo à toa.</summary>
    public void Ativar(DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a data");
        if (Ativo)
        {
            return;
        }

        Ativo = true;
        AtualizadoEm = agora;
    }

    /// <summary>RN-PR-03. Sem efeito se já estiver inativo (mesmo motivo de <see cref="Ativar"/>).</summary>
    public void Desativar(DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a data");
        if (!Ativo)
        {
            return;
        }

        Ativo = false;
        AtualizadoEm = agora;
    }

    /// <summary>
    /// RN-PR-06: liga (ou desliga, com <paramref name="grupoEstoqueId"/> nulo) o produto a um grupo de estoque.
    /// Produto de valor livre (self-service) não liga: não tem quantidade de pote. Sem mudança, não marca AtualizadoEm.
    /// </summary>
    public void LigarAoEstoque(Guid? grupoEstoqueId, int unidadesPorVenda, DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a data");
        Guard.Contra(grupoEstoqueId == Guid.Empty, "Grupo de estoque inválido.");
        Guard.Contra(grupoEstoqueId is not null && PermiteValorLivre,
            $"\"{Nome}\" tem valor livre (self-service) e não baixa estoque.");
        Guard.Contra(unidadesPorVenda < 1 || unidadesPorVenda > UnidadesPorVendaMaximo,
            $"Unidades por venda vai de 1 a {UnidadesPorVendaMaximo}.");
        var unidades = grupoEstoqueId is null ? 1 : unidadesPorVenda;
        if (GrupoEstoqueId == grupoEstoqueId && UnidadesPorVenda == unidades)
        {
            return;
        }

        GrupoEstoqueId = grupoEstoqueId;
        UnidadesPorVenda = unidades;
        AtualizadoEm = agora;
    }

    /// <summary>
    /// RN-PR-07: informa (ou apaga, com nulo) o custo de uma unidade vendida. Maior que zero, 2 casas. Não marca AtualizadoEm:
    /// o custo é só do painel e o app não precisa baixar o produto de novo.
    /// </summary>
    public void InformarCusto(decimal? custo)
    {
        CustoInformado = custo is null ? null : Guard.Dinheiro(custo.Value, "o custo", permiteZero: false);
    }

    /// <summary>
    /// O modo do grupo de estoque ligado mudou (ex.: potes voltaram para contagem): marca o produto como alterado para o app
    /// baixar de novo e saber se pede sabor (RN-PR-06).
    /// </summary>
    public void GrupoDeEstoqueMudou(DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a data");
        if (GrupoEstoqueId is not null)
        {
            AtualizadoEm = agora;
        }
    }

    public static string NormalizarNome(string? nome) => (nome ?? string.Empty).Trim().ToLowerInvariant();

    private void Aplicar(string nome, string categoria, decimal preco, bool permiteValorLivre, int ordem)
    {
        Guard.Contra(permiteValorLivre && GrupoEstoqueId is not null,
            "Produto ligado ao estoque não pode ter valor livre: desligue do grupo de estoque antes.");
        Nome = Guard.Texto(nome, "o nome do produto", 2, 80);
        NomeNormalizado = NormalizarNome(Nome);
        Categoria = Guard.Texto(categoria, "a categoria", 1, 50);
        Preco = Guard.Dinheiro(preco, "o preço", permiteZero: true);
        Guard.Contra(ordem < 0, "A ordem não pode ser negativa.");
        PermiteValorLivre = permiteValorLivre;
        Ordem = ordem;
    }
}
