using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Caixas;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Relatorios;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Relatorios;

/// <summary>Relatórios do painel. Fase 1: resumo do dia (dashboard), com vendas por hora, comparativo e mais vendidos. Só leitura.</summary>
public sealed class RelatorioService
{
    private readonly IComandaRepository _comandas;
    private readonly IProdutoRepository _produtos;
    private readonly CaixaConsultaService _caixas;
    private readonly IClock _clock;

    public RelatorioService(IComandaRepository comandas, IProdutoRepository produtos, CaixaConsultaService caixas, IClock clock)
    {
        _comandas = comandas;
        _produtos = produtos;
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
        var ticketMedio = CalculoRelatorio.TicketMedio(total, fechadas.Count);

        var (_, canceladas) = await _comandas.ListarPorPeriodoAsync(
            inicioUtc, fimUtc, new FiltroVendas(StatusComanda.Cancelada), pular: 0, quantidade: 1, cancellationToken);

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
            await _caixas.ObterAtualAsync(cancellationToken),
            CalculoRelatorio.PorHora(fechadas),
            await SemanaPassadaAsync(data, total, fechadas.Count, ticketMedio, cancellationToken),
            await MaisVendidosAsync(fechadas, cancellationToken));
    }

    /// <summary>Rótulo dos itens livres no ranking (RN-RL-03).</summary>
    public const string RotuloItemLivre = CalculoRelatorio.RotuloItemLivre;

    private const int QuantidadeMaisVendidos = 5;

    /// <summary>
    /// RN-RL-07: mesmo dia da semana anterior. Hoje → só até o mesmo horário de agora, para não comparar
    /// a manhã de hoje com o dia inteiro da semana passada.
    /// </summary>
    private async Task<ComparativoDiaDto> SemanaPassadaAsync(
        DateOnly data, decimal total, int quantidade, decimal ticketMedio, CancellationToken cancellationToken)
    {
        var anterior = data.AddDays(-7);
        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(anterior);
        var hoje = data == DiaComercial.DataDe(_clock.UtcNow);
        if (hoje)
        {
            fimUtc = _clock.UtcNow.AddDays(-7);
        }

        var vendas = await _comandas.ListarFechadasAsync(inicioUtc, fimUtc, cancellationToken);
        var fechadas = vendas.Where(c => c.Status == StatusComanda.Fechada).ToList();
        var totalAntes = Moeda.Arredondar(fechadas.Sum(c => c.Total));
        var ticketAntes = CalculoRelatorio.TicketMedio(totalAntes, fechadas.Count);

        return new ComparativoDiaDto(
            anterior,
            hoje,
            totalAntes,
            fechadas.Count,
            ticketAntes,
            CalculoRelatorio.Variacao(total, totalAntes),
            CalculoRelatorio.Variacao(quantidade, fechadas.Count),
            CalculoRelatorio.Variacao(ticketMedio, ticketAntes),
            CalculoRelatorio.PorHora(fechadas));
    }

    /// <summary>
    /// RN-RL-03: até 5 por faturamento; itens livres juntos; empate → mais unidades, depois nome. Nome do cadastro atual
    /// (a linha do pote traz o sabor e não serve de nome do produto).
    /// </summary>
    private async Task<IReadOnlyList<ProdutoVendidoDto>> MaisVendidosAsync(IReadOnlyList<Comanda> fechadas, CancellationToken cancellationToken)
    {
        var produtos = (await _produtos.ListarAsync(somenteAtivos: false, cancellationToken)).ToDictionary(p => p.Id);
        return fechadas
            .SelectMany(c => c.Itens.Select(i => (Comanda: c, Item: i)))
            .GroupBy(x => x.Item.ProdutoId)
            .Select(g =>
            {
                var (descricao, _) = CalculoRelatorio.NomeECategoria(g.Key, produtos, g.MaxBy(x => x.Comanda.FechadaEm).Item.Descricao);
                return new ProdutoVendidoDto(g.Key, descricao, g.Sum(x => x.Item.Quantidade), Moeda.Arredondar(g.Sum(x => x.Item.Subtotal)));
            })
            .OrderByDescending(p => p.Total)
            .ThenByDescending(p => p.Quantidade)
            .ThenBy(p => p.Descricao, StringComparer.CurrentCulture)
            .Take(QuantidadeMaisVendidos)
            .ToList();
    }
}
