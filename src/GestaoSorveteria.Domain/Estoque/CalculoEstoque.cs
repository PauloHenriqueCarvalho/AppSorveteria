namespace GestaoSorveteria.Domain.Estoque;

/// <summary>Contas de estoque que não dependem de banco (mesmo código no servidor e, se preciso, no app).</summary>
public static class CalculoEstoque
{
    /// <summary>
    /// RN-ES-07: saída da semana = contagem anterior + recebido entre as duas − contagem atual (em unidades).
    /// Negativo (contou mais do que devia) → <c>null</c>: aparece como "conferir" e não entra na média.
    /// </summary>
    public static int? SaidaDaSemana(int contagemAnterior, int recebidoEntreAsDuas, int contagemAtual)
    {
        var saida = contagemAnterior + recebidoEntreAsDuas - contagemAtual;
        return saida < 0 ? null : saida;
    }

    /// <summary>A última contagem concluída de um item: a base do saldo (RN-ES-08).</summary>
    public readonly record struct BaseDoSaldo(int Unidades, DateTime Em);

    /// <summary>
    /// RN-ES-08: saldo do pote = última contagem + movimentos que aconteceram depois dela (entrada, baixa, estorno). Sem
    /// contagem, soma todos os movimentos. Movimento de antes da contagem já está no que foi contado — mesmo que tenha
    /// chegado ao servidor depois (venda sincronizada tarde). Pode dar negativo: a venda nunca é bloqueada por saldo.
    /// </summary>
    public static int Saldo(BaseDoSaldo? baseDoSaldo, IEnumerable<MovimentoEstoque> movimentos) =>
        baseDoSaldo is { } contagem
            ? contagem.Unidades + movimentos.Where(m => m.Em > contagem.Em).Sum(m => m.Efeito)
            : movimentos.Sum(m => m.Efeito);

    /// <summary>
    /// RN-AV-04: aviso automático pelo saldo — zerou (ou ficou negativo) é <c>Urgente</c>; chegou no mínimo é <c>Alta</c>;
    /// acima do mínimo (ou sem mínimo e com saldo) não avisa.
    /// </summary>
    public static PrioridadeAviso? PrioridadePeloSaldo(int saldo, int? minimoUnidades) =>
        saldo <= 0 ? PrioridadeAviso.Urgente
        : minimoUnidades is { } minimo && saldo <= minimo ? PrioridadeAviso.Alta
        : null;
}
