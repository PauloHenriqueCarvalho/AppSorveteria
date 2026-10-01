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

    public static string NormalizarNome(string? nome) => (nome ?? string.Empty).Trim().ToLowerInvariant();

    private void Aplicar(string nome, string categoria, decimal preco, bool permiteValorLivre, int ordem)
    {
        Nome = Guard.Texto(nome, "o nome do produto", 2, 80);
        NomeNormalizado = NormalizarNome(Nome);
        Categoria = Guard.Texto(categoria, "a categoria", 1, 50);
        Preco = Guard.Dinheiro(preco, "o preço", permiteZero: true);
        Guard.Contra(ordem < 0, "A ordem não pode ser negativa.");
        PermiteValorLivre = permiteValorLivre;
        Ordem = ordem;
    }
}
