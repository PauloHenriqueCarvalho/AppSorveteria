using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Loja;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Clima e calendário (docs/02, seção 9b).</summary>
public class ClimaTests
{
    private static readonly DateTime Agora = new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoje = new(2026, 10, 15);
    private static readonly PontoClima Ponto = PontoClima.De(-21.1775m, -47.8103m);

    private static LeituraClima Leitura(DateOnly dia, decimal maxima = 30.04m, decimal minima = 18.96m, OrigemClima origem = OrigemClima.Observado) =>
        new(dia, maxima, minima, 24.25m, 31.55m, 2.35m, 12.5m, 40m, 61.2m, 30m, "rain", "Chuva, Parcialmente nublado", origem);

    // ---------------------------------------------------------------- RN-CL-01: ponto e local

    [Fact]
    public void PontoClima_De_ArredondaParaDuasCasas()
    {
        Assert.Equal(-21.18m, Ponto.Latitude);
        Assert.Equal(-47.81m, Ponto.Longitude);
        Assert.Equal("-21.18,-47.81", Ponto.ToString());
    }

    [Fact]
    public void PontoClima_MesmoLugarComCasasDiferentes_EhOMesmoPonto() =>
        Assert.Equal(PontoClima.De(-21.1801m, -47.8099m), Ponto);

    [Theory]
    [InlineData(-90.5, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 180.1)]
    public void PontoClima_ForaDaFaixa_Lanca(double latitude, double longitude) =>
        Assert.Throws<DomainException>(() => PontoClima.De((decimal)latitude, (decimal)longitude));

    [Fact]
    public void DefinirLocal_GuardaNomeECoordenadasComQuatroCasas_EOPontoComDuas()
    {
        var configuracao = ConfiguracaoLoja.Padrao(Agora);

        configuracao.DefinirLocal("  Ribeirão Preto, SP, Brasil ", -21.177512m, -47.810349m, Agora);

        Assert.Equal("Ribeirão Preto, SP, Brasil", configuracao.LocalNome);
        Assert.Equal(-21.1775m, configuracao.Latitude);
        Assert.Equal(-47.8103m, configuracao.Longitude);
        Assert.Equal(Ponto, configuracao.PontoClima);
    }

    [Fact]
    public void PontoClima_SemLocal_EhNulo() =>
        Assert.Null(ConfiguracaoLoja.Padrao(Agora).PontoClima);

    [Fact]
    public void DefinirLocal_CoordenadaInvalida_Lanca()
    {
        var configuracao = ConfiguracaoLoja.Padrao(Agora);

        Assert.Throws<DomainException>(() => configuracao.DefinirLocal("Lugar", -95m, 0m, Agora));
        Assert.Null(configuracao.LocalNome);
    }

    // ---------------------------------------------------------------- RN-CL-02: clima do dia

