using GestaoSorveteria.Domain.Comandas;
using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>Linha da tabela local de comandas. Espelha a <c>Comanda</c> do Domain.</summary>
[Table("comandas")]
public class Comanda
{
    private DateTime _criadaEm;
    private DateTime? _fechadaEm;
    private DateTime? _canceladaEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Indexed, Column("caixa_id")]
    public Guid CaixaId { get; set; }

    [Column("usuario_id")]
    public Guid UsuarioId { get; set; }

    [Column("numero")]
    public int Numero { get; set; }

    [Column("nome_cliente")]
    public string? NomeCliente { get; set; }

    [Column("tipo")]
    public TipoComanda Tipo { get; set; } = TipoComanda.Balcao;

    [Indexed, Column("status")]
    public StatusComanda Status { get; set; } = StatusComanda.Aberta;

    [Column("observacao")]
    public string? Observacao { get; set; }

    [Column("total_centavos")]
    public long TotalCentavos { get; set; }

    [Ignore]
    public decimal Total
    {
        get => ParaReais(TotalCentavos);
        set => TotalCentavos = ParaCentavos(value);
    }

    [Column("criada_em")]
    public DateTime CriadaEm { get => _criadaEm; set => _criadaEm = ComoUtc(value); }

    [Column("fechada_em")]
    public DateTime? FechadaEm { get => _fechadaEm; set => _fechadaEm = ComoUtc(value); }

    [Column("cancelada_em")]
    public DateTime? CanceladaEm { get => _canceladaEm; set => _canceladaEm = ComoUtc(value); }

    [Column("motivo_cancelamento")]
    public string? MotivoCancelamento { get; set; }

    /// <summary>Fechada ou cancelada e ainda não confirmada pela API (docs/03, seção 8).</summary>
    [Indexed, Column("pendente_envio")]
    public bool PendenteEnvio { get; set; }

    /// <summary>C4: quantas vezes o envio falhou por rede/servidor (não zera o pendente).</summary>
    [Column("tentativas_envio")]
    public int TentativasEnvio { get; set; }

    [Column("ultimo_erro_envio")]
    public string? UltimoErroEnvio { get; set; }

    /// <summary>C4: motivo devolvido pela API quando <c>rejeitada</c>; fica visível, nunca some em silêncio (RN-SY-03).</summary>
    [Column("rejeitada_motivo")]
    public string? RejeitadaMotivo { get; set; }

    [Column("enviada_em")]
    public DateTime? EnviadaEm { get => _enviadaEm; set => _enviadaEm = ComoUtc(value); }
    private DateTime? _enviadaEm;

    /// <summary>Para o histórico do turno (C5).</summary>
    [Ignore]
    public string SituacaoEnvio => RejeitadaMotivo is not null ? $"Rejeitada: {RejeitadaMotivo}"
        : PendenteEnvio ? "Aguardando envio" : Status == GestaoSorveteria.Domain.Comandas.StatusComanda.Aberta ? string.Empty : "Enviada";

    /// <summary>Para a tela: RN-CM-11, delivery na F1 é só marcação + observação.</summary>
    [Ignore]
    public bool EhDelivery => Tipo == TipoComanda.Delivery;

    [Ignore]
    public bool TemObservacao => !string.IsNullOrWhiteSpace(Observacao);

    /// <summary>Para a tela: hora do celular (RN-TD-01).</summary>
    [Ignore]
    public DateTime CriadaEmLocal => CriadaEm.ToLocalTime();
}
