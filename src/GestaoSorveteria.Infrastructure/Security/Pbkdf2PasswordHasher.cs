using System.Security.Cryptography;
using GestaoSorveteria.Application.Abstractions;

namespace GestaoSorveteria.Infrastructure.Security;

/// <summary>
/// RN-US-04 / ADR-008: PBKDF2-SHA256 com salt aleatório por usuário, só com a BCL.
/// Formato gravado: <c>PBKDF2-SHA256$iteracoes$saltBase64$hashBase64</c> — o prefixo e as iterações
/// ficam no próprio hash, então dá para aumentar o custo no futuro sem quebrar senhas antigas.
/// </summary>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public const string Algoritmo = "PBKDF2-SHA256";
    public const int IteracoesPadrao = 210_000;
    private const int TamanhoSalt = 16;
    private const int TamanhoHash = 32;
    private const char Separador = '$';

    private readonly int _iteracoes;

    public Pbkdf2PasswordHasher() : this(IteracoesPadrao)
    {
    }

    public Pbkdf2PasswordHasher(int iteracoes)
    {
        if (iteracoes < 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(iteracoes), "Use pelo menos 10.000 iterações.");
        }

        _iteracoes = iteracoes;
    }

    public string Hash(string senha)
    {
        ArgumentException.ThrowIfNullOrEmpty(senha);

        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, _iteracoes, HashAlgorithmName.SHA256, TamanhoHash);

        return string.Join(Separador, Algoritmo, _iteracoes, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verificar(string senha, string hashArmazenado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(hashArmazenado))
        {
            return false;
        }

        var partes = hashArmazenado.Split(Separador);
        if (partes.Length != 4 || partes[0] != Algoritmo || !int.TryParse(partes[1], out var iteracoes) || iteracoes <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] esperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var calculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
