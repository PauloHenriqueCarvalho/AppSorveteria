using GestaoSorveteria.Application.Clima;
using GestaoSorveteria.Contracts.Clima;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Coleta do clima (RN-CL-04), local da loja e situação (RN-CL-01/07).</summary>
public class ColetaClimaServiceTests
{
    // Quinta 15/10/2026, 12:00 em Brasília.
    private static readonly DateTime Agora = new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoje = new(2026, 10, 15);

    private readonly FonteClimaFake _fonte = new() { Hoje = Hoje };
    private readonly ClimaRepositoryFake _clima = new();
    private readonly ConfiguracaoLojaRepositoryFake _configuracao = new();
    private readonly ComandaRepositoryFake _comandas = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new() { UtcNow = Agora };
    private readonly SituacaoColetaClima _situacao = new();
    private readonly ColetaClimaService _coleta;
    private readonly ClimaService _service;

    public ColetaClimaServiceTests()
    {
        var configuracao = ConfiguracaoLoja.Padrao(Agora);
        configuracao.DefinirLocal("Ribeirão Preto, SP, Brasil", -21.1775m, -47.8103m, Agora);
        _configuracao.Configuracao = configuracao;
        _coleta = new ColetaClimaService(_fonte, _clima, _configuracao, _comandas, _uow, _clock, _situacao);
        _service = new ClimaService(_fonte, _clima, _configuracao, _uow, _clock, _situacao, _coleta);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Dia(int mes, int dia) => new(2026, mes, dia);

    private void PrimeiraVendaEm(DateOnly dia)
    {
        var fechadaEm = dia.ToDateTime(new TimeOnly(15, 0), DateTimeKind.Utc);
        var caixa = Caixa.Abrir(Cenario.Atendente, 100m, fechadaEm.AddHours(-1));
        var comanda = Comanda.Abrir(caixa, Cenario.Atendente, 1, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        comanda.AdicionarItemLivre("Venda avulsa", 10m);
        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, comanda.Total)], fechadaEm);
        _comandas.Comandas.Add(comanda);
    }

    private void Passar(TimeSpan tempo)
    {
        _clock.UtcNow += tempo;
        _fonte.Hoje = DateOnly.FromDateTime(_clock.UtcNow.AddHours(-3));
    }

    // ---------------------------------------------------------------- quando não coleta

    [Fact]
    public async Task Coletar_SemChave_NaoChamaOServico()
    {
        _fonte.Configurada = false;

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.False(resultado.Coletou);
        Assert.Contains("Clima__ChaveApi", resultado.Mensagem);
        Assert.Empty(_fonte.Chamadas);
    }

    [Fact]
    public async Task Coletar_SemLocal_NaoChamaOServico()
    {
        _configuracao.Configuracao = ConfiguracaoLoja.Padrao(Agora);

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.False(resultado.Coletou);
        Assert.Contains("local da loja", resultado.Mensagem);
        Assert.Empty(_fonte.Chamadas);
    }

    // ---------------------------------------------------------------- o que coleta

    [Fact]
    public async Task Coletar_SemVenda_UltimosSeteDiasEAPrevisaoNumaChamadaSo()
    {
        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.True(resultado.Coletou);
        Assert.Equal((Dia(10, 9), Dia(10, 29)), (_fonte.Chamadas.Single().De, _fonte.Chamadas.Single().Ate));
        Assert.Equal(PontoClima.De(-21.18m, -47.81m), _fonte.Chamadas.Single().Ponto);
        Assert.Equal((7, 14), (resultado.DiasGravados, resultado.PrevisoesGravadas));
        Assert.Equal(Enumerable.Range(9, 7).Select(d => Dia(10, d)), _clima.Dias.Select(d => d.Data).OrderBy(d => d));
        Assert.Equal(5, _clima.Dias.Count(d => d.Definitivo)); // 09 a 13; 14 e 15 ainda abertos
        Assert.All(_clima.Previsoes, p => Assert.Equal(Hoje, p.FeitaNoDia));
        Assert.Equal((Dia(10, 16), Dia(10, 29)), (_clima.Previsoes.Min(p => p.Data), _clima.Previsoes.Max(p => p.Data)));
        Assert.Equal("7 dias de clima guardados; previsão dos próximos 14 dias.", resultado.Mensagem);
        Assert.Equal(1, _uow.Confirmacoes);
        Assert.Equal(((DateTime?)Agora, (string?)null), (_situacao.UltimaColetaEm, _situacao.UltimoErro));
    }

    [Fact]
    public async Task Coletar_DesdeAPrimeiraVenda_JanelasDeAte31DiasMaisRecentesPrimeiro()
    {
        PrimeiraVendaEm(Dia(9, 1));

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Collection(_fonte.Chamadas,
            c => Assert.Equal((Dia(9, 15), Dia(10, 29)), (c.De, c.Ate)),  // 31 dias até hoje + a previsão
            c => Assert.Equal((Dia(9, 1), Dia(9, 14)), (c.De, c.Ate)));
        Assert.Equal(45, resultado.DiasGravados);
        Assert.Equal(2, _uow.Confirmacoes); // grava a cada janela
    }

    [Fact]
    public async Task Coletar_LogoDepois_NaoChamaNada()
    {
        await _coleta.ColetarAsync(forcar: false, Ct);
        Passar(TimeSpan.FromHours(2));

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.False(resultado.Coletou);
        Assert.Equal("O clima já está em dia.", resultado.Mensagem);
        Assert.Single(_fonte.Chamadas);
    }

    [Fact]
    public async Task Coletar_TresHorasDepois_SoOsDiasAindaAbertosSemPrevisaoDeNovo()
    {
        await _coleta.ColetarAsync(forcar: false, Ct);
        Passar(TimeSpan.FromHours(3));
        _fonte.MaximaPadrao = 33m;

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Equal((Dia(10, 14), Dia(10, 15)), (_fonte.Chamadas[1].De, _fonte.Chamadas[1].Ate));
        Assert.Equal((2, 0), (resultado.DiasGravados, resultado.PrevisoesGravadas));
        Assert.Equal(33m, _clima.Dias.Single(d => d.Data == Hoje).TempMax);
        Assert.Equal(28m, _clima.Dias.Single(d => d.Data == Dia(10, 13)).TempMax); // definitivo não muda
        Assert.Equal(7, _clima.Dias.Count);
    }

    [Fact]
    public async Task Coletar_NoDiaSeguinte_FechaODiaEGuardaANovaPrevisaoSemApagarAAnterior()
    {
        await _coleta.ColetarAsync(forcar: false, Ct);
        Passar(TimeSpan.FromDays(1));

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Equal((Dia(10, 14), Dia(10, 30)), (_fonte.Chamadas[1].De, _fonte.Chamadas[1].Ate));
        Assert.Equal((3, 14), (resultado.DiasGravados, resultado.PrevisoesGravadas));
        Assert.True(_clima.Dias.Single(d => d.Data == Dia(10, 14)).Definitivo);
        Assert.False(_clima.Dias.Single(d => d.Data == Dia(10, 15)).Definitivo);
        Assert.Equal(28, _clima.Previsoes.Count); // a feita em 15/10 continua lá (RN-CL-03)
        Assert.Equal(2, _clima.Previsoes.Count(p => p.Data == Dia(10, 20)));
    }

    [Fact]
    public async Task Coletar_Forcado_BuscaDeNovoOsDiasAbertosMesmoAntesDeTresHoras()
    {
        await _coleta.ColetarAsync(forcar: false, Ct);
        Passar(TimeSpan.FromMinutes(10));

        var resultado = await _coleta.ColetarAsync(forcar: true, Ct);

        Assert.Equal((Dia(10, 14), Dia(10, 15)), (_fonte.Chamadas[1].De, _fonte.Chamadas[1].Ate));
        Assert.Equal(2, resultado.DiasGravados);
    }

    [Fact]
    public async Task Coletar_MuitosDiasParaTras_NoMaximo120PorRodadaEORestoNaProxima()
    {
        PrimeiraVendaEm(Hoje.AddDays(-199)); // 200 dias com hoje

        var primeira = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Equal(120, primeira.DiasGravados);
        Assert.Equal(4, _fonte.Chamadas.Count); // 31 + 31 + 31 + 27
        Assert.Equal(Hoje.AddDays(-119), _clima.Dias.Min(d => d.Data));

        Passar(TimeSpan.FromHours(3));
        var segunda = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Equal(82, segunda.DiasGravados); // os 80 que faltavam + 14 e 15/10, ainda abertos
        Assert.Equal(200, _clima.Dias.Count);
    }

    [Fact]
    public async Task Coletar_VendaDeTresAnosAtras_NoMaximoDoisAnos()
    {
        PrimeiraVendaEm(Hoje.AddYears(-3));

        for (var i = 0; i < 8; i++)
        {
            await _coleta.ColetarAsync(forcar: false, Ct);
            Passar(TimeSpan.FromHours(1)); // mesmo dia: 12h → 20h
        }

        Assert.Equal(Hoje.AddDays(-(ColetaClimaService.MaximoDiasParaTras - 1)), _clima.Dias.Min(d => d.Data));
        Assert.Equal(ColetaClimaService.MaximoDiasParaTras, _clima.Dias.Count);
    }

    // ---------------------------------------------------------------- falhas

    [Fact]
    public async Task Coletar_ServicoCaiNaSegundaChamada_GuardaAPrimeiraERegistraOErro()
    {
        PrimeiraVendaEm(Dia(9, 1));
        _fonte.FalharNaChamada = 2;

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.True(resultado.Coletou);
        Assert.Equal((31, 14), (resultado.DiasGravados, resultado.PrevisoesGravadas));
        Assert.Contains("não respondeu", resultado.Mensagem);
        Assert.Contains("não respondeu", _situacao.UltimoErro);
        Assert.Null(_situacao.UltimaColetaEm);
        Assert.Equal(Agora, _situacao.UltimaTentativaEm);

        _fonte.FalharNaChamada = null;
        Passar(TimeSpan.FromMinutes(5));
        var depois = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Equal((Dia(9, 1), Dia(9, 14)), (_fonte.Chamadas[^1].De, _fonte.Chamadas[^1].Ate));
        Assert.Equal(14, depois.DiasGravados);
        Assert.Null(_situacao.UltimoErro);
    }

    [Fact]
    public async Task Coletar_DiaComDadoEstranho_FicaDeForaEOsOutrosSeguem()
    {
        _fonte.DiasEstranhos.Add(Dia(10, 12));

        var resultado = await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Equal(6, resultado.DiasGravados);
        Assert.DoesNotContain(_clima.Dias, d => d.Data == Dia(10, 12));
        Assert.Contains("1 dia com dado estranho ficou de fora", resultado.Mensagem);

        _fonte.DiasEstranhos.Clear();
        Passar(TimeSpan.FromMinutes(5));
        await _coleta.ColetarAsync(forcar: false, Ct);

        Assert.Contains(_clima.Dias, d => d.Data == Dia(10, 12)); // a próxima rodada tenta de novo
    }

    // ---------------------------------------------------------------- janelas

    [Fact]
    public void Janelas_DiasSoltos_UmaJanelaPorTrechoEAPrevisaoNaDeHoje()
    {
        var janelas = ColetaClimaService.Janelas([Dia(10, 15), Dia(10, 14), Dia(10, 10), Dia(10, 9)], precisaPrevisao: true, Hoje);

        Assert.Equal(new[] { (Dia(10, 14), Dia(10, 29)), (Dia(10, 9), Dia(10, 10)) }, janelas);
    }

    [Fact]
    public void Janelas_HojeEmDia_UmaJanelaSoDaPrevisao()
    {
        var janelas = ColetaClimaService.Janelas([Dia(10, 1)], precisaPrevisao: true, Hoje);

        Assert.Equal(new[] { (Dia(10, 16), Dia(10, 29)), (Dia(10, 1), Dia(10, 1)) }, janelas);
    }

    [Fact]
    public void Janelas_NadaPendente_Vazio() =>
        Assert.Empty(ColetaClimaService.Janelas([], precisaPrevisao: false, Hoje));

    // ---------------------------------------------------------------- local e situação (ClimaService)

    [Fact]
    public async Task ProcurarLocal_Encontrado_DevolveParaConfirmar()
    {
        _fonte.Local = new("Ribeirão Preto, SP, Brasil", -21.1775m, -47.8103m);

        var local = await _service.ProcurarLocalAsync("  Ribeirão Preto, SP ", Ct);

        Assert.Equal(new LocalLojaDto("Ribeirão Preto, SP, Brasil", -21.1775m, -47.8103m), local);
        Assert.Equal("Ribeirão Preto, SP", _fonte.ProcuradoPor);
    }

    [Fact]
    public async Task ProcurarLocal_NaoEncontrado_LancaComExemplo()
    {
        var erro = await Assert.ThrowsAsync<DomainException>(() => _service.ProcurarLocalAsync("Xyzópolis", Ct));

        Assert.Contains("Não encontramos \"Xyzópolis\"", erro.Message);
    }

    [Fact]
    public async Task ProcurarLocal_SemChaveOuTextoCurto_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() => _service.ProcurarLocalAsync("R", Ct));
        _fonte.Configurada = false;
        await Assert.ThrowsAsync<DomainException>(() => _service.ProcurarLocalAsync("Ribeirão Preto", Ct));
        Assert.Null(_fonte.ProcuradoPor);
    }

    [Fact]
    public async Task ProcurarLocal_ServicoRecusaAChave_ViraMensagemParaOPainel()
    {
        _fonte.FalharAoProcurar = true;

        var erro = await Assert.ThrowsAsync<DomainException>(() => _service.ProcurarLocalAsync("Ribeirão Preto", Ct));

        Assert.Contains("chave do serviço de clima foi recusada", erro.Message);
    }

    [Fact]
    public async Task DefinirLocal_SemConfiguracao_CriaJaColetaEDevolveASituacao()
    {
        _configuracao.Configuracao = null;

        var situacao = await _service.DefinirLocalAsync(new DefinirLocalLojaRequest("Franca, SP, Brasil", -20.5386m, -47.4009m), Ct);

        Assert.Equal(new LocalLojaDto("Franca, SP, Brasil", -20.5386m, -47.4009m), situacao.Local);
        Assert.Equal(PontoClima.De(-20.54m, -47.40m), _fonte.Chamadas.Single().Ponto);
        Assert.Equal(((DateOnly?)Dia(10, 9), (DateOnly?)Hoje, 7), (situacao.PrimeiroDia, situacao.UltimoDia, situacao.DiasComClima));
        Assert.Equal(Agora, situacao.UltimaColetaEm);
        Assert.True(situacao.ChaveConfigurada);
        Assert.Equal("Dados do clima: Teste", situacao.Atribuicao);
    }

    [Fact]
    public async Task DefinirLocal_OutroLugar_ColetaOPontoNovoSemMisturar()
    {
        await _coleta.ColetarAsync(forcar: false, Ct);

        var situacao = await _service.DefinirLocalAsync(new DefinirLocalLojaRequest("Franca, SP, Brasil", -20.5386m, -47.4009m), Ct);

        Assert.Equal(7, situacao.DiasComClima); // só os do ponto novo
        Assert.Equal(14, _clima.Dias.Count);
        Assert.Equal(28, _clima.Previsoes.Count);
    }

    [Fact]
    public async Task Situacao_SemLocal_SemDiasEComAChave()
    {
        _configuracao.Configuracao = null;

        var situacao = await _service.SituacaoAsync(Ct);

        Assert.Null(situacao.Local);
        Assert.Equal(((DateOnly?)null, (DateOnly?)null, 0), (situacao.PrimeiroDia, situacao.UltimoDia, situacao.DiasComClima));
        Assert.True(situacao.ChaveConfigurada);
        Assert.Null(situacao.UltimaColetaEm);
        Assert.Equal("https://exemplo.test/", situacao.AtribuicaoLink);
    }
}
