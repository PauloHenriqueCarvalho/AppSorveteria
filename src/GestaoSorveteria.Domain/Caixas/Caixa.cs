using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Caixas;

/// <summary>
/// Turno de caixa (RN-CX-*). Abre com fundo de troco, recebe sangrias/suprimentos e fecha
/// com a conferência: esperado = fundo + vendas em dinheiro + suprimentos − sangrias.
/// As vendas em dinheiro vêm das comandas do caixa e são informadas no fechamento pela Application.
/// </summary>
public sealed class Caixa : Entity
{
    private readonly List<MovimentoCaixa> _movimentos = new();

    public Guid AbertoPorUsuarioId { get; private set; }
    public DateTime AbertoEm { get; private set; }
    public decimal FundoTroco { get; private set; }
    public StatusCaixa Status { get; private set; }

    public Guid? FechadoPorUsuarioId { get; private set; }
    public DateTime? FechadoEm { get; private set; }

    /// <summary>Soma dos pagamentos em dinheiro das comandas fechadas, gravada no fechamento.</summary>
    public decimal? TotalVendasDinheiro { get; private set; }

    public decimal? ValorEsperado { get; private set; }
    public decimal? ValorContado { get; private set; }

    /// <summary>contado − esperado. Negativo = faltou dinheiro na gaveta.</summary>
    public decimal? Diferenca { get; private set; }

    public string? Observacao { get; private set; }

    public IReadOnlyCollection<MovimentoCaixa> Movimentos => _movimentos.AsReadOnly();

    public bool EstaAberto => Status == StatusCaixa.Aberto;

    public decimal TotalSangrias => _movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Sangria).Sum(m => m.Valor);

    public decimal TotalSuprimentos => _movimentos.Where(m => m.Tipo == TipoMovimentoCaixa.Suprimento).Sum(m => m.Valor);

    // EF Core
    private Caixa()
    {
    }

    private Caixa(Guid? id, Guid usuarioId, decimal fundoTroco, DateTime abertoEmUtc) : base(id)
    {
        AbertoPorUsuarioId = usuarioId;
        FundoTroco = fundoTroco;
        AbertoEm = abertoEmUtc;
        Status = StatusCaixa.Aberto;
    }

    /// <summary>RN-CX-02. A garantia de "um caixa aberto por vez" (RN-CX-01) é da Application + índice único no banco.</summary>
    public static Caixa Abrir(Guid usuarioId, decimal fundoTroco, DateTime agoraUtc, Guid? id = null) =>
        new(
            id,
            Guard.NaoVazio(usuarioId, "o usuário"),
            Guard.Dinheiro(fundoTroco, "o fundo de troco", permiteZero: true),
            Guard.Utc(agoraUtc, "a data de abertura"));

    public MovimentoCaixa RegistrarSangria(decimal valor, string motivo, Guid usuarioId, DateTime agoraUtc, Guid? id = null) =>
        RegistrarMovimento(TipoMovimentoCaixa.Sangria, valor, motivo, usuarioId, agoraUtc, id);

    public MovimentoCaixa RegistrarSuprimento(decimal valor, string motivo, Guid usuarioId, DateTime agoraUtc, Guid? id = null) =>
        RegistrarMovimento(TipoMovimentoCaixa.Suprimento, valor, motivo, usuarioId, agoraUtc, id);

    /// <summary>RN-CX-06.</summary>
    public decimal CalcularEsperado(decimal totalVendasDinheiro)
    {
        Guard.Dinheiro(totalVendasDinheiro, "o total de vendas em dinheiro", permiteZero: true);
        return Moeda.Arredondar(FundoTroco + totalVendasDinheiro + TotalSuprimentos - TotalSangrias);
    }

    /// <summary>
    /// RN-CX-05/06/07. A Application garante antes que não há comanda aberta no caixa
    /// e informa <paramref name="totalVendasDinheiro"/> (soma dos pagamentos em dinheiro das comandas fechadas).
    /// </summary>
    public void Fechar(decimal valorContado, decimal totalVendasDinheiro, Guid usuarioId, DateTime agoraUtc, string? observacao = null)
    {
        ExigirAberto();
        Guard.NaoVazio(usuarioId, "o usuário");
        Guard.Utc(agoraUtc, "a data de fechamento");
        Guard.Contra(agoraUtc < AbertoEm, "A data de fechamento não pode ser anterior à abertura.");

        ValorContado = Guard.Dinheiro(valorContado, "o valor contado", permiteZero: true);
        TotalVendasDinheiro = Moeda.Arredondar(totalVendasDinheiro);
        ValorEsperado = CalcularEsperado(totalVendasDinheiro);
        Diferenca = Moeda.Arredondar(ValorContado.Value - ValorEsperado.Value);
        Observacao = Guard.TextoOpcional(observacao, "a observação", 500);
        FechadoPorUsuarioId = usuarioId;
        FechadoEm = agoraUtc;
        Status = StatusCaixa.Fechado;
    }

    private MovimentoCaixa RegistrarMovimento(TipoMovimentoCaixa tipo, decimal valor, string motivo, Guid usuarioId, DateTime agoraUtc, Guid? id)
    {
        ExigirAberto();
        var movimento = new MovimentoCaixa(Id, tipo, valor, motivo, usuarioId, agoraUtc, id);

        // Idempotência (RN-SY-02): repetir o mesmo movimento não duplica.
        var existente = _movimentos.FirstOrDefault(m => m.Id == movimento.Id);
        if (existente is not null)
        {
            return existente;
        }

        _movimentos.Add(movimento);
        return movimento;
    }

    private void ExigirAberto() => Guard.Contra(!EstaAberto, "Este caixa já está fechado (RN-CX-07).");
}
