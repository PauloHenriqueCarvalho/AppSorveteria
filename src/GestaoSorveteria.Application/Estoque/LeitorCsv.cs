using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using GestaoSorveteria.Application.Common;

namespace GestaoSorveteria.Application.Estoque;

/// <summary>
/// Lê o CSV do catálogo como ele sai do repositório (vírgula, ponto decimal) ou depois de aberto e salvo no Excel brasileiro
/// como "CSV UTF-8" (ponto e vírgula, vírgula decimal). Aspas como no Excel: só abrem no começo do campo ("a, b" é um campo só,
/// "" dentro delas é uma aspa); no meio do texto são um caractere comum (Copo 5" grande).
/// </summary>
public static class LeitorCsv
{
    public const int TamanhoMaximo = 1_000_000;

    private const char Bom = '\uFEFF';

    /// <summary>Caractere que aparece no lugar das letras acentuadas quando o arquivo não é UTF-8 (CSV comum do Excel no Windows).</summary>
    private const char Substituto = '\uFFFD';

    private static readonly Regex Inteiro = new("^[0-9]+$", RegexOptions.CultureInvariant);
    private static readonly Regex PontoDecimal = new(@"^[0-9]+\.[0-9]{1,2}$", RegexOptions.CultureInvariant);
    private static readonly Regex PontoDeMilhar = new(@"^[0-9]{1,3}(\.[0-9]{3})+$", RegexOptions.CultureInvariant);
    private static readonly Regex VirgulaDecimal = new(@"^([0-9]{1,3}(\.[0-9]{3})*|[0-9]+),[0-9]{1,2}$", RegexOptions.CultureInvariant);

    public sealed record Linha(int Numero, IReadOnlyList<string> Campos);

    /// <summary>
    /// Registros com o número da linha que o Excel mostra (cabeçalho = 1; um campo com quebra de linha continua na mesma linha).
    /// Linhas em branco contam no número, mas ficam de fora da lista.
    /// </summary>
    public static IReadOnlyList<Linha> Ler(string? conteudo)
    {
        var texto = (conteudo ?? string.Empty).TrimStart(Bom);
        Exigir.Que(!string.IsNullOrWhiteSpace(texto), "O arquivo está vazio.");
        Exigir.Que(texto.Length <= TamanhoMaximo, "O arquivo é grande demais para um catálogo (máximo de 1 MB).");
        Exigir.Que(!texto.Contains(Substituto),
            "Os acentos do arquivo vieram estragados: ele não foi salvo em UTF-8. No Excel, use Salvar como → \"CSV UTF-8 (delimitado por vírgulas)\".");
        texto = texto.Normalize(NormalizationForm.FormC);

        var separador = DetectarSeparador(texto);
        var linhas = new List<Linha>();
        var campos = new List<string>();
        var campo = new StringBuilder();
        var entreAspas = false;
        var inicioDoCampo = true;
        var numero = 1;

        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreAspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    campo.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    entreAspas = false;
                }
                else
                {
                    campo.Append(c);
                }
            }
            else if (c == '"' && inicioDoCampo)
            {
                entreAspas = true;
                inicioDoCampo = false;
            }
            else if (c == separador)
            {
                campos.Add(campo.ToString());
                campo.Clear();
                inicioDoCampo = true;
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
                {
                    i++;
                }

                FecharLinha();
                numero++;
            }
            else
            {
                campo.Append(c);
                inicioDoCampo = false;
            }
        }

        Exigir.Que(!entreAspas, $"Aspas abertas e não fechadas na linha {numero}.");
        FecharLinha();
        Exigir.Que(linhas.Count > 0, "O arquivo está vazio.");
        return linhas;

        void FecharLinha()
        {
            campos.Add(campo.ToString());
            campo.Clear();
            inicioDoCampo = true;
            if (campos.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                linhas.Add(new Linha(numero, campos.Select(c => c.Trim()).ToList()));
            }

            campos = new List<string>();
        }
    }

    /// <summary>
    /// Valor em reais: "25.20" e "25,20" (centavos), "1.200" e "R$ 1.200,00" (ponto de milhar), "30". Null quando não dá para ter
    /// certeza do valor (ex.: "1,000.00" ou "12.345.6"): melhor recusar a linha do que gravar um custo errado.
    /// </summary>
    public static decimal? LerDecimal(string? texto)
    {
        var limpo = (texto ?? string.Empty).Replace("R$", string.Empty);
        limpo = new string(limpo.Where(c => !char.IsWhiteSpace(c)).ToArray());

        if (Inteiro.IsMatch(limpo) || PontoDecimal.IsMatch(limpo))
        {
            return decimal.Parse(limpo, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        if (PontoDeMilhar.IsMatch(limpo))
        {
            return decimal.Parse(limpo.Replace(".", string.Empty), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        if (VirgulaDecimal.IsMatch(limpo))
        {
            return decimal.Parse(limpo.Replace(".", string.Empty).Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>Só dígitos ("28"); "1.000" ou "28,5" não são aceitos como quantidade.</summary>
    public static int? LerInteiro(string? texto) =>
        int.TryParse((texto ?? string.Empty).Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var valor) ? valor : null;

    /// <summary>O separador é o que mais aparece no cabeçalho (primeira linha).</summary>
    private static char DetectarSeparador(string texto)
    {
        var fim = texto.IndexOfAny(['\n', '\r']);
        var cabecalho = fim < 0 ? texto : texto[..fim];
        return cabecalho.Count(c => c == ';') > cabecalho.Count(c => c == ',') ? ';' : ',';
    }
}
