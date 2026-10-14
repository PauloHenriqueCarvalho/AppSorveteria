using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// RN-ES-08/09: entrada, baixa ou estorno no saldo de um item de grupo <c>SaldoPelaVenda</c> (potes), em unidades.
/// Nunca é apagado nem alterado (correção = outro movimento). <see cref="Em"/> é quando aconteceu na loja (conferência da
/// entrega, fechamento da venda); o estorno usa o mesmo <see cref="Em"/> da baixa que desfaz, para os dois ficarem do mesmo
/// lado da contagem que serve de base do saldo. <see cref="RegistradoEm"/> é quando o servidor gravou.
/// </summary>
public sealed class MovimentoEstoque : Entity
{
    public Guid ItemEstoqueId { get; private set; }
    public TipoMovimentoEstoque Tipo { get; private set; }

    /// <summary>Sempre positivo; o sentido vem do tipo (<see cref="Efeito"/>).</summary>
    public int Unidades { get; private set; }

    public DateTime Em { get; private set; }
    public DateTime RegistradoEm { get; private set; }

    /// <summary>Entrada: pedido conferido.</summary>
    public Guid? PedidoId { get; private set; }

    /// <summary>Baixa e estorno: a venda e a linha (uma baixa por linha, idempotente na sincronização).</summary>
    public Guid? ComandaId { get; private set; }

    public Guid? ItemComandaId { get; private set; }

    /// <summary>Quanto soma no saldo: entrada e estorno somam, baixa tira.</summary>
    public int Efeito => Tipo == TipoMovimentoEstoque.BaixaVenda ? -Unidades : Unidades;

    // EF Core
    private MovimentoEstoque()
    {
    }

    private MovimentoEstoque(Guid itemEstoqueId, TipoMovimentoEstoque tipo, int unidades, DateTime emUtc, DateTime registradoEmUtc)
        : base(null)
    {
        ItemEstoqueId = Guard.NaoVazio(itemEstoqueId, "o item de estoque");
        Guard.Contra(unidades < 1, "O movimento de estoque precisa de pelo menos 1 unidade.");
        Tipo = tipo;
        Unidades = unidades;
        Em = Guard.Utc(emUtc, "a data do movimento");
        RegistradoEm = Guard.Utc(registradoEmUtc, "a data do registro");
    }

    /// <summary>
    /// RN-RC-03: o que veio na entrega (já em unidades: embalagens × unidades por embalagem), na hora em que chegou na loja;
    /// <paramref name="conferidoEmUtc"/> é quando a dona conferiu no sistema.
    /// </summary>
    public static MovimentoEstoque Entrada(Guid itemEstoqueId, int unidades, Guid pedidoId, DateTime chegouEmUtc, DateTime? conferidoEmUtc = null) =>
        new(itemEstoqueId, TipoMovimentoEstoque.Entrada, unidades, chegouEmUtc, conferidoEmUtc ?? chegouEmUtc)
        {
            PedidoId = Guard.NaoVazio(pedidoId, "o pedido"),
        };

    /// <summary>RN-ES-09: linha de venda fechada com sabor. <paramref name="vendidoEmUtc"/> = fechamento da comanda.</summary>
    public static MovimentoEstoque BaixaVenda(
        Guid itemEstoqueId, int unidades, Guid comandaId, Guid itemComandaId, DateTime vendidoEmUtc, DateTime registradoEmUtc) =>
        new(itemEstoqueId, TipoMovimentoEstoque.BaixaVenda, unidades, vendidoEmUtc, registradoEmUtc)
        {
            ComandaId = Guard.NaoVazio(comandaId, "a venda"),
            ItemComandaId = Guard.NaoVazio(itemComandaId, "a linha da venda"),
        };

    /// <summary>Estorno da venda (RN-CM-09): devolve exatamente a baixa, com o mesmo momento da venda.</summary>
    public static MovimentoEstoque EstornoVenda(MovimentoEstoque baixa, DateTime estornadoEmUtc)
    {
        Guard.Contra(baixa.Tipo != TipoMovimentoEstoque.BaixaVenda, "Só baixa de venda é estornada.");
        return new MovimentoEstoque(baixa.ItemEstoqueId, TipoMovimentoEstoque.EstornoVenda, baixa.Unidades, baixa.Em, estornadoEmUtc)
        {
            ComandaId = baixa.ComandaId,
            ItemComandaId = baixa.ItemComandaId,
        };
    }
}
