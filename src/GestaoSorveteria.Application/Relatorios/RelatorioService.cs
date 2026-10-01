using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Relatorios;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Relatorios;

/// <summary>Relatórios do painel. Fase 1: resumo do dia (dashboard). Só leitura.</summary>
public sealed class RelatorioService
{
    private readonly IComandaRepository _comandas;
    private readonly CaixaConsultaService _caixas;
    private readonly IClock _clock;

    public RelatorioService(IComandaRepository comandas, CaixaConsultaService caixas, IClock clock)
    {
        _comandas = comandas;
        _caixas = caixas;
        _clock = clock;
    }

    /// <summary>Resumo de um dia comercial (RN-RL-02); sem <paramref name="dia"/> = hoje.</summary>
    public async Task<ResumoDiaDto> ResumoDoDiaAsync(DateOnly? dia, CancellationToken cancellationToken = default)
    {
        var data = dia ?? DiaComercial.DataDe(_clock.UtcNow);
        PeriodoConsulta.Validar(data, data, _clock.UtcNow, maximoDias: 1);
        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(data);

        // RN-RL-01: faturamento = comandas fechadas no dia; as estornadas depois ficam à parte, no dia da venda.
        var vendas = await _comandas.ListarFechadasAsync(inicioUtc, fimUtc, cancellationToken);
        var fechadas = vendas.Where(c => c.Status == StatusComanda.Fechada).ToList();
        var estornadas = vendas.Where(c => c.Status == StatusComanda.Estornada).ToList();
        var total = Moeda.Arredondar(fechadas.Sum(c => c.Total));
        var ticketMedio = fechadas.Count == 0 ? 0m : Moeda.Arredondar(total / fechadas.Count);

        var (_, canceladas) = await _comandas.ListarPorPeriodoAsync(
            inicioUtc, fimUtc, StatusComanda.Cancelada, pular: 0, quantidade: 1, cancellationToken);

        return new ResumoDiaDto(
            data,
            total,
            fechadas.Count,
            ticketMedio,
            CaixaConsultaService.TotaisPorForma(fechadas),
            Moeda.Arredondar(estornadas.Sum(c => c.Total)),
            estornadas.Count,
            canceladas,
            fechadas.Count(c => c.RecebidaAposFechamentoCaixa),
            await _caixas.ObterAtualAsync(cancellationToken));
    }
}
