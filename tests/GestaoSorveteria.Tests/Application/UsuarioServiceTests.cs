using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Usuarios;
using GestaoSorveteria.Contracts.Usuarios;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

public class UsuarioServiceTests
{
    private static readonly DateTime Agora = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);

    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly PasswordHasherFake _hasher = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new();
    private readonly UsuarioService _service;
    private readonly Usuario _dona;
    private readonly Usuario _joao;

    public UsuarioServiceTests()
    {
        _service = new UsuarioService(_usuarios, _hasher, _uow, _clock);
        _dona = Usuario.Criar("Dona Maria", "maria", _hasher.Hash("senha123"), PerfilUsuario.Admin, Agora);
        _joao = Usuario.Criar("João", "joao", _hasher.Hash("1234"), PerfilUsuario.Atendente, Agora);
        _usuarios.Usuarios.Add(_dona);
        _usuarios.Usuarios.Add(_joao);
    }

    private static CriarUsuarioRequest NovoUsuario(string login = "ana", string senha = "4321", string perfil = "Atendente", string nome = "Ana") =>
        new(nome, login, senha, perfil);

    [Fact]
    public async Task Listar_RetornaTodosInclusiveDesativados()
    {
        _joao.Desativar();

        var lista = await _service.ListarAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, lista.Count);
        Assert.Contains(lista, u => u.Login == "joao" && !u.Ativo);
        Assert.Contains(lista, u => u.Login == "maria" && u.Perfil == "Admin");
    }

    [Fact]
    public async Task Criar_Atendente_GravaHashENormalizaLogin()
    {
        var dto = await _service.CriarAsync(NovoUsuario(login: "  ANA "), TestContext.Current.CancellationToken);

        Assert.Equal("ana", dto.Login);
        Assert.Equal("Atendente", dto.Perfil);
        Assert.True(dto.Ativo);
        var gravado = Assert.Single(_usuarios.Usuarios, u => u.Login == "ana");
        Assert.Equal(_hasher.Hash("4321"), gravado.SenhaHash); // RN-US-04: só o hash
        Assert.Equal(_clock.UtcNow, gravado.CriadoEm);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Criar_AdminComSenhaForte_Funciona()
    {
        var dto = await _service.CriarAsync(NovoUsuario(login: "paulo", senha: "senhaforte1", perfil: "admin", nome: "Paulo"), TestContext.Current.CancellationToken);

        Assert.Equal("Admin", dto.Perfil);
    }

    [Fact]
    public async Task Criar_LoginRepetido_Lanca()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarAsync(NovoUsuario(login: "JOAO"), TestContext.Current.CancellationToken));

        Assert.Equal("Já existe um usuário com esse login.", ex.Message);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Theory]
    [InlineData("Atendente", "123")]       // RN-US-03: PIN curto
    [InlineData("Atendente", "12a4")]      // PIN com letra
    [InlineData("Atendente", "1234567")]   // PIN longo
    [InlineData("Admin", "curta")]         // senha de Admin curta
    public async Task Criar_SenhaForaDaPolitica_Lanca(string perfil, string senha)
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarAsync(NovoUsuario(senha: senha, perfil: perfil), TestContext.Current.CancellationToken));

        Assert.DoesNotContain(_usuarios.Usuarios, u => u.Login == "ana");
    }

    [Theory]
    [InlineData("Gerente")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Criar_PerfilDesconhecido_Lanca(string perfil)
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarAsync(NovoUsuario(perfil: perfil), TestContext.Current.CancellationToken));

        Assert.Contains("Atendente, Admin", ex.Message);
    }

    [Fact]
    public async Task Criar_LoginInvalido_Lanca()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CriarAsync(NovoUsuario(login: "ana silva"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RedefinirSenha_Atendente_TrocaOHash()
    {
        var dto = await _service.RedefinirSenhaAsync(_joao.Id, new RedefinirSenhaRequest("9876"), TestContext.Current.CancellationToken);

        Assert.Equal("joao", dto.Login);
        Assert.Equal(_hasher.Hash("9876"), _joao.SenhaHash);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task RedefinirSenha_AtendenteComSenhaDeAdmin_LancaPorqueValeAPoliticaDoPerfil()
    {
        var antes = _joao.SenhaHash;

        await Assert.ThrowsAsync<DomainException>(() =>
            _service.RedefinirSenhaAsync(_joao.Id, new RedefinirSenhaRequest("senhaforte1"), TestContext.Current.CancellationToken));

        Assert.Equal(antes, _joao.SenhaHash);
    }

    [Fact]
    public async Task RedefinirSenha_UsuarioInexistente_Lanca404()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.RedefinirSenhaAsync(Guid.NewGuid(), new RedefinirSenhaRequest("1234"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Desativar_OutroUsuario_DesativaSemApagar()
    {
        var dto = await _service.DesativarAsync(_joao.Id, _dona.Id, TestContext.Current.CancellationToken);

        Assert.False(dto.Ativo);
        Assert.Equal(2, _usuarios.Usuarios.Count); // RN-US-07: nunca apaga
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Desativar_ASiMesmo_Lanca()
    {
        // RN-US-10
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            _service.DesativarAsync(_dona.Id, _dona.Id, TestContext.Current.CancellationToken));

        Assert.Contains("próprio usuário", ex.Message);
        Assert.True(_dona.Ativo);
        Assert.Equal(0, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Ativar_UsuarioDesativado_VoltaAAtivo()
    {
        _joao.Desativar();

        var dto = await _service.AtivarAsync(_joao.Id, TestContext.Current.CancellationToken);

        Assert.True(dto.Ativo);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Ativar_UsuarioInexistente_Lanca404()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.AtivarAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Desativar_UsuarioInexistente_Lanca404()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.DesativarAsync(Guid.NewGuid(), _dona.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Obter_Inexistente_Lanca404()
    {
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() =>
            _service.ObterAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }
}
