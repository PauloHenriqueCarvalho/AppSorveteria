using GestaoSorveteria.Application.Relatorios;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Clima;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Tests.Domain;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Clima × vendas (RN-CL-06): dias, feriados, médias por faixa da máxima e com/sem chuva, próximos dias.</summary>
public class ClimaVendasServiceTests
{
    // Quinta 15/10/2026, 12:00 em Brasília.
    private static readonly DateTime Agora = new(2026, 10, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoje = new(2026, 10, 15);
    private static readonly PontoClima Ponto = PontoClima.De(-21.18m, -47.81m);

    private readonly ComandasEmMemoria _comandas = new();
    private readonly ProdutoRepositoryFake _produtos = new();
    private readonly AvisoRepositoryFake _avisos = new();
    private readonly ClimaRepositoryFake _clima = new();
    private readonly ConfiguracaoLojaRepositoryFake _configuracao = new();
    private readonly FonteClimaFake _fonte = new() { Hoje = Hoje };
    private readonly ClockFake _clock = new() { UtcNow = Agora };
    private readonly Caixa _caixa = Caixa.Abrir(Cenario.Atendente, 100m, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
    private readonly ClimaVendasService _service;
    private int _numero;

    public ClimaVendasServiceTests()
    {
        var configuracao = ConfiguracaoLoja.Padrao(Agora);
        configuracao.DefinirLocal("Ribeirão Preto, SP, Brasil", -21.1775m, -47.8103m, Agora);
        _configuracao.Configuracao = configuracao;
        _service = new ClimaVendasService(_comandas, _produtos, _avisos, _clima, _configuracao, _fonte, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static DateOnly Dia(int dia) => new(2026, 10, dia);

    /// <summary>Hora de Brasília (UTC−3) num dia de outubro.</summary>
    private static DateTime Em(int dia, int hora = 15) => new DateTime(2026, 10, dia, 0, 0, 0, DateTimeKind.Utc).AddHours(hora + 3);

    private Comanda Venda(int dia, decimal valor, Action<Comanda>? itens = null)
    {
        var fechadaEm = Em(dia);
        var comanda = Comanda.Abrir(_caixa, Cenario.Atendente, ++_numero, TipoComanda.Balcao, fechadaEm.AddMinutes(-5), fechadaEm);
        if (itens is null)
        {
            comanda.AdicionarItemLivre("Venda avulsa", valor);
        }
        else
        {
            itens(comanda);
        }

        comanda.Fechar([new DadosPagamento(FormaPagamento.Pix, comanda.Total)], fechadaEm);
        _comandas.Comandas.Add(comanda);
        return comanda;
    }

    private void Clima(int dia, decimal maxima, decimal chuva = 0m) =>
        _clima.Dias.Add(ClimaDia.Registrar(
            Ponto,
            new LeituraClima(Dia(dia), maxima, maxima - 10m, maxima - 5m, null, chuva, null, null, null, null, "clear-day", "Céu limpo", OrigemClima.Observado),
            "Teste",
            Agora,
            Hoje));

    /// <summary>05 a 15/10: seg 18 °C, ter 22 °C com chuva, qua 31 °C, qui sem venda, sex sem clima, sáb 33 °C, dom 26 °C com
    /// garoa, seg 12/10 feriado 29 °C com chuva, 13 e 14 sem nada, hoje 30 °C.</summary>
    private void CenarioDe05a15()
    {
        Clima(5, 18m);
        Venda(5, 100m);
        Clima(6, 22m, chuva: 5m);
        Venda(6, 50m);
        Venda(6, 30m);
        Clima(7, 31m);
        Venda(7, 300m);
        Clima(8, 31m);              // sem venda: loja fechada
        Venda(9, 200m);             // sem clima
        Clima(10, 33m);
        Venda(10, 500m);
        Clima(11, 26m, chuva: 0.5m); // garoa: menos de 1 mm conta como sem chuva
        Venda(11, 250m);
        Clima(12, 29m, chuva: 2m);
        Venda(12, 400m);
        Clima(15, 30m);
        Venda(15, 80m);             // hoje: fora das médias
    }

    [Fact]
    public async Task Obter_CadaDiaComClimaFeriadoEVendas()
    {
        CenarioDe05a15();

        var relatorio = await _service.ObterAsync(Dia(5), Hoje, Ct);

        Assert.Equal(11, relatorio.Dias.Count);
        Assert.Equal(3, relatorio.DiasSemClima); // 09, 13 e 14
        Assert.Equal("Ribeirão Preto, SP, Brasil", relatorio.Local!.Nome);

        var terca = relatorio.Dias[1];
        Assert.Equal((Dia(6), 2, 80m, 2, 40m), (terca.Data, terca.DiaDaSemana, terca.Faturamento, terca.Vendas, terca.TicketMedio));
        Assert.Equal((22m, 5m), (terca.Clima!.TempMax, terca.Clima.ChuvaMm!.Value));
        Assert.False(terca.FimDeSemanaOuFeriado);
        Assert.True(terca.EntraNasMedias);

        var feriado = relatorio.Dias[7];
        Assert.Equal(("Nossa Senhora Aparecida", false, true), (feriado.Feriado, feriado.FeriadoFacultativo, feriado.FimDeSemanaOuFeriado));

        Assert.False(relatorio.Dias[3].EntraNasMedias);  // 08: sem venda
        Assert.False(relatorio.Dias[4].EntraNasMedias);  // 09: sem clima
        Assert.Null(relatorio.Dias[4].Clima);
        Assert.False(relatorio.Dias[10].EntraNasMedias); // hoje
        Assert.Equal("Dados do clima: Teste", relatorio.Atribuicao);
    }

    [Fact]
    public async Task Obter_MediasPorFaixaDaMaxima_SeparandoDiasUteisDeFimDeSemanaEFeriado()
    {
        CenarioDe05a15();

        var faixas = (await _service.ObterAsync(Dia(5), Hoje, Ct)).PorTemperatura;

        Assert.Equal(new[] { "Abaixo de 20 °C", "20 a 25 °C", "25 a 30 °C", "30 a 35 °C", "35 °C ou mais" }, faixas.Select(f => f.Faixa));
        Assert.Null(faixas[0].De);
        Assert.Equal(20m, faixas[0].Ate);
        Assert.Equal((1, 100m, 1.0m), (faixas[0].DiasUteis.Dias, faixas[0].DiasUteis.FaturamentoMedio!.Value, faixas[0].DiasUteis.VendasMedias!.Value));
        Assert.Equal((1, 80m, 2.0m), (faixas[1].DiasUteis.Dias, faixas[1].DiasUteis.FaturamentoMedio!.Value, faixas[1].DiasUteis.VendasMedias!.Value));
        Assert.Equal(0, faixas[2].DiasUteis.Dias);
        Assert.Null(faixas[2].DiasUteis.FaturamentoMedio);
        Assert.Equal((2, 325m), (faixas[2].FimDeSemanaEFeriado.Dias, faixas[2].FimDeSemanaEFeriado.FaturamentoMedio!.Value)); // dom 250 + feriado 400
        Assert.Equal((1, 300m), (faixas[3].DiasUteis.Dias, faixas[3].DiasUteis.FaturamentoMedio!.Value));                   // 08 (sem venda) fora
        Assert.Equal((1, 500m), (faixas[3].FimDeSemanaEFeriado.Dias, faixas[3].FimDeSemanaEFeriado.FaturamentoMedio!.Value));
        Assert.Equal((0, 0), (faixas[4].DiasUteis.Dias, faixas[4].FimDeSemanaEFeriado.Dias));
    }

    [Fact]
    public async Task Obter_LimiteDaFaixa_FicaNaDeCima()
    {
        Clima(7, 25m);
        Venda(7, 10m);

        var faixas = (await _service.ObterAsync(Dia(7), Dia(7), Ct)).PorTemperatura;

        Assert.Equal(1, faixas.Single(f => f.Faixa == "25 a 30 °C").DiasUteis.Dias);
    }

    [Fact]
    public async Task Obter_ComChuvaESemChuva_GaroaContaComoSemChuva()
    {
        CenarioDe05a15();

        var chuva = (await _service.ObterAsync(Dia(5), Hoje, Ct)).PorChuva;

        var sem = chuva.Single(c => !c.ComChuva);
        var com = chuva.Single(c => c.ComChuva);
        Assert.Equal(("Sem chuva", 2, 200m), (sem.Situacao, sem.DiasUteis.Dias, sem.DiasUteis.FaturamentoMedio!.Value));                // 05 e 07
        Assert.Equal((2, 375m), (sem.FimDeSemanaEFeriado.Dias, sem.FimDeSemanaEFeriado.FaturamentoMedio!.Value));                     // sáb e dom (garoa)
        Assert.Equal((1, 80m), (com.DiasUteis.Dias, com.DiasUteis.FaturamentoMedio!.Value));
        Assert.Equal((1, 400m), (com.FimDeSemanaEFeriado.Dias, com.FimDeSemanaEFeriado.FaturamentoMedio!.Value));
    }

    [Fact]
    public async Task Obter_CategoriasDoDiaEAvisosUrgentes()
    {
        var picole = Cenario.Picole(5m);
        _produtos.Produtos.Add(picole);
        Clima(7, 31m);
        Venda(7, 0m, c =>
        {
            c.AdicionarProduto(picole, 4);
            c.AdicionarItemLivre("Self-service", 28.5m);
        });
        _avisos.Avisos.Add(Aviso.DoItem(Guid.NewGuid(), 0, PrioridadeAviso.Urgente, null, Cenario.Atendente, Em(7, 14)));
        _avisos.Avisos.Add(Aviso.DoItem(Guid.NewGuid(), 3, PrioridadeAviso.Alta, null, Cenario.Atendente, Em(7, 16)));
        _avisos.Avisos.Add(Aviso.DoItem(Guid.NewGuid(), 0, PrioridadeAviso.Urgente, null, Cenario.Atendente, Em(8, 10)));
        // Automático dos potes (RN-AV-04, autor "Sistema"): não é o balcão avisando.
        _avisos.Avisos.Add(Aviso.DoItem(Guid.NewGuid(), 0, PrioridadeAviso.Urgente, null, null, Em(7, 18)));

        var dia = (await _service.ObterAsync(Dia(7), Dia(7), Ct)).Dias.Single();

        Assert.Equal(1, dia.AvisosUrgentes);
        Assert.Collection(dia.PorCategoria,
            c => Assert.Equal(("Self-service / venda avulsa", 1, 28.5m), (c.Categoria, c.Quantidade, c.Total)),
            c => Assert.Equal(("Picolés", 4, 20m), (c.Categoria, c.Quantidade, c.Total)));
        Assert.Equal(48.5m, dia.Faturamento);
    }

    [Fact]
    public async Task Obter_VendaEstornada_ForaDoFaturamentoDoDia()
    {
        Clima(7, 31m);
        Venda(7, 100m);
        Venda(7, 40m).Estornar("Valor errado", Cenario.Dona, Em(7, 18));

        var dia = (await _service.ObterAsync(Dia(7), Dia(7), Ct)).Dias.Single();

        Assert.Equal((100m, 1), (dia.Faturamento, dia.Vendas));
    }

    [Fact]
    public async Task Obter_ProximosDias_APrevisaoMaisRecente()
    {
        var leitura = (int dia, decimal maxima) =>
            new LeituraClima(Dia(dia), maxima, maxima - 10m, maxima - 5m, null, 0m, null, null, null, 20m, "rain", "Chuva", OrigemClima.Previsto);
        for (var d = 15; d <= 28; d++)
        {
            _clima.Previsoes.Add(PrevisaoClima.Registrar(Ponto, Dia(14), Agora.AddDays(-1), leitura(d, 20m), "Teste"));
        }

        for (var d = 16; d <= 29; d++)
        {
            _clima.Previsoes.Add(PrevisaoClima.Registrar(Ponto, Hoje, Agora, leitura(d, 32m), "Teste"));
        }

        var proximos = (await _service.ObterAsync(null, null, Ct)).ProximosDias;

        Assert.Equal(Enumerable.Range(16, 7).Select(Dia), proximos.Select(p => p.Data));
        Assert.All(proximos, p => Assert.Equal((Hoje, 32m), (p.FeitaNoDia, p.TempMax)));
    }

    [Fact]
    public async Task Obter_SemDatas_UltimosTrintaDias()
    {
        var relatorio = await _service.ObterAsync(null, null, Ct);

        Assert.Equal((new DateOnly(2026, 9, 16), Hoje, 30), (relatorio.De, relatorio.Ate, relatorio.Dias.Count));
    }

    [Fact]
    public async Task Obter_SemLocal_DiasSemClimaEMediasVazias()
    {
        _configuracao.Configuracao = null;
        CenarioDe05a15();

        var relatorio = await _service.ObterAsync(Dia(5), Hoje, Ct);

        Assert.Null(relatorio.Local);
        Assert.Equal(11, relatorio.DiasSemClima);
        Assert.All(relatorio.Dias, d => Assert.Null(d.Clima));
        Assert.All(relatorio.PorTemperatura, f => Assert.Equal(0, f.DiasUteis.Dias + f.FimDeSemanaEFeriado.Dias));
        Assert.Empty(relatorio.ProximosDias);
        Assert.Equal(1910m, relatorio.Dias.Sum(d => d.Faturamento)); // as vendas continuam aparecendo
    }

    [Fact]
    public async Task Obter_DataFinalDepoisDeHoje_Lanca() =>
        await Assert.ThrowsAsync<DomainException>(() => _service.ObterAsync(Dia(10), Dia(16), Ct));

    [Fact]
    public async Task Obter_MaisDeUmAno_Lanca() =>
        await Assert.ThrowsAsync<DomainException>(() => _service.ObterAsync(new DateOnly(2025, 10, 1), Hoje, Ct));
}
