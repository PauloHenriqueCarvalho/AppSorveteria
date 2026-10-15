using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Clima;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Clima;

/// <summary>
/// RN-CL-04: completa o clima que falta — dias sem clima ou ainda não definitivos desde o primeiro dia com venda até hoje, e
/// a previsão do dia se ainda não foi guardada. Chamada pelo serviço em segundo plano da API (ao ligar e a cada 3 h) e pelo
/// "Atualizar agora" do painel. Sem horário marcado: a API dormindo por dias não perde nada, a próxima rodada busca o histórico.
/// </summary>
public sealed class ColetaClimaService
{
    /// <summary>Dias por chamada ao serviço de clima.</summary>
    public const int DiasPorConsulta = 31;

    /// <summary>Dias do clima do dia por rodada (os mais recentes primeiro); o resto vem nas próximas.</summary>
    public const int MaximoDiasPorRodada = 120;

    /// <summary>Sem venda ainda: coleta os últimos 7 dias.</summary>
    public const int DiasSemVenda = 7;

    /// <summary>No máximo 2 anos para trás.</summary>
    public const int MaximoDiasParaTras = 731;

    private readonly IFonteClima _fonte;
    private readonly IClimaRepository _clima;
    private readonly IConfiguracaoLojaRepository _configuracao;
    private readonly IComandaRepository _comandas;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly SituacaoColetaClima _situacao;

    public ColetaClimaService(
        IFonteClima fonte,
        IClimaRepository clima,
        IConfiguracaoLojaRepository configuracao,
        IComandaRepository comandas,
        IUnitOfWork uow,
        IClock clock,
        SituacaoColetaClima situacao)
    {
        _fonte = fonte;
        _clima = clima;
        _configuracao = configuracao;
        _comandas = comandas;
        _uow = uow;
        _clock = clock;
        _situacao = situacao;
    }

    /// <summary>Mensagem da coleta desligada (sem a chave no servidor), sem jargão: aparece para a dona.</summary>
    public const string MensagemSemChave =
        "A coleta do clima ainda não foi ligada no servidor: falta configurar a chave do serviço de clima.";

    /// <summary>Banco fora do ar ou gravação recusada no meio da coleta (o erro completo fica no log).</summary>
    public const string MensagemFalhaAoGravar = "Não deu para gravar o clima agora. A próxima rodada tenta de novo.";

    /// <param name="forcar">"Atualizar agora": coleta de novo os dias não definitivos mesmo se a última coleta tem menos de 3 h.</param>
    public Task<ColetaClimaDto> ColetarAsync(bool forcar, CancellationToken cancellationToken = default) =>
        ColetarAsync(forcar, TimeSpan.Zero, cancellationToken);

