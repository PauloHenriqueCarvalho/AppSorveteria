using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Contracts.Clima;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Clima;

/// <summary>Local da loja e situação da coleta do clima, para a tela Configurações do painel (RN-CL-01/04/07). Só Admin.</summary>
public sealed class ClimaService
{
    /// <summary>Ao mudar o local, espera a rodada em segundo plano terminar por até esse tempo.</summary>
    public static readonly TimeSpan EsperaPelaColeta = TimeSpan.FromSeconds(30);

    private readonly IFonteClima _fonte;
    private readonly IClimaRepository _clima;
    private readonly IConfiguracaoLojaRepository _configuracao;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly SituacaoColetaClima _situacao;
    private readonly ColetaClimaService _coleta;

    public ClimaService(
        IFonteClima fonte,
        IClimaRepository clima,
        IConfiguracaoLojaRepository configuracao,
        IUnitOfWork uow,
        IClock clock,
        SituacaoColetaClima situacao,
        ColetaClimaService coleta)
    {
        _fonte = fonte;
        _clima = clima;
        _configuracao = configuracao;
        _uow = uow;
        _clock = clock;
        _situacao = situacao;
        _coleta = coleta;
    }

    public async Task<SituacaoClimaDto> SituacaoAsync(CancellationToken cancellationToken = default)
    {
        var configuracao = await _configuracao.ObterAsync(cancellationToken);
        var local = Local(configuracao);
        var guardado = configuracao?.PontoClima is { } ponto
            ? await _clima.ResumoAsync(ponto, cancellationToken)
            : new ResumoClimaGuardado(null, null, 0);
        var (tentativa, coleta, erro) = _situacao.Ler();

        return new SituacaoClimaDto(
            _fonte.Configurada,
            local,
            coleta,
            tentativa,
            erro,
            guardado.PrimeiroDia,
            guardado.UltimoDia,
            guardado.Dias,
            _fonte.Nome,
            _fonte.Atribuicao,
            _fonte.AtribuicaoLink);
    }

    /// <summary>RN-CL-01: o lugar que o serviço de clima encontrou pelo texto, para a dona conferir antes de gravar.</summary>
    /// <exception cref="DomainException">Texto curto, coleta desligada, lugar não encontrado ou falha no serviço de clima.</exception>
    public async Task<LocalLojaDto> ProcurarLocalAsync(string? texto, CancellationToken cancellationToken = default)
    {
        var procurado = (texto ?? string.Empty).Trim();
        if (procurado.Length < 2 || procurado.Length > 120)
        {
            throw new DomainException("Escreva a cidade e o estado (ex.: Ribeirão Preto, SP).");
        }

        if (!_fonte.Configurada)
        {
            throw new DomainException(ColetaClimaService.MensagemSemChave);
        }

        LocalEncontrado? encontrado;
        try
        {
            encontrado = await _fonte.ProcurarLocalAsync(procurado, cancellationToken);
        }
        catch (FonteClimaException ex)
        {
            throw new DomainException(ex.Message);
        }

        return encontrado is null
            ? throw new DomainException($"Não encontramos \"{procurado}\". Escreva a cidade e o estado (ex.: Ribeirão Preto, SP).")
            : new LocalLojaDto(encontrado.Nome, encontrado.Latitude, encontrado.Longitude);
    }

    /// <summary>
    /// RN-CL-01: grava o local confirmado pela dona e já coleta o que falta (RN-CL-04), forçando: o ponto novo começa com o
    /// histórico desde a primeira venda. Se a rodada em segundo plano está no meio, espera até 30 s por ela. O local fica gravado
    /// mesmo se a coleta falhar: o erro aparece na situação devolvida e a próxima rodada tenta de novo.
    /// </summary>
    public async Task<SituacaoClimaDto> DefinirLocalAsync(DefinirLocalLojaRequest request, CancellationToken cancellationToken = default)
    {
        var configuracao = await _configuracao.ObterAsync(cancellationToken);
        if (configuracao is null)
        {
            configuracao = ConfiguracaoLoja.Padrao(_clock.UtcNow);
            await _configuracao.AdicionarAsync(configuracao, cancellationToken);
        }

        configuracao.DefinirLocal(request.Nome, request.Latitude, request.Longitude, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);

        try
        {
            await _coleta.ColetarAsync(forcar: true, EsperaPelaColeta, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A coleta já registrou o erro na situação (RN-CL-04); o local está gravado.
        }

        return await SituacaoAsync(cancellationToken);
    }

    internal static LocalLojaDto? Local(ConfiguracaoLoja? configuracao) =>
        configuracao is { LocalNome: { } nome, Latitude: { } latitude, Longitude: { } longitude }
            ? new LocalLojaDto(nome, latitude, longitude)
            : null;
}
