using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Tests.Application;

// Fakes em memória para as consultas do painel (caixas e comandas).

internal sealed class CaixasEmMemoria : ICaixaRepository
{
    public List<Caixa> Caixas { get; } = new();

    public Task<Caixa?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Caixas.FirstOrDefault(c => c.Id == id));

    public Task<Caixa?> ObterAbertoAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Caixas.FirstOrDefault(c => c.EstaAberto));

    public Task<IReadOnlyList<Caixa>> ListarAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Caixa>>(Caixas.Where(c => c.AbertoEm >= deUtc && c.AbertoEm < ateUtc).ToList());

    public Task<bool> ExisteMovimentoAsync(Guid movimentoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Caixas.Any(c => c.Movimentos.Any(m => m.Id == movimentoId)));

    public Task AdicionarAsync(Caixa caixa, CancellationToken cancellationToken = default)
    {
        Caixas.Add(caixa);
        return Task.CompletedTask;
    }
}

internal sealed class ComandasEmMemoria : IComandaRepository
{
    public List<Comanda> Comandas { get; } = new();

    public Task<Comanda?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Comanda>> ListarPorCaixaAsync(Guid caixaId, StatusComanda? status = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comanda>>(
            Comandas.Where(c => c.CaixaId == caixaId && (status is null || c.Status == status)).OrderBy(c => c.Numero).ToList());

    public Task<IReadOnlyList<Comanda>> ListarFechadasAsync(DateTime deUtc, DateTime ateUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Comanda>>(
            Comandas.Where(c => c.Status == StatusComanda.Fechada && c.FechadaEm >= deUtc && c.FechadaEm < ateUtc).ToList());

    public Task<(IReadOnlyList<Comanda> Itens, int Total)> ListarPorPeriodoAsync(
        DateTime deUtc, DateTime ateUtc, StatusComanda? status, int pular, int quantidade, CancellationToken cancellationToken = default)
    {
        var filtradas = Comandas
            .Where(c => DataDaVenda(c) >= deUtc && DataDaVenda(c) < ateUtc && (status is null || c.Status == status))
            .OrderByDescending(DataDaVenda)
            .ThenByDescending(c => c.Numero)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Comanda>, int)>((filtradas.Skip(pular).Take(quantidade).ToList(), filtradas.Count));
    }

    private static DateTime DataDaVenda(Comanda c) => c.FechadaEm ?? c.CanceladaEm ?? c.CriadaEm;

    public Task<int> ProximoNumeroAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Where(c => c.CaixaId == caixaId).Select(c => c.Numero).DefaultIfEmpty(0).Max() + 1);

    public Task<bool> ExisteAbertaNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Any(c => c.CaixaId == caixaId && c.EstaAberta));

    public Task<decimal> TotalDinheiroFechadasNoCaixaAsync(Guid caixaId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Where(c => c.CaixaId == caixaId && c.EntraNoCaixa).Sum(c => c.TotalEmDinheiro));

    public Task<bool> ExisteNumeroNoCaixaAsync(Guid caixaId, int numero, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Any(c => c.CaixaId == caixaId && c.Numero == numero));

    public Task<bool> ExisteItemOuPagamentoAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult(Comandas.Any(c => c.Itens.Any(i => ids.Contains(i.Id)) || c.Pagamentos.Any(p => ids.Contains(p.Id))));

    public Task AdicionarAsync(Comanda comanda, CancellationToken cancellationToken = default)
    {
        Comandas.Add(comanda);
        return Task.CompletedTask;
    }
}
