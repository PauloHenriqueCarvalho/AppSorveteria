using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// RN-AV-01: "Está acabando" — item do catálogo ou texto livre, quanto tem agora e a prioridade.
/// Nasce no tablet (offline) ou no painel; aberto até a entrega chegar ou a dona fechar (RN-AV-05). Nada é apagado.
/// </summary>
public sealed class Aviso : Entity
{
    public const int QuantidadeMaxima = 9999;

    public Guid? ItemEstoqueId { get; private set; }

    /// <summary>"Outro": quando o item não está no catálogo (ex.: "tampa do pote 2L").</summary>
    public string? TextoLivre { get; private set; }

    /// <summary>Quanto tem agora, na unidade que o atendente vê (0 = acabou).</summary>
    public int QuantidadeAtual { get; private set; }

    public PrioridadeAviso Prioridade { get; private set; }
    public string? Observacao { get; private set; }

    /// <summary>Nulo = criado pelo sistema (pote no mínimo ou zerado — RN-AV-04).</summary>
    public Guid? CriadoPorUsuarioId { get; private set; }

    public DateTime CriadoEm { get; private set; }

    /// <summary>Último "Ainda falta" (ou a criação).</summary>
    public Guid? AtualizadoPorUsuarioId { get; private set; }
    public DateTime AtualizadoEm { get; private set; }

    public StatusAviso Status { get; private set; }
    public DateTime? FechadoEm { get; private set; }
    public Guid? FechadoPorUsuarioId { get; private set; }
    public string? MotivoFechamento { get; private set; }

    /// <summary>Fechado pela conferência da entrega deste pedido (RN-AV-05a).</summary>
    public Guid? FechadoPeloPedidoId { get; private set; }

    /// <summary>RN-AV-08: quando o link do WhatsApp foi aberto — nunca "entregue".</summary>
    public DateTime? WhatsAppAbertoEm { get; private set; }

    public bool Aberto => Status == StatusAviso.Aberto;
    public bool CriadoPeloSistema => CriadoPorUsuarioId is null;

    // EF Core
    private Aviso()
    {
    }

    private Aviso(Guid? id) : base(id)
    {
        Status = StatusAviso.Aberto;
    }

    public static Aviso DoItem(Guid itemEstoqueId, int quantidadeAtual, PrioridadeAviso prioridade, string? observacao, Guid? usuarioId, DateTime agoraUtc, Guid? id = null)
    {
        var aviso = new Aviso(id) { ItemEstoqueId = Guard.NaoVazio(itemEstoqueId, "o item") };
        aviso.Iniciar(quantidadeAtual, prioridade, observacao, usuarioId, agoraUtc);
        return aviso;
    }

    public static Aviso DeTextoLivre(string texto, int quantidadeAtual, PrioridadeAviso prioridade, string? observacao, Guid usuarioId, DateTime agoraUtc, Guid? id = null)
    {
        var aviso = new Aviso(id) { TextoLivre = Guard.Texto(texto, "o que está acabando", 2, 120) };
        aviso.Iniciar(quantidadeAtual, prioridade, observacao, Guard.NaoVazio(usuarioId, "quem avisou"), agoraUtc);
        return aviso;
    }

    /// <summary>
    /// RN-AV-03 "Ainda falta": atualiza quantidade e observação do mesmo aviso; a prioridade só pode subir
    /// (Média → Alta → Urgente). Devolve <c>true</c> se virou Urgente agora (o app abre o WhatsApp — RN-AV-06).
    /// </summary>
    public bool AindaFalta(int quantidadeAtual, PrioridadeAviso prioridade, string? observacao, Guid? usuarioId, DateTime agoraUtc)
    {
        Guard.Contra(!Aberto, "Este aviso já foi fechado. Crie um aviso novo.");
        ValidarPrioridade(prioridade);
        Guard.Contra(prioridade < Prioridade, $"\"Ainda falta\" não pode baixar a prioridade (está {Nome(Prioridade)}).");
        var agora = Guard.Utc(agoraUtc, "a data");
        Guard.Contra(agora < CriadoEm, "A atualização não pode ser anterior ao aviso.");

        var virouUrgente = prioridade == PrioridadeAviso.Urgente && Prioridade != PrioridadeAviso.Urgente;
        QuantidadeAtual = ValidarQuantidade(quantidadeAtual);
        Prioridade = prioridade;
        Observacao = Guard.TextoOpcional(observacao, "a observação", 200) ?? Observacao;
        AtualizadoPorUsuarioId = usuarioId;
        AtualizadoEm = agora;
        return virouUrgente;
    }

