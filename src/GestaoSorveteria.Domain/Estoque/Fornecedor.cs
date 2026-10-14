using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// RN-PF-01: quem vende para a loja (a fábrica dos pais da dona, descartáveis, casquinhas...).
/// Nunca é apagado: desativa.
/// </summary>
public sealed class Fornecedor : Entity
{
    public const int PrazoMaximoDias = 30;

    public string Nome { get; private set; }
    public string NomeNormalizado { get; private set; }

    /// <summary>Só dígitos, com DDD (10 ou 11) e opcionalmente o 55 do Brasil (12 ou 13).</summary>
    public string? WhatsApp { get; private set; }

    /// <summary>Dias da semana em que a dona faz o pedido (pelo menos um).</summary>
    public IReadOnlyCollection<DayOfWeek> DiasPedido => _diasPedido.AsReadOnly();

    public int PrazoEntregaDias { get; private set; }
    public bool AceitaMeiaCaixa { get; private set; }
    public FormaEnvioPedido Envio { get; private set; }

    /// <summary>A folha impressa mostra o total em R$ (RN-PF-07).</summary>
    public bool MostrarPrecos { get; private set; }

    public bool Ativo { get; private set; }

    private List<DayOfWeek> _diasPedido = new();

    // EF Core
    private Fornecedor()
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
    }

    private Fornecedor(Guid? id) : base(id)
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
        Ativo = true;
    }

    public static Fornecedor Criar(
        string nome,
        string? whatsApp,
        IEnumerable<DayOfWeek> diasPedido,
        int prazoEntregaDias,
        bool aceitaMeiaCaixa,
        FormaEnvioPedido envio,
        bool mostrarPrecos,
        Guid? id = null)
    {
        var fornecedor = new Fornecedor(id);
        fornecedor.Atualizar(nome, whatsApp, diasPedido, prazoEntregaDias, aceitaMeiaCaixa, envio, mostrarPrecos);
        return fornecedor;
    }

    public void Atualizar(
        string nome,
        string? whatsApp,
        IEnumerable<DayOfWeek> diasPedido,
        int prazoEntregaDias,
        bool aceitaMeiaCaixa,
        FormaEnvioPedido envio,
        bool mostrarPrecos)
    {
        // Valida tudo antes de mudar qualquer campo: um erro não deixa o fornecedor alterado pela metade.
        var nomeValido = NomeEstoque.Limpar(Guard.Texto(nome, "o nome do fornecedor", 2, 80));
        var whatsAppValido = NormalizarWhatsApp(whatsApp);

        var dias = diasPedido.Distinct().Order().ToList();
        Guard.Contra(dias.Count == 0, "Escolha pelo menos um dia da semana para o pedido.");
        Guard.Contra(dias.Any(d => !Enum.IsDefined(d)), "Dia da semana inválido.");
        Guard.Contra(prazoEntregaDias < 0 || prazoEntregaDias > PrazoMaximoDias,
            $"O prazo de entrega deve ser de 0 a {PrazoMaximoDias} dias.");
        Guard.Contra(!Enum.IsDefined(envio), "Forma de envio do pedido inválida.");
        Guard.Contra(envio is FormaEnvioPedido.WhatsApp or FormaEnvioPedido.Ambos && whatsAppValido is null,
            "Para mandar o pedido pelo WhatsApp, informe o número do fornecedor.");

        Nome = nomeValido;
        NomeNormalizado = NomeEstoque.Normalizar(nomeValido);
        WhatsApp = whatsAppValido;
        _diasPedido = dias;
        PrazoEntregaDias = prazoEntregaDias;
        Envio = envio;
        AceitaMeiaCaixa = aceitaMeiaCaixa;
        MostrarPrecos = mostrarPrecos;
    }

    public void Ativar() => Ativo = true;

    public void Desativar() => Ativo = false;

    /// <summary>Dia de pedido: o painel monta o rascunho sozinho (RN-PF-04); o app lembra da contagem na véspera (RN-ES-05).</summary>
    public bool PedeEm(DayOfWeek dia) => _diasPedido.Contains(dia);

    private static string? NormalizarWhatsApp(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        Guard.Contra(digitos.Length is < 10 or > 13,
            "WhatsApp inválido: use DDD + número, ex.: (11) 98765-4321.");
        return digitos;
    }
}
