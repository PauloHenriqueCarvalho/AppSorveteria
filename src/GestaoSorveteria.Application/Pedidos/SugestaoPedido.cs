namespace GestaoSorveteria.Application.Pedidos;

/// <summary>
/// Contas do pedido sugerido (RN-PF-02, RN-ES-07), separadas do serviço para testar sem repositório.
/// Tudo em unidades (picolé, pote, balde) até o fim, quando vira embalagens arredondadas para cima no passo.
/// </summary>
public static class SugestaoPedido
{
    /// <summary>Quantos intervalos entre contagens entram na média (RN-PF-02: "até 4 semanas").</summary>
    public const int IntervalosNaMedia = 4;

    /// <summary>Uma contagem concluída do item: quando e quanto tinha.</summary>
    public readonly record struct Contado(DateTime Em, int Unidades);

    /// <summary>
    /// RN-ES-07: saída de cada intervalo = anterior + recebido entre as duas − atual, convertida para "por semana"
    /// (contagens com 6 ou 8 dias de distância contam igual). Intervalo com saída negativa ("contou mais do que devia")
    /// fica de fora da média. Devolve a média dos últimos <see cref="IntervalosNaMedia"/> intervalos válidos, ou null sem histórico.
    /// </summary>
    public static decimal? SaidaSemanalMedia(IReadOnlyList<Contado> contagens, Func<DateTime, DateTime, int> recebidoEntre)
    {
        var ordenadas = contagens.OrderBy(c => c.Em).ToList();
        var porSemana = new List<decimal>();
        for (var i = 1; i < ordenadas.Count; i++)
        {
            var anterior = ordenadas[i - 1];
            var atual = ordenadas[i];
            var dias = (decimal)(atual.Em - anterior.Em).Ticks / TimeSpan.TicksPerDay;
            if (dias < 1)
            {
                continue; // duas contagens no mesmo dia: não dá para tirar consumo
            }

            var saida = anterior.Unidades + recebidoEntre(anterior.Em, atual.Em) - atual.Unidades;
            if (saida >= 0)
            {
                porSemana.Add(saida / dias * 7m);
            }
        }

        var ultimas = porSemana.TakeLast(IntervalosNaMedia).ToList();
        return ultimas.Count == 0 ? null : ultimas.Average();
    }

    /// <summary>
    /// Dias que o estoque precisa durar a partir de hoje: até o próximo dia de pedido (depois de hoje) mais o prazo de entrega —
    /// é quando chega o pedido seguinte. Fábrica que pede toda segunda e entrega em 2 dias: 7 + 2 = 9 dias.
    /// </summary>
    public static int DiasACobrir(DayOfWeek hoje, IReadOnlyCollection<DayOfWeek> diasDePedido, int prazoEntregaDias)
    {
        var ateOProximo = Enumerable.Range(1, 7).First(d => diasDePedido.Contains((DayOfWeek)(((int)hoje + d) % 7)));
        return ateOProximo + prazoEntregaDias;
    }

    /// <summary>
    /// Unidades que faltam para cobrir os dias: média semanal × semanas − estoque. Sem histórico: estoque alvo − estoque
    /// (alvo nulo = 0). Nunca negativo. <paramref name="estoqueUnidades"/> = última contagem + o que chegou depois dela + o que
    /// está a caminho (pedido enviado e ainda não recebido).
    /// </summary>
    public static decimal NecessidadeEmUnidades(decimal? saidaSemanal, int diasACobrir, int? estoqueUnidades, int? alvoUnidades)
    {
        var contado = estoqueUnidades ?? 0;
        var necessidade = saidaSemanal is { } media
            ? media * diasACobrir / 7m - contado
            : (alvoUnidades ?? 0) - contado;
        return Math.Max(0, necessidade);
    }

    /// <summary>Unidades → embalagens, arredondando para cima no passo (0,5 ou 1). 0 fica 0.</summary>
    public static decimal EmEmbalagens(decimal unidades, int unidadesPorEmbalagem, decimal passo)
    {
        if (unidades <= 0)
        {
            return 0;
        }

        // Arredonda a 6 casas antes do teto: dízimas da média (56,0000…03) não podem virar uma caixa a mais.
        var embalagens = Math.Round(unidades / unidadesPorEmbalagem / passo, 6, MidpointRounding.AwayFromZero);
        return Math.Ceiling(embalagens) * passo;
    }
}