    [Fact]
    public void Registrar_ArredondaParaUmaCasaEGuardaOPonto()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje.AddDays(-5)), "Teste", Agora, Hoje);

        Assert.Equal(30.0m, dia.TempMax);
        Assert.Equal(19.0m, dia.TempMin);
        Assert.Equal(24.3m, dia.TempMedia);
        Assert.Equal(31.6m, dia.SensacaoMax);
        Assert.Equal(2.4m, dia.ChuvaMm);
        Assert.Equal(Ponto, dia.Ponto);
        Assert.Equal("Teste", dia.Fonte);
        Assert.Equal(Agora, dia.ColetadoEm);
    }

    [Theory]
    [InlineData(0, false)]  // hoje: ainda aberto
    [InlineData(1, false)]  // ontem: o serviço ainda corrige
    [InlineData(2, true)]   // anteontem: fecha
    [InlineData(30, true)]
    public void Registrar_DefinitivoSoDepoisDeDoisDias(int diasAtras, bool definitivo) =>
        Assert.Equal(definitivo, ClimaDia.Registrar(Ponto, Leitura(Hoje.AddDays(-diasAtras)), "Teste", Agora, Hoje).Definitivo);

    [Fact]
    public void Registrar_DiaAntigoAindaPrevisto_NaoEhDefinitivo() =>
        Assert.False(ClimaDia.Registrar(Ponto, Leitura(Hoje.AddDays(-5), origem: OrigemClima.Previsto), "Teste", Agora, Hoje).Definitivo);

    [Fact]
    public void Registrar_DiaFuturo_LancaPorqueEhPrevisao() =>
        Assert.Throws<DomainException>(() => ClimaDia.Registrar(Ponto, Leitura(Hoje.AddDays(1)), "Teste", Agora, Hoje));

    [Fact]
    public void Registrar_MaximaAbaixoDaMinima_Lanca() =>
        Assert.Throws<DomainException>(() => ClimaDia.Registrar(Ponto, Leitura(Hoje, maxima: 15m, minima: 20m), "Teste", Agora, Hoje));

    [Fact]
    public void Registrar_PercentualForaDeZeroACem_Lanca() =>
        Assert.Throws<DomainException>(() =>
            ClimaDia.Registrar(Ponto, Leitura(Hoje) with { UmidadePct = 140m }, "Teste", Agora, Hoje));

    [Fact]
    public void Registrar_ChuvaNegativa_Lanca() =>
        Assert.Throws<DomainException>(() =>
            ClimaDia.Registrar(Ponto, Leitura(Hoje) with { ChuvaMm = -1m }, "Teste", Agora, Hoje));

    [Fact]
    public void Registrar_TextoComprido_CortaNoTamanhoDaColuna()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje) with { Condicao = new string('x', 300), Icone = "  " }, "Teste", Agora, Hoje);

        Assert.Equal(120, dia.Condicao!.Length);
        Assert.Null(dia.Icone);
    }

    [Fact]
    public void Atualizar_DiaQueFechou_FicaDefinitivo()
    {
        var ontem = Hoje.AddDays(-1);
        var dia = ClimaDia.Registrar(Ponto, Leitura(ontem, maxima: 29m), "Teste", Agora, Hoje);

        dia.Atualizar(Leitura(ontem, maxima: 31m), "Teste", Agora.AddDays(1), Hoje.AddDays(1));

        Assert.Equal(31m, dia.TempMax);
        Assert.True(dia.Definitivo);
        Assert.Equal(Agora.AddDays(1), dia.ColetadoEm);
    }

    [Fact]
    public void Atualizar_Definitivo_LancaENaoMuda()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje.AddDays(-10), maxima: 29m), "Teste", Agora, Hoje);

        Assert.Throws<DomainException>(() => dia.Atualizar(Leitura(Hoje.AddDays(-10), maxima: 35m), "Teste", Agora, Hoje));
        Assert.Equal(29m, dia.TempMax);
    }

    [Fact]
    public void Atualizar_ValorEstranho_NaoMudaNada()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje, maxima: 29m), "Teste", Agora, Hoje);

        Assert.Throws<DomainException>(() => dia.Atualizar(Leitura(Hoje, maxima: 29m) with { NuvensPct = 101m, TempMedia = 10m }, "Outra", Agora.AddHours(4), Hoje));

        Assert.Equal(24.3m, dia.TempMedia);
        Assert.Equal("Teste", dia.Fonte);
        Assert.Equal(Agora, dia.ColetadoEm);
    }

    [Fact]
    public void Atualizar_LeituraDeOutroDia_Lanca()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje), "Teste", Agora, Hoje);

        Assert.Throws<DomainException>(() => dia.Atualizar(Leitura(Hoje.AddDays(-1)), "Teste", Agora, Hoje));
    }

    [Fact]
    public void PrecisaColetar_NaoDefinitivo_SoDepoisDeTresHorasOuForcado()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje), "Teste", Agora, Hoje);

        Assert.False(dia.PrecisaColetar(Agora.AddHours(2).AddMinutes(59), forcar: false));
        Assert.True(dia.PrecisaColetar(Agora.AddHours(3), forcar: false));
        Assert.True(dia.PrecisaColetar(Agora.AddMinutes(1), forcar: true));
    }

    [Fact]
    public void PrecisaColetar_Definitivo_NuncaMesmoForcado()
    {
        var dia = ClimaDia.Registrar(Ponto, Leitura(Hoje.AddDays(-3)), "Teste", Agora, Hoje);

        Assert.False(dia.PrecisaColetar(Agora.AddDays(30), forcar: true));
    }

    // ---------------------------------------------------------------- RN-CL-03: previsão guardada

    [Fact]
    public void Previsao_GuardaComoEstavaEAAntecedencia()
    {
        var previsao = PrevisaoClima.Registrar(Ponto, Hoje, Agora, Leitura(Hoje.AddDays(3), origem: OrigemClima.Previsto), "Teste");

        Assert.Equal(Hoje, previsao.FeitaNoDia);
        Assert.Equal(Hoje.AddDays(3), previsao.Data);
        Assert.Equal(3, previsao.Antecedencia);
        Assert.Equal(30.0m, previsao.TempMax);
        Assert.Equal(30m, previsao.ChanceChuvaPct);
        Assert.Equal(-21.18m, previsao.Latitude);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(15)]
    public void Previsao_ForaDosProximosQuatorzeDias_Lanca(int diasAFrente) =>
        Assert.Throws<DomainException>(() =>
            PrevisaoClima.Registrar(Ponto, Hoje, Agora, Leitura(Hoje.AddDays(diasAFrente), origem: OrigemClima.Previsto), "Teste"));

    // ---------------------------------------------------------------- RN-CL-05: feriados

    [Theory]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    public void Pascoa_DatasConhecidas(int ano, int mes, int dia) =>
        Assert.Equal(new DateOnly(ano, mes, dia), Feriados.Pascoa(ano));

    [Fact]
    public void Nacionais_2026_FixosEMoveisEmOrdem()
    {
        var feriados = Feriados.Nacionais(2026);

        Assert.Equal(13, feriados.Count); // 9 fixos (com a Consciência Negra) + 2 de Carnaval + Sexta-feira Santa + Corpus Christi
        Assert.Equal(feriados.OrderBy(f => f.Data).Select(f => f.Data), feriados.Select(f => f.Data));
        Assert.Equal("Carnaval", Feriados.Do(new DateOnly(2026, 2, 16))!.Nome);
        Assert.True(Feriados.Do(new DateOnly(2026, 2, 17))!.PontoFacultativo);
        Assert.Equal("Sexta-feira Santa", Feriados.Do(new DateOnly(2026, 4, 3))!.Nome);
        Assert.False(Feriados.Do(new DateOnly(2026, 4, 3))!.PontoFacultativo);
        Assert.Equal("Corpus Christi", Feriados.Do(new DateOnly(2026, 6, 4))!.Nome);
        Assert.Equal("Nossa Senhora Aparecida", Feriados.Do(new DateOnly(2026, 10, 12))!.Nome);
        Assert.Contains("Consciência Negra", Feriados.Do(new DateOnly(2026, 11, 20))!.Nome);
        Assert.Null(Feriados.Do(new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Nacionais_AntesDe2024_SemConscienciaNegra() =>
        Assert.Null(Feriados.Do(new DateOnly(2023, 11, 20)));

    [Fact]
    public void Nacionais_DoisNoMesmoDia_ViramUmComOsNomesJuntos()
    {
        // 2000: Páscoa em 23/04 → Sexta-feira Santa em 21/04, dia de Tiradentes.
        var feriado = Feriados.Do(new DateOnly(2000, 4, 21));

        Assert.Equal("Sexta-feira Santa / Tiradentes", feriado!.Nome);
        Assert.Single(Feriados.Nacionais(2000), f => f.Data == new DateOnly(2000, 4, 21));
    }
}
