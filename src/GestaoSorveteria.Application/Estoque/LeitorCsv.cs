using System.Globalization;
using System.Text;
using GestaoSorveteria.Application.Common;

namespace GestaoSorveteria.Application.Estoque;

/// <summary>
/// Lê o CSV do catálogo como ele sai do repositório (vírgula, ponto decimal) ou depois de aberto e salvo no Excel brasileiro
/// (ponto e vírgula, vírgula decimal). Aspas seguem o padrão: "a, b" é um campo só e "" dentro de aspas é uma aspa.
/// </summary>
internal static class LeitorCsv
{
    public const int TamanhoMaximo = 1_000_000;

    public sealed record Linha(int Numero, IReadOnlyList<string> Campos);

    /// <summary>Linhas com o número que o Excel mostraria (cabeçalho = 1). Linhas em branco ficam de fora.</summary>
    public static IReadOnlyList<Linha> Ler(string? conteudo)
    {
        var texto = (conteudo ?? string.Empty).TrimStart('﻿');
        Exigir.Que(!string.IsNullOrWhiteSpace(texto), "O arquivo está vazio.");
        Exigir.Que(texto.Length <= TamanhoMaximo, "O arquivo é grande demais para um catálogo (máximo de 1 MB).");

        var separador = DetectarSeparador(texto);
        var linhas = new List<Linha>();
        var campos = new List<string>();
        var campo = new StringBuilder();
        var entreAspas = false;
        var numero = 1;
        var inicioDaLinha = 1;

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
                    if (c == '\n')
                    {
                        numero++;
                    }

                    campo.Append(c);
                }
            }
            else if (c == '"')
            {
                entreAspas = true;
            }
            else if (c == separador)
            {
                campos.Add(campo.ToString());
                campo.Clear();
            }
            else if (c is '\n' or '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
                {
                    i++;
                }

                FecharLinha();
                numero++;
                inicioDaLinha = numero;
            }
            else
            {
                campo.Append(c);
            }
        }

        Exigir.Que(!entreAspas, $"Aspas abertas e não fechadas a partir da linha {inicioDaLinha}.");
        FecharLinha();
        return linhas;

        void FecharLinha()
        {
            campos.Add(campo.ToString());
            campo.Clear();
            if (campos.Any(c => !string.IsNullOrWhiteSpace(c)))
            {
                linhas.Add(new Linha(inicioDaLinha, campos.Select(c => c.Trim()).ToList()));
            }

            campos = new List<string>();
        }
    }

    /// <summary>"25.20", "25,20", "R$ 1.234,56" → decimal. Null quando não é número.</summary>
    public static decimal? LerDecimal(string? texto)
    {
        var limpo = (texto ?? string.Empty).Replace("R$", string.Empty).Replace(" ", string.Empty).Replace(" ", string.Empty);
        if (limpo.Length == 0)
        {
            return null;
        }

        if (limpo.Contains(','))
        {
            limpo = limpo.Replace(".", string.Empty).Replace(',', '.');
        }

        return decimal.TryParse(limpo, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : null;
    }

    /// <summary>O separador é o que mais aparece na primeira linha fora de aspas (o cabeçalho).</summary>
    private static char DetectarSeparador(string texto)
    {
        int virgulas = 0, pontoEVirgulas = 0;
        var entreAspas = false;
        foreach (var c in texto)
        {
            if (c == '"')
            {
                entreAspas = !entreAspas;
            }
            else if (!entreAspas && c is '\n' or '\r')
            {
                break;
            }
            else if (!entreAspas && c == ',')
            {
                virgulas++;
            }
            else if (!entreAspas && c == ';')
            {
                pontoEVirgulas++;
            }
        }

        return pontoEVirgulas > virgulas ? ';' : ',';
    }
}
