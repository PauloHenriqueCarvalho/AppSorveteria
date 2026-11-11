using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Pix;

/// <summary>RN-PX-03: situação da cobrança. Concluída é final; Removida e Expirada também (não voltam a Ativa).</summary>
public enum StatusCobrancaPix
{
    Ativa,
    Concluida,
    Removida,
    Expirada,
}

/// <summary>O que o banco diz da cobrança agora (RN-PX-03). <see cref="ValorPago"/> e <see cref="PagaEm"/> só quando concluída.</summary>
public sealed record SituacaoCobrancaPix(StatusCobrancaPix Status, string? EndToEndId = null, decimal? ValorPago = null, DateTime? PagaEm = null);

/// <summary>
/// RN-PX-01…06: cobrança Pix com valor (QR Code dinâmico) criada pela API no banco da loja para uma venda do balcão.
/// O <see cref="Id"/> vem do app (uma tentativa de cobrança = um Id) e o <see cref="Txid"/> sai dele, então pedir de novo a
/// mesma cobrança nunca cria outra no banco (idempotente, como a sincronização). A venda só é gravada no app depois de
/// <see cref="StatusCobrancaPix.Concluida"/>; o pagamento Pix da venda leva o txid (RN-PX-05) para a conferência.
/// Nada é apagado: cobrança desistida fica <see cref="StatusCobrancaPix.Removida"/>.
/// </summary>
public sealed class CobrancaPix : Entity
{
    /// <summary>RN-PX-02: 10 minutos para o cliente pagar; depois disso o banco recusa o pagamento.</summary>
    public const int ExpiracaoPadraoSegundos = 600;

    public const int ExpiracaoMinimaSegundos = 60;
    public const int ExpiracaoMaximaSegundos = 3600;

    /// <summary>Folga depois do vencimento antes de dar a cobrança como expirada (relógio do banco × do servidor).</summary>
    public static readonly TimeSpan FolgaExpiracao = TimeSpan.FromMinutes(1);

    public const int TamanhoMaximoTxid = 35;
    public const int TamanhoMaximoCopiaECola = 512;
    public const int TamanhoMaximoEndToEndId = 35;

    public string Txid { get; private set; } = string.Empty;
    public Guid UsuarioId { get; private set; }
    public decimal Valor { get; private set; }
    public StatusCobrancaPix Status { get; private set; }
    public string PixCopiaECola { get; private set; } = string.Empty;
    public DateTime CriadaEm { get; private set; }
    public DateTime ExpiraEm { get; private set; }
    public DateTime AtualizadaEm { get; private set; }
    public DateTime? PagaEm { get; private set; }
    public string? EndToEndId { get; private set; }
    public decimal? ValorPago { get; private set; }

    /// <summary>Pago e com o valor exato da cobrança (RN-PX-04). Pago com valor diferente não libera a venda.</summary>
    public bool PagaComValorCerto => Status == StatusCobrancaPix.Concluida && ValorPago == Valor;

    // EF Core
    private CobrancaPix()
    {
    }

    private CobrancaPix(Guid id) : base(id)
    {
    }

    /// <summary>
    /// RN-PX-02: txid = o Id da tentativa sem hífens (32 caracteres de 0-9 e a-f), dentro do padrão do Banco Central
    /// (26 a 35 letras e números).
    /// </summary>
    public static string TxidDe(Guid id) => Guard.NaoVazio(id, "o Id da cobrança").ToString("N");

    /// <summary>RN-PX-02 / RN-PX-05: 26 a 35 letras e números, sem acento nem símbolo.</summary>
    public static bool TxidValido(string? txid) =>
        txid is { Length: >= 26 and <= TamanhoMaximoTxid } && txid.All(char.IsAsciiLetterOrDigit);

