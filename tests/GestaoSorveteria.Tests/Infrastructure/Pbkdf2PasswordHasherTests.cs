using GestaoSorveteria.Infrastructure.Security;

namespace GestaoSorveteria.Tests.Infrastructure;

public class Pbkdf2PasswordHasherTests
{
    // Menos iterações só para o teste ser rápido; em produção vale IteracoesPadrao.
    private readonly Pbkdf2PasswordHasher _hasher = new(10_000);

    [Fact]
    public void Hash_VerificaSenhaCorreta()
    {
        var hash = _hasher.Hash("senha123");

        Assert.True(_hasher.Verificar("senha123", hash));
    }

    [Fact]
    public void Hash_RejeitaSenhaErrada()
    {
        var hash = _hasher.Hash("senha123");

        Assert.False(_hasher.Verificar("senha124", hash));
        Assert.False(_hasher.Verificar("", hash));
    }

    [Fact]
    public void Hash_MesmaSenha_GeraHashesDiferentes()
    {
        Assert.NotEqual(_hasher.Hash("1234"), _hasher.Hash("1234")); // salt aleatório
    }

    [Fact]
    public void Hash_TemFormatoVersionado()
    {
        var hash = _hasher.Hash("1234");
        var partes = hash.Split('$');

        Assert.Equal(4, partes.Length);
        Assert.Equal(Pbkdf2PasswordHasher.Algoritmo, partes[0]);
        Assert.Equal("10000", partes[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("lixo")]
    [InlineData("PBKDF2-SHA256$abc$x$y")]
    [InlineData("OUTRO$10000$c2FsdA==$aGFzaA==")]
    [InlineData("PBKDF2-SHA256$10000$nao-e-base64$aGFzaA==")]
    public void Verificar_HashInvalido_RetornaFalseSemLancar(string hashArmazenado)
    {
        Assert.False(_hasher.Verificar("1234", hashArmazenado));
    }

    [Fact]
    public void Hash_SenhaVazia_Lanca()
    {
        Assert.Throws<ArgumentException>(() => _hasher.Hash(""));
    }
}
