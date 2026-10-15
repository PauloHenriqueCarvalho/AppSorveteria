using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Clima;

/// <summary>Local da loja para o clima (RN-CL-01): como o serviço de clima encontrou, coordenadas com 4 casas.</summary>
public sealed record LocalLojaDto(string Nome, decimal Latitude, decimal Longitude);

/// <summary>PUT /api/clima/local (Admin): grava o local confirmado pela dona e já coleta (RN-CL-01/04).</summary>
public sealed record DefinirLocalLojaRequest(
    [Required, StringLength(120, MinimumLength = 2)] string Nome,
    [Range(-90, 90)] decimal Latitude,
    [Range(-180, 180)] decimal Longitude);

/// <summary>
/// GET /api/clima/situacao (Admin): a coleta do clima está ligada? <paramref name="ChaveConfigurada"/>: há chave do serviço
/// (Clima__ChaveApi). <paramref name="UltimaColetaEm"/>: última coleta que deu certo; <paramref name="UltimoErro"/>: da última
/// tentativa (nulo se deu certo). Datas da coleta só desde que a API ligou pela última vez. Dias: o que já está guardado no
/// ponto do local atual. <paramref name="Atribuicao"/>/<paramref name="AtribuicaoLink"/>: o crédito que o painel mostra (RN-CL-07).
/// </summary>
public sealed record SituacaoClimaDto(
    bool ChaveConfigurada,
    LocalLojaDto? Local,
    DateTime? UltimaColetaEm,
    DateTime? UltimaTentativaEm,
    string? UltimoErro,
    DateOnly? PrimeiroDia,
    DateOnly? UltimoDia,
    int DiasComClima,
    string Fonte,
    string Atribuicao,
    string AtribuicaoLink);

/// <summary>POST /api/clima/atualizar (Admin): o que a coleta fez (RN-CL-04). <paramref name="Coletou"/> = falou com o serviço de clima.</summary>
public sealed record ColetaClimaDto(bool Coletou, int DiasGravados, int PrevisoesGravadas, string Mensagem);

/// <summary>
/// Clima de um dia (RN-CL-02). °C com 1 casa, chuva em mm, percentuais 0–100. <paramref name="Origem"/>: Observado, Combinado
/// ou Previsto. <paramref name="Definitivo"/>: não muda mais.
/// </summary>
public sealed record ClimaDiaDto(
    DateOnly Data,
    decimal TempMax,
    decimal TempMin,
    decimal TempMedia,
    decimal? SensacaoMax,
    decimal? ChuvaMm,
    decimal? ChuvaHorasPct,
    decimal? NuvensPct,
    decimal? UmidadePct,
    string? Icone,
    string? Condicao,
    string Origem,
    bool Definitivo);

/// <summary>Previsão guardada de um dia (RN-CL-03): feita em <paramref name="FeitaNoDia"/>.</summary>
public sealed record PrevisaoDiaDto(
    DateOnly Data,
    DateOnly FeitaNoDia,
    decimal TempMax,
    decimal TempMin,
    decimal? ChuvaMm,
    decimal? ChanceChuvaPct,
    string? Icone,
    string? Condicao);

/// <summary>
/// GET /api/relatorios/clima-vendas?de=&amp;ate= (Admin, RN-CL-06): cada dia com clima, feriado e vendas; médias por faixa
/// da máxima e com/sem chuva; previsão dos próximos 7 dias. <paramref name="Local"/> nulo = a dona ainda não informou o local
/// (os dias vêm sem clima). <paramref name="DiasSemClima"/>: dias do período sem clima guardado.
/// </summary>
public sealed record ClimaVendasDto(
    DateOnly De,
    DateOnly Ate,
    LocalLojaDto? Local,
    IReadOnlyList<DiaClimaVendasDto> Dias,
    IReadOnlyList<FaixaTemperaturaDto> PorTemperatura,
    IReadOnlyList<ChuvaVendasDto> PorChuva,
    IReadOnlyList<PrevisaoDiaDto> ProximosDias,
    int DiasSemClima,
    string Atribuicao,
    string AtribuicaoLink);

/// <summary>
/// Um dia no Clima × vendas. <paramref name="DiaDaSemana"/>: 0 = domingo … 6 = sábado. <paramref name="Feriado"/>: nome do
/// feriado nacional (RN-CL-05). <paramref name="FimDeSemanaOuFeriado"/>: sábado, domingo ou feriado (inclusive ponto
/// facultativo). Faturamento, vendas e ticket como no RN-RL-01 (comandas fechadas). <paramref name="AvisosUrgentes"/>: avisos
/// urgentes criados no dia (faltou produto). <paramref name="EntraNasMedias"/>: tem clima, teve venda e não é hoje.
/// </summary>
public sealed record DiaClimaVendasDto(
    DateOnly Data,
    int DiaDaSemana,
    string? Feriado,
    bool FeriadoFacultativo,
    bool FimDeSemanaOuFeriado,
    ClimaDiaDto? Clima,
    decimal Faturamento,
    int Vendas,
    decimal TicketMedio,
    IReadOnlyList<CategoriaNoDiaDto> PorCategoria,
    int AvisosUrgentes,
    bool EntraNasMedias);

/// <summary>Unidades e faturamento de uma categoria no dia (nome e categoria do cadastro atual, RN-RL-03).</summary>
public sealed record CategoriaNoDiaDto(string Categoria, int Quantidade, decimal Total);

/// <summary>
/// Faixa da máxima (RN-CL-06): <paramref name="De"/> inclusive, <paramref name="Ate"/> exclusive; nulo = sem limite.
/// Médias separadas por dias úteis e por fins de semana e feriados.
/// </summary>
public sealed record FaixaTemperaturaDto(string Faixa, decimal? De, decimal? Ate, MediaDiasDto DiasUteis, MediaDiasDto FimDeSemanaEFeriado);

/// <summary>Com chuva (≥ 1 mm) ou sem chuva (RN-CL-06).</summary>
public sealed record ChuvaVendasDto(string Situacao, bool ComChuva, MediaDiasDto DiasUteis, MediaDiasDto FimDeSemanaEFeriado);

/// <summary>Média de faturamento (2 casas) e de vendas (1 casa) por dia; nulas quando nenhum dia entrou.</summary>
public sealed record MediaDiasDto(int Dias, decimal? FaturamentoMedio, decimal? VendasMedias);
