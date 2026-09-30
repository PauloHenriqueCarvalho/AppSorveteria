using GestaoSorveteria.Application.Auth;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

public class AuthServiceTests
{
    private static readonly DateTime Agora = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly PasswordHasherFake _hasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(_usuarios, _hasher, new TokenServiceFake());
        _usuarios.Usuarios.Add(Usuario.Criar("Dona Maria", "maria", _hasher.Hash("senha123"), PerfilUsuario.Admin, Agora));
        _usuarios.Usuarios.Add(Usuario.Criar("João", "joao", _hasher.Hash("1234"), PerfilUsuario.Atendente, Agora));
    }

    [Fact]
    public async Task Login_Correto_RetornaTokenEUsuario()
    {
        var resposta = await _service.LoginAsync(new LoginRequest("maria", "senha123"));

        Assert.NotNull(resposta);
        Assert.Equal("token-maria", resposta.Token);
        Assert.Equal(TokenServiceFake.Expira, resposta.ExpiraEmUtc);
        Assert.Equal("maria", resposta.Usuario.Login);
        Assert.Equal("Admin", resposta.Usuario.Perfil);
        Assert.Equal("Dona Maria", resposta.Usuario.Nome);
    }

    [Fact]
    public async Task Login_NormalizaLoginDigitado()
    {
        var resposta = await _service.LoginAsync(new LoginRequest("  MARIA ", "senha123"));

        Assert.NotNull(resposta);
        Assert.Equal("maria", resposta.Usuario.Login);
    }

    [Fact]
    public async Task Login_AtendenteComPin_Funciona()
    {
        var resposta = await _service.LoginAsync(new LoginRequest("joao", "1234"));

        Assert.NotNull(resposta);
        Assert.Equal("Atendente", resposta.Usuario.Perfil);
    }

    [Fact]
    public async Task Login_SenhaErrada_RetornaNull()
    {
        var resposta = await _service.LoginAsync(new LoginRequest("maria", "errada"));

        Assert.Null(resposta);
    }

    [Fact]
    public async Task Login_UsuarioInexistente_RetornaNull()
    {
        var resposta = await _service.LoginAsync(new LoginRequest("ninguem", "senha123"));

        Assert.Null(resposta);
    }

    [Fact]
    public async Task Login_UsuarioDesativado_RetornaNull()
    {
        _usuarios.Usuarios.Single(u => u.Login == "joao").Desativar();

        var resposta = await _service.LoginAsync(new LoginRequest("joao", "1234"));

        Assert.Null(resposta);
    }

    [Theory]
    [InlineData("", "senha123")]
    [InlineData("maria", "")]
    public async Task Login_CamposVazios_RetornaNull(string login, string senha)
    {
        var resposta = await _service.LoginAsync(new LoginRequest(login, senha));

        Assert.Null(resposta);
    }
}
