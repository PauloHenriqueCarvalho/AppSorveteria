using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Caixas;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Caixas;

/// <summary>
/// Ações da dona sobre o caixa pelo painel. Hoje só o fechamento forçado (RN-CX-09); abrir, sangria e o fechamento
/// normal acontecem no celular e chegam por <c>POST /api/sync/caixas</c>.
/// </summary>
public sealed class CaixaService
{
    private readonly ICaixaRepository _caixas;
    private readonly IComandaRepository _comandas;
    private readonly CaixaConsultaService _consulta;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public CaixaService(ICaixaRepository caixas, IComandaRepository comandas, CaixaConsultaService consulta, IUnitOfWork uow, IClock clock)
    {
        _caixas = caixas;
        _comandas = comandas;
        _consulta = consulta;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>
    /// RN-CX-09: fecha o caixa que o atendente esqueceu aberto. Contado = 0, vendas em dinheiro = as já recebidas
    /// deste caixa (mesma conta da RN-CX-10), diferença gravada para a dona acertar no próximo caixa.
    /// Caixa já fechado → 400 (RN-CX-07). Dois fechamentos ao mesmo tempo → o segundo falha pelo token de concorrência (409).
    /// </summary>
    public async Task<CaixaDetalheDto> FecharForcadoAsync(Guid id, FecharCaixaForcadoRequest request, Guid adminId, CancellationToken cancellationToken = default)
    {
        var caixa = await _caixas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Caixa não encontrado.");

        var vendasDinheiro = await _comandas.TotalDinheiroFechadasNoCaixaAsync(caixa.Id, cancellationToken);
        caixa.FecharForcado(vendasDinheiro, adminId, _clock.UtcNow, request.Motivo);
        await _uow.SaveChangesAsync(cancellationToken);

        return await _consulta.ObterAsync(caixa.Id, cancellationToken);
    }
}
