using GestaoSorveteria.Domain.Common;

namespace SorveteriaMaui.Services;

/// <summary>Botão do "quanto o cliente entregou" no dinheiro do balcão (RN-CM-13, RN-PG-03).</summary>
public sealed record NotaParaTroco(decimal Valor, string Texto);

/// <summary>
/// RN-CM-13: no dinheiro, em vez de digitar o valor recebido, o atendente toca no que o cliente entregou: o valor exato
/// ou as notas mais prováveis para aquele total. O troco continua sendo calculado pelo Domain ao fechar (RN-PG-03).
/// </summary>
public static class NotasDoTroco
{
    private static readonly decimal[] Notas = [5m, 10m, 20m, 50m, 100m, 200m];

    /// <summary>
    /// Exato + arredondado para cima em R$ 5 e R$ 10 + as notas que cobrem o total, sem repetir, em ordem, até 5 botões.
    /// Ex.: R$ 23,50 → exato, 25, 30, 50, 100. R$ 18 → exato, 20, 50, 100.
    /// </summary>
    public static IReadOnlyList<NotaParaTroco> Para(decimal total)
    {
        total = Moeda.Arredondar(total);
        if (total <= 0)
        {
            return [];
        }

        var valores = new List<decimal>
        {
            total,
            Math.Ceiling(total / 5m) * 5m,
            Math.Ceiling(total / 10m) * 10m,
        };
        valores.AddRange(Notas.Where(n => n >= total));

        return valores
            .Distinct()
            .Order()
            .Take(5)
            .Select(v => new NotaParaTroco(v, v == total ? $"Exato\nR$ {v:F2}" : $"R$ {v:F2}"))
            .ToList();
    }
}
