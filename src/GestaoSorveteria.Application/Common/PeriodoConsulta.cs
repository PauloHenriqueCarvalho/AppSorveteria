using System.Globalization;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Application.Common;

/// <summary>
/// Valida os dias pedidos nas consultas do painel antes de virar intervalo UTC (<see cref="DiaComercial"/>):
/// datas absurdas (ex.: 9999-12-31) estourariam a conversão e virariam erro 500.
/// </summary>
public static class PeriodoConsulta
{
    public static readonly DateOnly PrimeiroDia = new(2020, 1, 1);

    /// <exception cref="DomainException">Data fora da faixa, final antes da inicial ou período maior que <paramref name="maximoDias"/>.</exception>
    public static void Validar(DateOnly de, DateOnly ate, DateTime agoraUtc, int maximoDias)
    {
        var ultimoDia = DiaComercial.DataDe(agoraUtc).AddYears(1);
        if (de < PrimeiroDia || ate > ultimoDia)
        {
            throw new DomainException(
                $"Escolha datas entre {Formatar(PrimeiroDia)} e {Formatar(ultimoDia)}.");
        }

        if (ate < de)
        {
            throw new DomainException("A data final não pode ser anterior à inicial.");
        }

        if (ate.DayNumber - de.DayNumber + 1 > maximoDias)
        {
            throw new DomainException($"Escolha um período de até {maximoDias} dias.");
        }
    }

    private static string Formatar(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
