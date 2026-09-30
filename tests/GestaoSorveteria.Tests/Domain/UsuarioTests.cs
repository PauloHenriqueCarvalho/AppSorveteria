using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Domain;

public class UsuarioTests
{
    [Fact]
    public void Criar_NormalizaLogin()
    {
        var usuario = Usuario.Criar("Maria Silva", "  Maria.Silva ", "hash", PerfilUsuario.Admin, Cenario.Agora);

        Assert.Equal("maria.silva", usuario.Login);
        Assert.Equal("Maria Silva", usuario.Nome);
        Assert.True(usuario.Ativo);
        Assert.Equal(PerfilUsuario.Admin, usuario.Perfil);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("maria silva")]
    [InlineData("maria@loja")]
    [InlineData("")]
    public void Criar_LoginInvalido_Lanca(string login)
    {
        Assert.Throws<DomainException>(() => Usuario.Criar("Maria", login, "hash", PerfilUsuario.Atendente, Cenario.Agora));
    }

    [Fact]
    public void Criar_SemHash_Lanca()
    {
        Assert.Throws<DomainException>(() => Usuario.Criar("Maria", "maria", "", PerfilUsuario.Atendente, Cenario.Agora));
    }

    [Fact]
    public void DesativarEAtivar()
    {
        var usuario = Cenario.Admin();

        usuario.Desativar();
        Assert.False(usuario.Ativo);

        usuario.Ativar();
        Assert.True(usuario.Ativo);
    }
}

public class PoliticaSenhaTests
{
    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    public void Atendente_PinDe4a6Digitos_Aceito(string pin)
    {
        PoliticaSenha.Validar(PerfilUsuario.Atendente, pin);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("12ab")]
    [InlineData("")]
    [InlineData((string?)null)]
    public void Atendente_PinInvalido_Lanca(string? pin)
    {
        Assert.Throws<DomainException>(() => PoliticaSenha.Validar(PerfilUsuario.Atendente, pin));
    }

    [Theory]
    [InlineData("senha123")]
    [InlineData("uma senha bem longa")]
    public void Admin_SenhaComOitoOuMais_Aceita(string senha)
    {
        PoliticaSenha.Validar(PerfilUsuario.Admin, senha);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("1234567")]
    [InlineData("        ")]
    public void Admin_SenhaCurta_Lanca(string senha)
    {
        Assert.Throws<DomainException>(() => PoliticaSenha.Validar(PerfilUsuario.Admin, senha));
    }
}
