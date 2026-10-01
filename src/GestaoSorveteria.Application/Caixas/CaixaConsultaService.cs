using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Caixas;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Caixas;

/// <summary>Leitura dos caixas para o painel da dona (docs/03 §6.1). Não altera nada.</summary>
public sealed class CaixaConsultaService
{
    /// <summary>Maior período aceito no histórico, para a consulta não ficar pesada no plano gratuito.</summary>
    public const int MaximoDiasPeriodo = 93;

    /// <summary>Sem datas: os últimos 30 dias, até hoje.</summary>
    public const int DiasPadrao = 30;

    private readonly ICaixaRepository _caixas;
    private readonly IComandaRepository _comandas;
    private readonly IUsuarioRepository _usuarios;
    private readonly IClock _clock;

    public CaixaConsultaService(ICaixaRepository caixas, IComandaRepository comandas, IUsuarioRepository usuarios, IClock clock)
    {
        _caixas = caixas;
        _comandas = comandas;
        _usuarios = usuarios;
        _clock = clock;
    }

    /// <summary>
    /// Caixas abertos entre os dias comerciais <paramref name="deDia"/> e <paramref name="ateDia"/> (inclusive, RN-RL-02),
    /// mais recente primeiro. Sem <paramref name="ateDia"/> = hoje; sem <paramref name="deDia"/> = <see cref="DiasPadrao"/> dias antes.
    /// </summary>
    public async Task<IReadOnlyList<CaixaResumoDto>> ListarAsync(DateOnly? deDia, DateOnly? ateDia, CancellationToken cancellationToken = default)
    {
        var ate = ateDia ?? DiaComercial.DataDe(_clock.UtcNow);
        var de = deDia ?? ate.AddDays(-(DiasPadrao - 1));
        PeriodoConsulta.Validar(de, ate, _clock.UtcNow, MaximoDiasPeriodo);

        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(de, ate);
        var caixas = await _caixas.ListarAsync(inicioUtc, fimUtc, cancellationToken);
        var nomes = await NomesAsync(cancellationToken);

        var resumos = new List<CaixaResumoDto>(caixas.Count);
        foreach (var caixa in caixas.OrderByDescending(c => c.AbertoEm))
        {
            var comandas = await _comandas.ListarPorCaixaAsync(caixa.Id, cancellationToken: cancellationToken);
            resumos.Add(Resumir(caixa, comandas, nomes));
        }

        return resumos;
    }

    /// <summary>O caixa aberto agora (RN-CX-01), ou null. Mostra o último estado recebido do app.</summary>
    public async Task<CaixaResumoDto?> ObterAtualAsync(CancellationToken cancellationToken = default)
    {
        var caixa = await _caixas.ObterAbertoAsync(cancellationToken);
        if (caixa is null)
        {
            return null;
        }

        var comandas = await _comandas.ListarPorCaixaAsync(caixa.Id, cancellationToken: cancellationToken);
        return Resumir(caixa, comandas, await NomesAsync(cancellationToken));
    }

    public async Task<CaixaDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var caixa = await _caixas.ObterPorIdAsync(id, cancellationToken)
            ?? throw new EntidadeNaoEncontradaException("Caixa não encontrado.");
        var comandas = await _comandas.ListarPorCaixaAsync(caixa.Id, cancellationToken: cancellationToken);
        var nomes = await NomesAsync(cancellationToken);

        var movimentos = caixa.Movimentos
            .OrderBy(m => m.Em)
            .Select(m => new MovimentoCaixaDto(m.Id, m.Tipo.ToString(), m.Valor, m.Motivo, Nome(nomes, m.UsuarioId), m.Em))
            .ToList();

        return new CaixaDetalheDto(Resumir(caixa, comandas, nomes), movimentos, TotaisPorForma(comandas), caixa.Observacao);
    }

    /// <summary>Soma dos pagamentos das comandas fechadas, por forma, na ordem do enum (Dinheiro, Pix, cartões).</summary>
    public static IReadOnlyList<TotalPorFormaDto> TotaisPorForma(IEnumerable<Comanda> comandas) =>
        comandas
            .Where(c => c.Status == StatusComanda.Fechada)
            .SelectMany(c => c.Pagamentos)
            .GroupBy(p => p.Forma)
            .OrderBy(g => g.Key)
            .Select(g => new TotalPorFormaDto(g.Key.ToString(), Moeda.Arredondar(g.Sum(p => p.Valor)), g.Count()))
            .ToList();

    private static CaixaResumoDto Resumir(Caixa caixa, IReadOnlyList<Comanda> comandas, IReadOnlyDictionary<Guid, string> nomes)
    {
        var fechadas = comandas.Where(c => c.Status == StatusComanda.Fechada).ToList();
        var totalVendas = Moeda.Arredondar(fechadas.Sum(c => c.Total));

        decimal? vendasDinheiro;
        decimal? esperado;
        if (caixa.EstaAberto)
        {
            // Parcial: mesma conta do fechamento (RN-CX-06), com as vendas já recebidas do app.
            vendasDinheiro = Moeda.Arredondar(fechadas.Sum(c => c.TotalEmDinheiro));
            esperado = caixa.CalcularEsperado(vendasDinheiro.Value);
        }
        else
        {
            // RN-CX-07: o gravado no fechamento não muda, mesmo que cheguem vendas depois (RN-CX-08).
            vendasDinheiro = caixa.TotalVendasDinheiro;
            esperado = caixa.ValorEsperado;
        }

        return new CaixaResumoDto(
            caixa.Id,
            caixa.Status.ToString(),
            caixa.AbertoEm,
            Nome(nomes, caixa.AbertoPorUsuarioId),
            caixa.FundoTroco,
            caixa.FechadoEm,
            caixa.FechadoPorUsuarioId is { } fechadoPor ? Nome(nomes, fechadoPor) : null,
            totalVendas,
            fechadas.Count,
            vendasDinheiro,
            esperado,
            caixa.ValorContado,
            caixa.Diferenca,
            caixa.DivergenciaSincronizacao,
            comandas.Count(c => c.RecebidaAposFechamentoCaixa),
            Moeda.Arredondar(fechadas.Where(c => c.RecebidaAposFechamentoCaixa).Sum(c => c.TotalEmDinheiro)));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> NomesAsync(CancellationToken cancellationToken) =>
        (await _usuarios.ListarAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Nome);

    private static string Nome(IReadOnlyDictionary<Guid, string> nomes, Guid usuarioId) =>
        nomes.TryGetValue(usuarioId, out var nome) ? nome : "Usuário desconhecido";
}
