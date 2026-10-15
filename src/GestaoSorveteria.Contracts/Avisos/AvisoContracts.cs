using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Avisos;

// Avisos "Está acabando" (docs/10 §2.2, docs/02 §6.3). Prioridade como texto: "Urgente", "Alta" ou "Media".
// Status: "Aberto" ou "Fechado". Datas em UTC.

/// <summary>
/// Um aviso. <paramref name="Descricao"/> já vem pronta ("Picolé ao leite Coco preto" ou o texto livre).
/// <paramref name="CriadoPorNome"/> é "Sistema" quando o aviso nasceu sozinho (pote no mínimo, RN-AV-04).
/// </summary>
public sealed record AvisoDto(
    Guid Id,
    Guid? ItemEstoqueId,
    string Descricao,
    string? GrupoNome,
    string? FornecedorNome,
    int QuantidadeAtual,
    string Prioridade,
    string? Observacao,
    string CriadoPorNome,
    DateTime CriadoEm,
    string? AtualizadoPorNome,
    DateTime AtualizadoEm,
    string Status,
    DateTime? FechadoEm,
    string? FechadoPorNome,
    string? MotivoFechamento,
    DateTime? WhatsAppAbertoEm);

/// <summary>
/// POST /api/avisos. Item do catálogo (<paramref name="ItemEstoqueId"/>) ou texto livre ("Outro").
/// <paramref name="Id"/> opcional, gerado no aparelho: reenviar o mesmo Id não cria outro aviso.
/// </summary>
public sealed record CriarAvisoRequest(
    Guid? Id,
    Guid? ItemEstoqueId,
    string? TextoLivre,
    int QuantidadeAtual,
    string Prioridade,
    string? Observacao);

/// <summary>POST /api/avisos/{id}/ainda-falta (RN-AV-03): a prioridade só pode subir.</summary>
public sealed record AindaFaltaRequest(int QuantidadeAtual, string Prioridade, string? Observacao);

/// <summary>POST /api/avisos/{id}/fechar (RN-AV-05b, só Admin).</summary>
public sealed record FecharAvisoRequest(string Motivo);

/// <summary>
/// Resposta de criar e de "ainda falta". <paramref name="JaExistia"/>: o item já tinha aviso aberto e ele foi atualizado
/// (RN-AV-03). <paramref name="EnviarAgora"/>: é urgente novo (ou acabou de virar urgente) — quem chamou abre
/// <paramref name="LinkWhatsApp"/>, que já leva <paramref name="MensagemWhatsApp"/> para o número da dona (RN-AV-06).
/// </summary>
public sealed record AvisoSalvoResponse(
    AvisoDto Aviso,
    bool JaExistia,
    bool EnviarAgora,
    string? MensagemWhatsApp,
    string? LinkWhatsApp);

/// <summary>GET /api/avisos/resumo-do-dia (RN-AV-07): texto pronto e o link para o WhatsApp da dona.</summary>
public sealed record ResumoDoDiaResponse(
    DateOnly Data,
    string Texto,
    string LinkWhatsApp,
    int Urgentes,
    int Altas,
    int Medias);

/// <summary>GET/PUT /api/configuracoes: nome nas mensagens e WhatsApp da dona (só dígitos na resposta).</summary>
public sealed record ConfiguracaoLojaDto(string NomeLoja, string? WhatsAppDona);

public sealed record SalvarConfiguracaoLojaRequest(string NomeLoja, string? WhatsAppDona);

// ---------------------------------------------------------------- sincronização do tablet

/// <summary>POST /api/sync/avisos: avisos criados ou atualizados ("ainda falta") no tablet, idempotente pelo Id.</summary>
public sealed record SyncAvisosRequest(
    [Required(ErrorMessage = "Informe os avisos.")]
    [MinLength(1, ErrorMessage = "Envie pelo menos um aviso.")]
    [MaxLength(100, ErrorMessage = "Envie no máximo 100 avisos por vez.")]
    IReadOnlyList<AvisoSyncDto> Avisos);

/// <summary>
/// O aviso como está no tablet agora (estado atual, não o histórico de toques). Campos de texto anuláveis:
/// erro em um aviso vira "rejeitada" só nele, sem derrubar o lote (mesma regra de /api/sync/comandas).
/// </summary>
public sealed record AvisoSyncDto(
    Guid Id,
    Guid? ItemEstoqueId,
    string? TextoLivre,
    int QuantidadeAtual,
    string? Prioridade,
    string? Observacao,
    Guid CriadoPorUsuarioId,
    DateTime CriadoEm,
    Guid? AtualizadoPorUsuarioId,
    DateTime AtualizadoEm,
    DateTime? WhatsAppAbertoEm);

public sealed record SyncAvisosResponse(IReadOnlyList<ResultadoSyncAvisoDto> Resultados);

/// <summary>
/// <paramref name="Status"/>: "aceita", "ja_recebida" ou "rejeitada" (com <paramref name="Motivo"/>).
/// <paramref name="AvisoId"/> é o aviso que ficou valendo no servidor: quando o item já tinha aviso aberto (de outro aparelho
/// ou do painel), o do tablet foi juntado a ele e o app passa a usar este Id. <paramref name="Aberto"/> = false quando a dona
/// já fechou o aviso: o app tira da lista.
/// </summary>
public sealed record ResultadoSyncAvisoDto(Guid Id, string Status, string? Motivo, Guid? AvisoId, bool? Aberto);
