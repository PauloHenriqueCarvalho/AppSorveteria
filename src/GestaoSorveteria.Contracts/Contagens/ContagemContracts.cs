using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Contagens;

// Contagem semanal do freezer (RN-ES-05/06, docs/10 §2.1). Quantidades contadas em embalagens ("2,5" caixas, meia caixa
// só para caixa); a API guarda na menor unidade (RN-ES-03) e devolve as duas. Origem: "App" ou "Painel".

/// <summary>Linha da lista de contagens.</summary>
public sealed record ContagemResumoDto(
    Guid Id,
    string ContadoPorNome,
    string Origem,
    DateTime IniciadaEm,
    DateTime? ConcluidaEm,
    int ItensContados,
    int ItensNaoContados);

/// <summary>
/// Contagem com uma linha por item que entra na contagem (ativo, grupo Contagem ou SaldoPelaVenda), na ordem da planilha.
/// Item ainda não contado vem com <c>ContadoUnidades</c> nulo. <c>AnteriorUnidades</c> é a última contagem concluída antes desta (só referência).
/// </summary>
public sealed record ContagemDto(
    Guid Id,
    string ContadoPorNome,
    string Origem,
    DateTime IniciadaEm,
    DateTime? ConcluidaEm,
    IReadOnlyList<LinhaContagemDto> Linhas);

public sealed record LinhaContagemDto(
    Guid ItemEstoqueId,
    Guid GrupoId,
    string GrupoNome,
    string ItemNome,
    string Unidade,
    int UnidadesPorEmbalagem,
    decimal PassoEmbalagens,
    int? ContadoUnidades,
    decimal? ContadoEmbalagens,
    int? AnteriorUnidades,
    decimal? AnteriorEmbalagens,
    int? SaldoSistemaUnidades);

/// <summary>
/// POST /api/estoque/contagens (painel): cria (<paramref name="Id"/> novo ou nulo) ou continua uma contagem aberta.
/// Cada linha: embalagens contadas; nulo = "não contado". <paramref name="Concluir"/> fecha a contagem (não muda mais).
/// </summary>
public sealed record SalvarContagemRequest(Guid? Id, IReadOnlyList<LinhaContagemRequest> Linhas, bool Concluir);

public sealed record LinhaContagemRequest(Guid ItemEstoqueId, decimal? Embalagens);

// ---------------------------------------------------------------- sincronização do tablet

/// <summary>POST /api/sync/contagens: contagens feitas no tablet (parciais ou concluídas), idempotente pelo Id.</summary>
public sealed record SyncContagensRequest(
    [Required(ErrorMessage = "Informe as contagens.")]
    [MinLength(1, ErrorMessage = "Envie pelo menos uma contagem.")]
    [MaxLength(20, ErrorMessage = "Envie no máximo 20 contagens por vez.")]
    IReadOnlyList<ContagemSyncDto> Contagens);

/// <summary>A contagem como está no tablet (todas as linhas registradas). Concluída no tablet = <paramref name="ConcluidaEm"/> preenchido.</summary>
public sealed record ContagemSyncDto(
    Guid Id,
    Guid UsuarioId,
    DateTime IniciadaEm,
    DateTime? ConcluidaEm,
    IReadOnlyList<LinhaContagemSyncDto>? Linhas);

public sealed record LinhaContagemSyncDto(Guid Id, Guid ItemEstoqueId, decimal? Embalagens);
