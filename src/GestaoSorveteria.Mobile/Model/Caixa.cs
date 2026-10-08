using GestaoSorveteria.Domain.Caixas;
using SQLite;
using static SorveteriaMaui.Model.Conversoes;

namespace SorveteriaMaui.Model;

/// <summary>Linha da tabela local de caixas (C3). Espelha o <c>Caixa</c> do Domain; dinheiro em centavos, datas UTC.</summary>
[Table("caixas")]
public class Caixa
{
    private DateTime _abertoEm;
    private DateTime? _fechadoEm;

    [PrimaryKey, Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("aberto_por_usuario_id")]
    public Guid AbertoPorUsuarioId { get; set; }

    [Column("aberto_em")]
    public DateTime AbertoEm { get => _abertoEm; set => _abertoEm = ComoUtc(value); }

    [Column("fundo_troco_centavos")]
    public long FundoTrocoCentavos { get; set; }

    [Indexed, Column("status")]
    public StatusCaixa Status { get; set; } = StatusCaixa.Aberto;

    [Column("fechado_por_usuario_id")]
    public Guid? FechadoPorUsuarioId { get; set; }

    [Column("fechado_em")]
    public DateTime? FechadoEm { get => _fechadoEm; set => _fechadoEm = ComoUtc(value); }

    /// <summary>RN-CX-06: os três valores da conferência ficam gravados (mais as vendas em dinheiro usadas).</summary>
    [Column("total_vendas_dinheiro_centavos")]
    public long? TotalVendasDinheiroCentavos { get; set; }

    [Column("valor_esperado_centavos")]
    public long? ValorEsperadoCentavos { get; set; }

    [Column("valor_contado_centavos")]
    public long? ValorContadoCentavos { get; set; }

    [Column("diferenca_centavos")]
    public long? DiferencaCentavos { get; set; }

    [Column("observacao")]
    public string? Observacao { get; set; }

    /// <summary>Mudou (abertura, movimento ou fechamento) e ainda não foi confirmado pela API (C4, RN-SY-03).</summary>
    [Indexed, Column("pendente_envio")]
    public bool PendenteEnvio { get; set; }

    /// <summary>C4: quantas vezes o envio falhou por rede/servidor (não zera o pendente).</summary>
    [Column("tentativas_envio")]
    public int TentativasEnvio { get; set; }

    [Column("ultimo_erro_envio")]
    public string? UltimoErroEnvio { get; set; }

    /// <summary>C4: motivo devolvido pela API quando <c>rejeitada</c>; fica visível, nunca some em silêncio (RN-SY-03).</summary>
    [Column("rejeitado_motivo")]
    public string? RejeitadoMotivo { get; set; }

    [Column("enviado_em")]
    public DateTime? EnviadoEm { get => _enviadoEm; set => _enviadoEm = ComoUtc(value); }
    private DateTime? _enviadoEm;

    [Ignore]
    public decimal FundoTroco { get => ParaReais(FundoTrocoCentavos); set => FundoTrocoCentavos = ParaCentavos(value); }

    [Ignore]
    public decimal? TotalVendasDinheiro
    {
        get => TotalVendasDinheiroCentavos is { } c ? ParaReais(c) : null;
        set => TotalVendasDinheiroCentavos = value is { } v ? ParaCentavos(v) : null;
    }

    [Ignore]
    public decimal? ValorEsperado
    {
        get => ValorEsperadoCentavos is { } c ? ParaReais(c) : null;
        set => ValorEsperadoCentavos = value is { } v ? ParaCentavos(v) : null;
    }

    [Ignore]
    public decimal? ValorContado
    {
        get => ValorContadoCentavos is { } c ? ParaReais(c) : null;
        set => ValorContadoCentavos = value is { } v ? ParaCentavos(v) : null;
    }

    /// <summary>contado − esperado; negativo = faltou dinheiro na gaveta.</summary>
    [Ignore]
    public decimal? Diferenca
    {
        get => DiferencaCentavos is { } c ? ParaReais(c) : null;
        set => DiferencaCentavos = value is { } v ? ParaCentavos(v) : null;
    }

    [Ignore]
    public bool EstaAberto => Status == StatusCaixa.Aberto;
}