    /// <summary>RN-PX-02: a cobrança que o banco acabou de criar (ou que já existia lá com o mesmo txid).</summary>
    public static CobrancaPix Criar(
        Guid id,
        Guid usuarioId,
        decimal valor,
        string pixCopiaECola,
        SituacaoCobrancaPix situacao,
        DateTime agoraUtc,
        int expiracaoSegundos = ExpiracaoPadraoSegundos)
    {
        Guard.Contra(expiracaoSegundos is < ExpiracaoMinimaSegundos or > ExpiracaoMaximaSegundos,
            $"A validade da cobrança Pix precisa ficar entre {ExpiracaoMinimaSegundos} e {ExpiracaoMaximaSegundos} segundos.");
        var agora = Guard.Utc(agoraUtc, "a hora da cobrança");
        var cobranca = new CobrancaPix(Guard.NaoVazio(id, "o Id da cobrança"))
        {
            UsuarioId = Guard.NaoVazio(usuarioId, "o atendente"),
            Valor = Guard.Dinheiro(valor, "o valor do Pix", permiteZero: false),
            PixCopiaECola = Guard.Texto(pixCopiaECola, "o código Pix copia e cola", 1, TamanhoMaximoCopiaECola),
            CriadaEm = agora,
            AtualizadaEm = agora,
            ExpiraEm = agora.AddSeconds(expiracaoSegundos),
            Status = StatusCobrancaPix.Ativa,
        };
        cobranca.Txid = TxidDe(cobranca.Id);
        cobranca.AtualizarPeloBanco(situacao, agora);
        return cobranca;
    }

    /// <summary>
    /// RN-PX-03: aplica o que o banco respondeu. Concluída é final (um retorno atrasado de "ativa" não desfaz o pagamento);
    /// Removida e Expirada só mudam para Concluída (o banco é quem manda se o dinheiro entrou). Devolve se algo mudou.
    /// </summary>
    public bool AtualizarPeloBanco(SituacaoCobrancaPix situacao, DateTime agoraUtc)
    {
        ArgumentNullException.ThrowIfNull(situacao);
        var agora = Guard.Utc(agoraUtc, "a hora da consulta");
        if (Status == StatusCobrancaPix.Concluida)
        {
            return false;
        }

        switch (situacao.Status)
        {
            case StatusCobrancaPix.Concluida:
                Status = StatusCobrancaPix.Concluida;
                ValorPago = situacao.ValorPago is { } pago ? Guard.Dinheiro(pago, "o valor pago", permiteZero: false) : Valor;
                PagaEm = situacao.PagaEm is { } quando ? Guard.Utc(quando, "a hora do pagamento") : agora;
                EndToEndId = Guard.TextoOpcional(situacao.EndToEndId, "o identificador do Pix", TamanhoMaximoEndToEndId);
                break;
            case StatusCobrancaPix.Removida or StatusCobrancaPix.Expirada when Status == StatusCobrancaPix.Ativa:
                Status = situacao.Status;
                break;
            default:
                return ExpirarSePassou(agora);
        }

        AtualizadaEm = agora;
        return true;
    }

    /// <summary>RN-PX-02: ativa e vencida há mais que a folga → expirada (o banco já não aceita o pagamento).</summary>
    public bool ExpirarSePassou(DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a hora da consulta");
        if (Status != StatusCobrancaPix.Ativa || agora <= ExpiraEm + FolgaExpiracao)
        {
            return false;
        }

        Status = StatusCobrancaPix.Expirada;
        AtualizadaEm = agora;
        return true;
    }

    /// <summary>RN-PX-06: o atendente desistiu (cliente vai pagar de outro jeito). Paga não pode ser cancelada.</summary>
    public void Cancelar(DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a hora do cancelamento");
        Guard.Contra(Status == StatusCobrancaPix.Concluida,
            "Este Pix já foi pago. Grave a venda em vez de cancelar (RN-PX-06).");
        if (Status != StatusCobrancaPix.Ativa)
        {
            return;
        }

        Status = StatusCobrancaPix.Removida;
        AtualizadaEm = agora;
    }
}
