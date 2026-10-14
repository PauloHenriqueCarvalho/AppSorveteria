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
}