    /// <param name="forcar">"Atualizar agora": coleta de novo os dias não definitivos mesmo se a última coleta tem menos de 3 h.</param>
    /// <param name="esperarVez">Se outra coleta está rodando, espera até esse tempo antes de desistir (a dona acabou de mudar o local).</param>
    /// <exception cref="Exception">Falha no banco: registrada na situação (aparece no painel) e relançada para o log.</exception>
    public async Task<ColetaClimaDto> ColetarAsync(bool forcar, TimeSpan esperarVez, CancellationToken cancellationToken = default)
    {
        if (!_fonte.Configurada)
        {
            return new ColetaClimaDto(false, 0, 0, MensagemSemChave);
        }

        var configuracao = await _configuracao.ObterAsync(cancellationToken);
        if (configuracao?.PontoClima is not { } ponto)
        {
            return new ColetaClimaDto(false, 0, 0, "Informe o local da loja em Configurações para começar a guardar o clima.");
        }

        if (!await _situacao.UmaPorVez.WaitAsync(esperarVez, cancellationToken))
        {
            return new ColetaClimaDto(false, 0, 0, "Já tem uma coleta do clima em andamento. Tente de novo em instantes.");
        }

        try
        {
            return await ColetarNoPontoAsync(ponto, forcar, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not FonteClimaException)
        {
            // RN-CL-04: o erro aparece no painel; o detalhe vai para o log por quem chamou.
            _situacao.Registrar(_clock.UtcNow, MensagemFalhaAoGravar);
            throw;
        }
        finally
        {
            _situacao.UmaPorVez.Release();
        }
    }

    private async Task<ColetaClimaDto> ColetarNoPontoAsync(PontoClima ponto, bool forcar, CancellationToken cancellationToken)
    {
        var agora = _clock.UtcNow;
        var hoje = DiaComercial.DataDe(agora);
        var inicio = await InicioAsync(ponto, hoje, cancellationToken);

        var guardados = (await _clima.ListarDiasAsync(ponto, inicio, hoje, cancellationToken)).ToDictionary(d => d.Data);
        var pendentes = Enumerable.Range(0, hoje.DayNumber - inicio.DayNumber + 1)
            .Select(i => hoje.AddDays(-i))
            .Where(d => !guardados.TryGetValue(d, out var dia) || dia.PrecisaColetar(agora, forcar))
            .Take(MaximoDiasPorRodada)
            .ToHashSet();
        var precisaPrevisao = !await _clima.ExistePrevisaoFeitaNoDiaAsync(ponto, hoje, cancellationToken);

        var janelas = Janelas(pendentes, precisaPrevisao, hoje);
        if (janelas.Count == 0)
        {
            _situacao.Registrar(agora, null);
            return new ColetaClimaDto(false, 0, 0, "O clima já está em dia.");
        }

        int dias = 0, previsoes = 0, recusados = 0;
        foreach (var (de, ate) in janelas)
        {
            IReadOnlyList<LeituraClima> leituras;
            try
            {
                leituras = await _fonte.LerDiasAsync(ponto, de, ate, cancellationToken);
            }
            catch (FonteClimaException ex)
            {
                // O que já veio nas janelas anteriores está gravado; a próxima rodada tenta o resto.
                _situacao.Registrar(agora, ex.Message);
                return new ColetaClimaDto(true, dias, previsoes, ex.Message, Falhou: true);
            }

            var novasPrevisoes = new List<PrevisaoClima>();
            foreach (var leitura in leituras.Where(l => l.Data >= de && l.Data <= ate))
            {
                try
                {
                    if (leitura.Data <= hoje && pendentes.Remove(leitura.Data))
                    {
                        if (guardados.TryGetValue(leitura.Data, out var guardado))
                        {
                            guardado.Atualizar(leitura, _fonte.Nome, agora, hoje);
                        }
                        else
                        {
                            await _clima.AdicionarDiaAsync(ClimaDia.Registrar(ponto, leitura, _fonte.Nome, agora, hoje), cancellationToken);
                        }

                        dias++;
                    }
                    else if (leitura.Data > hoje && precisaPrevisao && leitura.Data.DayNumber <= hoje.DayNumber + PrevisaoClima.DiasAFrente)
                    {
                        novasPrevisoes.Add(PrevisaoClima.Registrar(ponto, hoje, agora, leitura, _fonte.Nome));
                    }
                }
                catch (DomainException)
                {
                    // Dado estranho num dia (ex.: máxima abaixo da mínima): fica de fora, os outros dias seguem.
                    recusados++;
                }
            }

            if (novasPrevisoes.Count > 0)
            {
                await _clima.AdicionarPrevisoesAsync(novasPrevisoes, cancellationToken);
                previsoes += novasPrevisoes.Count;
                precisaPrevisao = false;
            }

            await _uow.SaveChangesAsync(cancellationToken);
        }

        _situacao.Registrar(agora, null);
        return new ColetaClimaDto(true, dias, previsoes, Mensagem(dias, previsoes, recusados));
    }

    /// <summary>
    /// Desde o primeiro dia com venda (sem venda: os últimos 7 dias), ou antes, se há dia guardado que ainda não é definitivo
    /// (local informado antes da primeira venda, API parada vários dias) — senão ele ficaria aberto para sempre. No máximo 2 anos.
    /// </summary>
    private async Task<DateOnly> InicioAsync(PontoClima ponto, DateOnly hoje, CancellationToken cancellationToken)
    {
        var primeiraVenda = await _comandas.PrimeiraVendaEmAsync(cancellationToken);
        var inicio = primeiraVenda is { } em ? DiaComercial.DataDe(em) : hoje.AddDays(-(DiasSemVenda - 1));
        if (await _clima.PrimeiroDiaAbertoAsync(ponto, cancellationToken) is { } aberto && aberto < inicio)
        {
            inicio = aberto;
        }

        var limite = hoje.AddDays(-(MaximoDiasParaTras - 1));
        if (inicio < limite)
        {
            inicio = limite;
        }

        return inicio > hoje ? hoje : inicio;
    }

    /// <summary>
    /// RN-CL-04: as chamadas ao serviço de clima — dias pendentes seguidos, até <see cref="DiasPorConsulta"/> por chamada, os
    /// mais recentes primeiro. Sem a previsão do dia guardada, a janela de hoje vai até hoje + 14 (uma chamada traz os dois); se
    /// hoje não está pendente, uma janela só da previsão.
    /// </summary>
    public static IReadOnlyList<(DateOnly De, DateOnly Ate)> Janelas(IReadOnlyCollection<DateOnly> pendentes, bool precisaPrevisao, DateOnly hoje)
    {
        var janelas = new List<(DateOnly De, DateOnly Ate)>();
        foreach (var dia in pendentes.OrderByDescending(d => d))
        {
            if (janelas.Count > 0)
            {
                var (de, ate) = janelas[^1];
                if (dia.DayNumber == de.DayNumber - 1 && ate.DayNumber - dia.DayNumber + 1 <= DiasPorConsulta)
                {
                    janelas[^1] = (dia, ate);
                    continue;
                }
            }

            janelas.Add((dia, dia));
        }

        if (precisaPrevisao)
        {
            var fimDaPrevisao = hoje.AddDays(PrevisaoClima.DiasAFrente);
            var indiceDeHoje = janelas.FindIndex(j => j.Ate == hoje);
            if (indiceDeHoje >= 0)
            {
                janelas[indiceDeHoje] = (janelas[indiceDeHoje].De, fimDaPrevisao);
            }
            else
            {
                janelas.Insert(0, (hoje.AddDays(1), fimDaPrevisao));
            }
        }

        return janelas;
    }

    private static string Mensagem(int dias, int previsoes, int recusados)
    {
        var partes = new List<string>
        {
            dias == 1 ? "1 dia de clima guardado" : $"{dias} dias de clima guardados",
        };
        if (previsoes > 0)
        {
            partes.Add($"previsão dos próximos {previsoes} dias");
        }

        if (recusados > 0)
        {
            partes.Add(recusados == 1 ? "1 dia com dado estranho ficou de fora" : $"{recusados} dias com dado estranho ficaram de fora");
        }

        return string.Join("; ", partes) + ".";
    }
}