    /// <summary>
    /// Atualização que chega pela sincronização (o tablet pode ter ficado sem internet). Diferente do "Ainda falta" na hora:
    /// a prioridade fica com a maior das duas (nunca baixa), e quantidade/observação só mudam se a informação for mais nova
    /// que a do servidor — reenvio fora de ordem não volta a quantidade para trás. Devolve <c>true</c> se virou Urgente agora.
    /// </summary>
    /// <param name="aplicarMesmoSeMaisAntiga">
    /// true no caminho online (painel ou app conectado): o que a pessoa acabou de informar vale, mesmo que o relógio de quem
    /// gravou antes estivesse adiantado. A prioridade continua nunca baixando.
    /// </param>
    public bool ReceberAtualizacao(
        int quantidadeAtual, PrioridadeAviso prioridade, string? observacao, Guid? usuarioId, DateTime emUtc, bool aplicarMesmoSeMaisAntiga = false)
    {
        Guard.Contra(!Aberto, "Este aviso já foi fechado.");
        ValidarPrioridade(prioridade);
        var quantidade = ValidarQuantidade(quantidadeAtual);
        var obs = Guard.TextoOpcional(observacao, "a observação", 200);
        var em = Guard.Utc(emUtc, "a data");

        var maior = prioridade > Prioridade ? prioridade : Prioridade;
        var virouUrgente = maior == PrioridadeAviso.Urgente && Prioridade != PrioridadeAviso.Urgente;
        if (aplicarMesmoSeMaisAntiga || em >= AtualizadoEm)
        {
            QuantidadeAtual = quantidade;
            Observacao = obs ?? Observacao;
            AtualizadoPorUsuarioId = usuarioId;
            AtualizadoEm = em;
        }

        Prioridade = maior;
        return virouUrgente;
    }

    /// <summary>RN-AV-05b: a dona fecha à mão, com motivo ("comprei no mercado").</summary>
    public void Fechar(string motivo, Guid usuarioId, DateTime agoraUtc)
    {
        Guard.Contra(!Aberto, "Este aviso já está fechado.");
        MotivoFechamento = Guard.Texto(motivo, "o motivo", 3, 200);
        FechadoPorUsuarioId = Guard.NaoVazio(usuarioId, "quem fechou");
        FecharEm(agoraUtc);
    }

    /// <summary>RN-AV-05a: a entrega com este item foi conferida com quantidade &gt; 0.</summary>
    public void FecharPelaEntrega(Guid pedidoId, DateTime agoraUtc)
    {
        if (!Aberto)
        {
            return; // idempotente: reconferir a entrega não muda nada
        }

        FechadoPeloPedidoId = Guard.NaoVazio(pedidoId, "o pedido");
        MotivoFechamento = "Chegou na entrega";
        FecharEm(agoraUtc);
    }

    /// <summary>RN-AV-08: registra só a primeira vez que o link foi aberto.</summary>
    public void MarcarWhatsAppAberto(DateTime agoraUtc) => WhatsAppAbertoEm ??= Guard.Utc(agoraUtc, "a data");

    private void Iniciar(int quantidadeAtual, PrioridadeAviso prioridade, string? observacao, Guid? usuarioId, DateTime agoraUtc)
    {
        ValidarPrioridade(prioridade);
        QuantidadeAtual = ValidarQuantidade(quantidadeAtual);
        Prioridade = prioridade;
        Observacao = Guard.TextoOpcional(observacao, "a observação", 200);
        Guard.Contra(usuarioId == Guid.Empty, "Informe quem avisou.");
        CriadoPorUsuarioId = usuarioId;
        AtualizadoPorUsuarioId = usuarioId;
        CriadoEm = Guard.Utc(agoraUtc, "a data do aviso");
        AtualizadoEm = CriadoEm;
    }

    private void FecharEm(DateTime agoraUtc)
    {
        var agora = Guard.Utc(agoraUtc, "a data");
        Guard.Contra(agora < CriadoEm, "O fechamento não pode ser anterior ao aviso.");
        FechadoEm = agora;
        Status = StatusAviso.Fechado;
    }

    private static int ValidarQuantidade(int quantidade)
    {
        Guard.Contra(quantidade < 0, "A quantidade atual não pode ser negativa (0 = acabou).");
        Guard.Contra(quantidade > QuantidadeMaxima, $"A quantidade atual deve ser no máximo {QuantidadeMaxima}.");
        return quantidade;
    }

    private static void ValidarPrioridade(PrioridadeAviso prioridade) =>
        Guard.Contra(!Enum.IsDefined(prioridade), "Prioridade inválida: use Urgente, Alta ou Média.");

    private static string Nome(PrioridadeAviso prioridade) => prioridade switch
    {
        PrioridadeAviso.Urgente => "Urgente",
        PrioridadeAviso.Alta => "Alta",
        _ => "Média",
    };
}
