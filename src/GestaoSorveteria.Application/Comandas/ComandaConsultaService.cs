using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Comandas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Comandas;

/// <summary>Leitura das vendas para o painel da dona (docs/03 §6.1). Não altera nada.</summary>
public sealed class ComandaConsultaService
{
    public const int MaximoDiasPeriodo = 93;
    public const int TamanhoPadrao = 50;
    public const int TamanhoMaximo = 100;

    private readonly IComandaRepository _comandas;
    private readonly IUsuarioRepository _usuarios;
    private readonly IClock _clock;

    public ComandaConsultaService(IComandaRepository comandas, IUsuarioRepository usuarios, IClock clock)
    {
        _comandas = comandas;
        _usuarios = usuarios;
        _clock = clock;
    }

    /// <summary>
    /// Vendas cujo dia comercial (RN-RL-02) da venda — fechamento, cancelamento ou, se ainda aberta, criação — está entre
    /// <paramref name="deDia"/> e <paramref name="ateDia"/> (inclusive). Sem datas = hoje. <paramref name="status"/>: "Fechada", "Cancelada"...
    /// </summary>
    public async Task<PaginaDto<ComandaResumoDto>> ListarAsync(
        DateOnly? deDia,
        DateOnly? ateDia,
        string? status,
        int? pagina,
        int? tamanho,
        CancellationToken cancellationToken = default)
    {
        var ate = ateDia ?? DiaComercial.DataDe(_clock.UtcNow);
        var de = deDia ?? ate;
        PeriodoConsulta.Validar(de, ate, _clock.UtcNow, MaximoDiasPeriodo);

        var numeroPagina = pagina ?? 1;
        var itensPorPagina = tamanho ?? TamanhoPadrao;
        if (numeroPagina < 1 || itensPorPagina < 1 || itensPorPagina > TamanhoMaximo)
        {
            throw new DomainException($"Página deve ser 1 ou mais e o tamanho de 1 a {TamanhoMaximo}.");
        }

        var filtro = LerStatus(status);
        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(de, ate);
        var (comandas, total) = await _comandas.ListarPorPeriodoAsync(
            inicioUtc, fimUtc, filtro, (numeroPagina - 1) * itensPorPagina, itensPorPagina, cancellationToken);
        var nomes = await NomesAsync(cancellationToken);

        var itens = comandas
            .Select(c => new ComandaResumoDto(
                c.Id,
                c.Numero,
                c.CaixaId,
                c.Tipo.ToString(),
                c.Status.ToString(),
                c.Total,
                c.CriadaEm,
                c.FechadaEm,
                Nome(nomes, c.UsuarioId),
                c.Pagamentos.Select(p => p.Forma).Distinct().Order().Select(f => f.ToString()).ToList(),
                c.RecebidaAposFechamentoCaixa))
            .ToList();

        return new PaginaDto<ComandaResumoDto>(itens, numeroPagina, itensPorPagina, total);
    }

    public async Task<ComandaDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var comanda = await _comandas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Venda não encontrada.");
        var nomes = await NomesAsync(cancellationToken);

        return new ComandaDetalheDto(
            comanda.Id,
            comanda.Numero,
            comanda.CaixaId,
            comanda.Tipo.ToString(),
            comanda.Status.ToString(),
            comanda.Total,
            comanda.Observacao,
            comanda.CriadaEm,
            comanda.RecebidaEm,
            comanda.FechadaEm,
            comanda.CanceladaEm,
            comanda.MotivoCancelamento,
            comanda.EstornadaEm,
            comanda.EstornadaPorUsuarioId is { } estornadaPor ? Nome(nomes, estornadaPor) : null,
            comanda.MotivoEstorno,
            comanda.RecebidaAposFechamentoCaixa,
            Nome(nomes, comanda.UsuarioId),
            comanda.Itens
                .Select(i => new ItemComandaDto(i.Id, i.ProdutoId, i.Descricao, i.Quantidade, i.PrecoUnitario, i.Subtotal))
                .ToList(),
            comanda.Pagamentos
                .OrderBy(p => p.Forma)
                .Select(p => new PagamentoDto(p.Id, p.Forma.ToString(), p.Valor, p.ValorRecebido, p.Troco))
                .ToList());
    }

    private static StatusComanda? LerStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse<StatusComanda>(status, ignoreCase: true, out var valor) && Enum.IsDefined(valor) && !int.TryParse(status, out _))
        {
            return valor;
        }

        throw new DomainException($"Situação inválida: \"{status}\". Use {string.Join(", ", Enum.GetNames<StatusComanda>())}.");
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NomesAsync(CancellationToken cancellationToken) =>
        (await _usuarios.ListarAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Nome);

    private static string Nome(IReadOnlyDictionary<Guid, string> nomes, Guid usuarioId) =>
        nomes.TryGetValue(usuarioId, out var nome) ? nome : "Usuário desconhecido";
}
