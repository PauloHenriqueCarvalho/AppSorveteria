using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Sugestoes;
using GestaoSorveteria.Contracts.Sugestoes;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Application;

/// <summary>Sugestões da dona no painel (RN-SG-01..05).</summary>
public sealed class SugestaoServiceTests
{
    private readonly SugestaoRepositoryFake _sugestoes = new();
    private readonly UsuarioRepositoryFake _usuarios = new();
    private readonly UnitOfWorkFake _uow = new();
    private readonly ClockFake _clock = new();
    private readonly SugestaoService _servico;
    private readonly Usuario _dona;
    private readonly Usuario _paulo;

    public SugestaoServiceTests()
    {
        _dona = Usuario.Criar("Laura", "admin", "hash", PerfilUsuario.Admin, _clock.UtcNow.AddDays(-30));
        _paulo = Usuario.Criar("Paulo", "paulo", "hash", PerfilUsuario.Admin, _clock.UtcNow.AddDays(-30));
        _usuarios.Usuarios.AddRange([_dona, _paulo]);
        _servico = new SugestaoService(_sugestoes, _usuarios, _uow, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static SalvarSugestaoRequest Pedido(string titulo = "Relatório por atendente") =>
        new(titulo, "Quero ver quanto cada uma vendeu no mês.", "Ideia", "Relatorios", "Media");

    [Fact]
    public async Task Criar_DevolveComNomeDeQuemPediuEConfirma()
    {
        var dto = await _servico.CriarAsync(Pedido(), _dona.Id, Ct);

        Assert.Equal("Nova", dto.Status);
        Assert.Equal("Laura", dto.CriadaPorNome);
        Assert.Equal("Relatorios", dto.Area);
        Assert.Equal(1, _uow.Confirmacoes);
    }

    [Fact]
    public async Task Criar_TipoDesconhecido_Lanca() =>
        await Assert.ThrowsAsync<DomainException>(() =>
            _servico.CriarAsync(Pedido() with { Tipo = "Elogio" }, _dona.Id, Ct));

    [Fact]
    public async Task Listar_AbertasAntesDasEncerradas()
    {
        var feita = await _servico.CriarAsync(Pedido("Primeira"), _dona.Id, Ct);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _servico.AlterarStatusAsync(feita.Id, new AlterarStatusSugestaoRequest("Feita", "Pronto!"), _paulo.Id, Ct);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        var aberta = await _servico.CriarAsync(Pedido("Segunda"), _dona.Id, Ct);
        _clock.UtcNow = _clock.UtcNow.AddHours(1);
        await _servico.ComentarAsync(feita.Id, new ComentarSugestaoRequest("Obrigada!"), _dona.Id, Ct); // atualizada por último

        var lista = await _servico.ListarAsync(Ct);

        Assert.Equal([aberta.Id, feita.Id], lista.Select(s => s.Id));
        Assert.Equal(2, lista[1].Comentarios);
    }

    [Fact]
    public async Task AnexarImagem_RegistraNaConversaEContaNaLista()
    {
        var dto = await _servico.CriarAsync(Pedido(), _dona.Id, Ct);

        var comImagem = await _servico.AnexarImagemAsync(dto.Id, "print.png", "image/png", [1, 2, 3], _dona.Id, Ct);

        var imagem = Assert.Single(comImagem.Imagens);
        Assert.Equal("print.png", imagem.NomeArquivo);
        Assert.Contains(comImagem.Comentarios, c => c.Texto.Contains("print.png"));
        Assert.Equal(1, Assert.Single(await _servico.ListarAsync(Ct)).Imagens);
        Assert.Equal([1, 2, 3], (await _servico.ObterImagemAsync(dto.Id, imagem.Id, Ct)).Conteudo);
    }

    [Fact]
    public async Task RemoverImagem_SugestaoEncerrada_Lanca()
    {
        var dto = await _servico.CriarAsync(Pedido(), _dona.Id, Ct);
        var comImagem = await _servico.AnexarImagemAsync(dto.Id, "print.png", "image/png", [1], _dona.Id, Ct);
        await _servico.AlterarStatusAsync(dto.Id, new AlterarStatusSugestaoRequest("Recusada", "Já existe em Vendas, filtro por atendente."), _paulo.Id, Ct);

        await Assert.ThrowsAsync<DomainException>(() =>
            _servico.RemoverImagemAsync(dto.Id, comImagem.Imagens[0].Id, _dona.Id, Ct));
        Assert.Single(_sugestoes.Imagens);
    }

    [Fact]
    public async Task Obter_Inexistente_Lanca404() =>
        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() => _servico.ObterAsync(Guid.NewGuid(), Ct));
}
