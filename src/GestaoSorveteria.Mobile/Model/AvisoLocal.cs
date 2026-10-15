using GestaoSorveteria.Domain.Estoque;
using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>
/// "Está acabando" no aparelho (Fase 2, RN-AV-01): os avisos criados no tablet (offline, sobem pela fila) e os abertos
/// que vieram do servidor (painel ou outro aparelho). O envio manda o <b>estado atual</b> de cada aviso, idempotente pelo
/// Id (RN-AV-09, docs/03 §5). Datas em UTC; nada é apagado do servidor por aqui.
/// </summary>
[Table("avisos")]
public class AvisoLocal
{
    private DateTime _criadoEm;
    private DateTime _atualizadoEm;
    private DateTime? _whatsAppAbertoEm;
    private DateTime? _enviadoEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Item do catálogo; nulo = "Outro" (texto livre, que não deduplica — RN-AV-03).</summary>
    [Indexed, Column("item_estoque_id")]
    public Guid? ItemEstoqueId { get; set; }

    [Column("texto_livre")]
    public string? TextoLivre { get; set; }

    /// <summary>Pronta para a tela e para o WhatsApp: "Picolé ao leite Coco preto" ou o texto livre.</summary>
    [Column("descricao")]
    public string Descricao { get; set; } = string.Empty;

    [Column("grupo_nome")]
    public string? GrupoNome { get; set; }

    /// <summary>Quanto tem agora, na menor unidade (0 = acabou).</summary>
    [Column("quantidade_atual")]
    public int QuantidadeAtual { get; set; }

    [Column("prioridade")]
    public PrioridadeAviso Prioridade { get; set; } = PrioridadeAviso.Media;

    [Column("observacao")]
    public string? Observacao { get; set; }

    /// <summary>
    /// Quem avisou. Nulo nos avisos baixados do servidor (a API manda só o nome); no envio vale o atendente do aparelho,
    /// que o servidor só confere se existe — o autor do aviso que já está lá não muda.
    /// </summary>
    [Column("criado_por_usuario_id")]
    public Guid? CriadoPorUsuarioId { get; set; }

    [Column("criado_por_nome")]
    public string CriadoPorNome { get; set; } = string.Empty;

    [Column("criado_em")]
    public DateTime CriadoEm { get => _criadoEm; set => _criadoEm = ComoUtc(value); }

    /// <summary>Último "Ainda falta" (nulo enquanto ninguém atualizou).</summary>
    [Column("atualizado_por_usuario_id")]
    public Guid? AtualizadoPorUsuarioId { get; set; }

    [Column("atualizado_por_nome")]
    public string? AtualizadoPorNome { get; set; }

    [Column("atualizado_em")]
    public DateTime AtualizadoEm { get => _atualizadoEm; set => _atualizadoEm = ComoUtc(value); }

    /// <summary>Aberto aparece em "Já avisados"; Fechado saiu da lista (a dona fechou, chegou na entrega ou o recusado foi dispensado).</summary>
    [Indexed, Column("status")]
    public StatusAviso Status { get; set; } = StatusAviso.Aberto;

    /// <summary>RN-AV-08: quando o link do WhatsApp foi aberto — nunca "entregue".</summary>
    [Column("whatsapp_aberto_em")]
    public DateTime? WhatsAppAbertoEm { get => _whatsAppAbertoEm; set => _whatsAppAbertoEm = ComoUtc(value); }

    /// <summary>Criado ou alterado no aparelho e ainda não confirmado pela API.</summary>
    [Indexed, Column("pendente_envio")]
    public bool PendenteEnvio { get; set; }

    /// <summary>
    /// Sobe a cada mudança feita no aparelho. O resultado do envio só tira da fila se o aviso não mudou enquanto o lote
    /// viajava ("Ainda falta" ou WhatsApp aberto durante o envio continuam pendentes e sobem no próximo).
    /// </summary>
    [Column("versao")]
    public int Versao { get; set; }

    [Column("ultimo_erro_envio")]
    public string? UltimoErroEnvio { get; set; }

    /// <summary>Motivo devolvido pela API quando <c>rejeitada</c>: o aviso fica em laranja na lista, nunca some em silêncio (RN-AV-09).</summary>
    [Column("rejeitado_motivo")]
    public string? RejeitadoMotivo { get; set; }

    [Column("enviado_em")]
    public DateTime? EnviadoEm { get => _enviadoEm; set => _enviadoEm = ComoUtc(value); }

    // ---------------------------------------------------------------- para a tela

    [Ignore]
    public bool EhRecusado => RejeitadoMotivo is not null;

    [Ignore]
    public bool EhUrgente => Prioridade == PrioridadeAviso.Urgente;

    [Ignore]
    public bool EhAlta => Prioridade == PrioridadeAviso.Alta;

    /// <summary>RN-AV-02: Urgente, Alta ou Média.</summary>
    [Ignore]
    public string PrioridadeNome => NomeDaPrioridade(Prioridade);

    [Ignore]
    public string PrioridadeTexto => Prioridade switch
    {
        PrioridadeAviso.Urgente => "🔴 Urgente",
        PrioridadeAviso.Alta => "🟠 Alta",
        _ => "🟡 Média",
    };

    [Ignore]
    public string QuantidadeTexto => QuantidadeAtual == 0 ? "Acabou" : $"Restam {QuantidadeAtual}";

    [Ignore]
    public string QuemAvisou => string.IsNullOrWhiteSpace(AtualizadoPorNome) ? CriadoPorNome : AtualizadoPorNome;

    /// <summary>"às 14:10" (hoje) ou "em 13/10 às 14:10", no fuso do tablet (RN-TD-01).</summary>
    [Ignore]
    public string QuandoTexto
    {
        get
        {
            var local = AtualizadoEm.ToLocalTime();
            return local.Date == DateTime.Today ? $"às {local:HH:mm}" : $"em {local:dd/MM} às {local:HH:mm}";
        }
    }

    /// <summary>"Acabou · Ana às 14:10".</summary>
    [Ignore]
    public string Detalhe => $"{QuantidadeTexto} · {QuemAvisou} {QuandoTexto}";

    /// <summary>RN-AV-03: "Já avisado às 14:10 por Ana — Acabou · Urgente".</summary>
    [Ignore]
    public string JaAvisadoTexto => $"Já avisado {QuandoTexto} por {QuemAvisou} — {QuantidadeTexto} · {PrioridadeNome}";

    [Ignore]
    public bool TemObservacao => !string.IsNullOrWhiteSpace(Observacao);

    [Ignore]
    public string SituacaoEnvio => RejeitadoMotivo is { } motivo ? $"⚠ Recusado: {motivo}"
        : PendenteEnvio ? "⏳ Aguardando envio para o painel"
        : WhatsAppAbertoEm is { } aberto ? $"WhatsApp aberto às {aberto.ToLocalTime():HH:mm}"
        : string.Empty;

    [Ignore]
    public bool TemSituacaoEnvio => SituacaoEnvio.Length > 0;

    public static string NomeDaPrioridade(PrioridadeAviso prioridade) => prioridade switch
    {
        PrioridadeAviso.Urgente => "Urgente",
        PrioridadeAviso.Alta => "Alta",
        _ => "Média",
    };
}
