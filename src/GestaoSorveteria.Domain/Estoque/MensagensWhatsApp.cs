using System.Globalization;
using System.Text;
using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// Textos que vão para o WhatsApp da dona (docs/10 §2.3). Ficam no Domain porque o app monta o urgente sem internet
/// (RN-AV-06) e a API monta o resumo do dia (RN-AV-07): os dois saem iguais. Datas já chegam no horário da loja.
/// O envio é por link: o sistema nunca afirma que a mensagem foi entregue (RN-AV-08).
/// </summary>
public static class MensagensWhatsApp
{
    private static readonly string[] DiasDaSemana = ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];

    /// <summary>Uma linha do resumo: "Picolé ao leite Coco preto" e a quantidade que tinha quando avisaram.</summary>
    public sealed record LinhaAviso(string Descricao, int QuantidadeAtual);

    /// <summary>"Coco preto" do grupo "Picolé ao leite" → "Picolé ao leite Coco preto"; texto livre fica como veio.</summary>
    public static string DescricaoDoItem(string? grupo, string nome) =>
        string.IsNullOrWhiteSpace(grupo) ? nome.Trim() : $"{grupo.Trim()} {nome.Trim()}";

    /// <summary>
    /// 🔴 URGENTE — Verão Tropical
    /// Picolé ao leite Coco preto: ACABOU
    /// Cliente pediu e não tinha.
    /// Ana · 14/10 15:42
    /// </summary>
    public static string Urgente(string nomeLoja, string descricao, int quantidadeAtual, string? observacao, string autor, DateTime quandoNaLoja)
    {
        var texto = new StringBuilder()
            .Append("🔴 URGENTE — ").Append(nomeLoja.Trim()).Append('\n')
            .Append(descricao.Trim()).Append(": ").Append(Quantidade(quantidadeAtual, maiusculo: true)).Append('\n');
        if (!string.IsNullOrWhiteSpace(observacao))
        {
            texto.Append(observacao.Trim()).Append('\n');
        }

        return texto.Append(autor.Trim()).Append(" · ").Append(quandoNaLoja.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)).ToString();
    }

    /// <summary>
    /// Resumo de quarta 14/10 — Verão Tropical
    /// 🔴 Urgente (1): Picolé ao leite Coco preto — acabou
    /// 🟠 Alta (2): Pote 1L Flocos — 1 · Copo 300 ml — 1
    /// 🟡 Média (1): Massa Pistache — 1
    /// Vendas do dia: R$ 1.284,50
    /// </summary>
    public static string ResumoDoDia(
        string nomeLoja,
        DateOnly dia,
        IReadOnlyList<LinhaAviso> urgentes,
        IReadOnlyList<LinhaAviso> altas,
        IReadOnlyList<LinhaAviso> medias,
        decimal? totalVendido)
    {
        var texto = new StringBuilder()
            .Append("Resumo de ").Append(DiasDaSemana[(int)dia.DayOfWeek]).Append(' ')
            .Append(dia.ToString("dd/MM", CultureInfo.InvariantCulture)).Append(" — ").Append(nomeLoja.Trim());

        if (urgentes.Count + altas.Count + medias.Count == 0)
        {
            texto.Append("\n✅ Nenhum aviso de estoque aberto.");
        }

        Secao(texto, "🔴 Urgente", urgentes);
        Secao(texto, "🟠 Alta", altas);
        Secao(texto, "🟡 Média", medias);

        if (totalVendido is { } total)
        {
            texto.Append("\nVendas do dia: ").Append(Reais(total));
        }

        return texto.ToString();
    }

    /// <summary>Link que abre o WhatsApp com a mensagem pronta; sem número, o próprio WhatsApp pede o contato.</summary>
    public static string Link(string? whatsAppDigitos, string mensagem)
    {
        var destino = string.IsNullOrWhiteSpace(whatsAppDigitos) ? string.Empty : Telefone.ParaLinkWhatsApp(whatsAppDigitos);
        return $"https://wa.me/{destino}?text={Uri.EscapeDataString(mensagem)}";
    }

    /// <summary>1284.5 → "R$ 1.284,50" (sem depender da cultura do servidor ou do tablet).</summary>
    public static string Reais(decimal valor)
    {
        var texto = Moeda.Arredondar(valor).ToString("#,##0.00", CultureInfo.InvariantCulture);
        return "R$ " + texto.Replace(',', '_').Replace('.', ',').Replace('_', '.');
    }

    private static void Secao(StringBuilder texto, string titulo, IReadOnlyList<LinhaAviso> linhas)
    {
        if (linhas.Count == 0)
        {
            return;
        }

        texto.Append('\n').Append(titulo).Append(" (").Append(linhas.Count).Append("): ")
            .AppendJoin(" · ", linhas.Select(l => $"{l.Descricao.Trim()} — {Quantidade(l.QuantidadeAtual, maiusculo: false)}"));
    }

    private static string Quantidade(int quantidade, bool maiusculo) =>
        quantidade == 0 ? (maiusculo ? "ACABOU" : "acabou") : maiusculo ? $"restam {quantidade}" : quantidade.ToString(CultureInfo.InvariantCulture);
}
