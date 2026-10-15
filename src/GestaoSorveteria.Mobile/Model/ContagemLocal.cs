using System.Globalization;
using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>
/// Contagem semanal do freezer feita no tablet (Fase 2, RN-ES-05/06, docs/10 §2.1). Nasce no primeiro item contado, com o
/// Id gerado aqui; pode ficar "em andamento" (salva parcial) e continuar depois; <b>concluída</b> não muda mais.
/// Uma em andamento por vez no tablet. Sobe inteira (como está) para <c>POST /api/sync/contagens</c>, idempotente pelo Id.
/// Nada é apagado: a contagem deixada de lado (recusada ou esquecida de outra semana) só ganha <see cref="DescartadaEm"/>.
/// </summary>
[Table("contagens")]
public class ContagemLocal
{
    private DateTime _iniciadaEm;
    private DateTime _alteradaEm;
    private DateTime? _concluidaEm;
    private DateTime? _descartadaEm;
    private DateTime? _enviadoEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Quem começou (vai no envio; quem continua depois não muda o autor, como na API).</summary>
    [Column("usuario_id")]
    public Guid UsuarioId { get; set; }

    [Column("usuario_nome")]
    public string UsuarioNome { get; set; } = string.Empty;

    [Column("iniciada_em")]
    public DateTime IniciadaEm { get => _iniciadaEm; set => _iniciadaEm = ComoUtc(value); }

    /// <summary>Último item contado ou apagado (hora do tablet).</summary>
    [Column("alterada_em")]
    public DateTime AlteradaEm { get => _alteradaEm; set => _alteradaEm = ComoUtc(value); }

    /// <summary>Preenchida = concluída: só ela vale (RN-ES-05) e não muda mais.</summary>
    [Indexed, Column("concluida_em")]
    public DateTime? ConcluidaEm { get => _concluidaEm; set => _concluidaEm = ComoUtc(value); }

    /// <summary>
    /// Deixada de lado no tablet (recusada pelo servidor ou começada em outro dia e trocada por uma nova). Não aparece mais,
    /// não sobe e não serve de referência. No servidor, o que já tinha subido fica como "não concluída" (não vale).
    /// </summary>
    [Column("descartada_em")]
    public DateTime? DescartadaEm { get => _descartadaEm; set => _descartadaEm = ComoUtc(value); }

    // ---------------------------------------------------------------- fila de envio (como os avisos)

    /// <summary>Mudou no tablet e ainda não foi confirmada pela API.</summary>
    [Indexed, Column("pendente_envio")]
    public bool PendenteEnvio { get; set; }

    /// <summary>Sobe a cada item contado e na conclusão: o resultado do envio só tira da fila se nada mudou durante o envio.</summary>
    [Column("versao")]
    public int Versao { get; set; }

    [Column("ultimo_erro_envio")]
    public string? UltimoErroEnvio { get; set; }

    /// <summary>Motivo devolvido pela API quando <c>rejeitada</c>: aparece em laranja na tela, nunca some em silêncio.</summary>
    [Column("rejeitado_motivo")]
    public string? RejeitadoMotivo { get; set; }

    [Column("enviado_em")]
    public DateTime? EnviadoEm { get => _enviadoEm; set => _enviadoEm = ComoUtc(value); }

    // ---------------------------------------------------------------- para a tela

    [Ignore]
    public bool EstaConcluida => ConcluidaEm is not null;

    [Ignore]
    public bool EstaEmAndamento => ConcluidaEm is null && DescartadaEm is null;

    [Ignore]
    public bool EhRecusada => RejeitadoMotivo is not null;

    /// <summary>"⏳ Aguardando envio para o painel", "✅ Enviada ao painel" ou o motivo da recusa.</summary>
    [Ignore]
    public string SituacaoEnvio => RejeitadoMotivo is { } motivo ? $"⚠ Recusada pelo sistema: {motivo}"
        : PendenteEnvio ? "⏳ Aguardando envio para o painel"
        : EnviadoEm is not null ? "✅ Enviada ao painel"
        : string.Empty;
}

/// <summary>
/// Um item na contagem do tablet: o que foi contado em <b>embalagens</b> ("2,5" caixas, "3" baldes), como a pessoa digitou.
/// <see cref="Embalagens"/> nulo = apagado depois de contado: vai como "não contado" no envio (nunca vira 0, RN-ES-06).
/// Item nunca tocado não tem linha (e não vai no envio).
/// </summary>
[Table("itens_contagem")]
public class ItemContagemLocal
{
    private DateTime _alteradoEm;
    private DateTime? _contadoEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, Column("contagem_id")]
    public Guid ContagemId { get; set; }

    [Indexed, Column("item_estoque_id")]
    public Guid ItemEstoqueId { get; set; }

    /// <summary>
    /// Texto invariável ("2.5"): o sqlite-net gravaria <c>decimal</c> como REAL; aqui o valor volta exatamente como foi contado.
    /// </summary>
    [Column("contado_embalagens")]
    public string? ContadoEmbalagens { get; set; }

    /// <summary>Último toque na linha (mesmo sem mudar o número).</summary>
    [Column("alterado_em")]
    public DateTime AlteradoEm { get => _alteradoEm; set => _alteradoEm = ComoUtc(value); }

    /// <summary>
    /// RN-ES-06/08: quando o item foi olhado no freezer (UTC) — muda só quando o número muda (tocar no mesmo valor não
    /// "reconta"); nulo quando apagado. É a base do saldo dos potes no servidor (<c>LinhaContagemSyncDto.ContadoEm</c>).
    /// </summary>
    [Column("contado_em")]
    public DateTime? ContadoEm { get => _contadoEm; set => _contadoEm = ComoUtc(value); }

    /// <summary>
    /// O que vai no envio e na conclusão: <see cref="ContadoEm"/>; linha gravada antes da coluna existir usa o último toque.
    /// Sem número, não tem hora.
    /// </summary>
    [Ignore]
    public DateTime? QuandoContado => Embalagens is null ? null : ContadoEm ?? AlteradoEm;

    [Ignore]
    public decimal? Embalagens
    {
        get => ContadoEmbalagens is { Length: > 0 } texto
            && decimal.TryParse(texto, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor)
                ? valor
                : null;
        set => ContadoEmbalagens = value?.ToString(CultureInfo.InvariantCulture);
    }
}
