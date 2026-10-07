using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Auth;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Contracts.Usuarios;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Usuarios;

/// <summary>
/// Gestão de usuários pelo painel da dona (RN-US-01…08, RN-US-10). Só Admin chega aqui (política no controller).
/// Usuário nunca é apagado: desativar é a única forma de tirá-lo do sistema (RN-US-07).
/// </summary>
public sealed class UsuarioService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IPasswordHasher _hasher;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public UsuarioService(IUsuarioRepository usuarios, IPasswordHasher hasher, IUnitOfWork uow, IClock clock)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Todos os usuários, inclusive desativados (o painel mostra o status).</summary>
    public async Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var usuarios = await _usuarios.ListarAsync(cancellationToken);
        return usuarios.Select(u => u.ToDto()).ToList();
    }

    public async Task<UsuarioDto> ObterAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await CarregarAsync(id, cancellationToken)).ToDto();

    /// <summary>
    /// Cria atendente (PIN) ou outro Admin (senha). A senha em texto é validada pela <see cref="PoliticaSenha"/>
    /// e só o hash é gravado (RN-US-03, RN-US-04). Login repetido → 400 com mensagem amigável (RN-US-02).
    /// </summary>
    public async Task<UsuarioDto> CriarAsync(CriarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var perfil = TextoEnum.Converter<PerfilUsuario>(request.Perfil, "Perfil");
        var login = Usuario.ValidarLogin(request.Login);
        PoliticaSenha.Validar(perfil, request.Senha);

        // O índice único em login segura a corrida (vira 409); aqui é a mensagem amigável.
        if (await _usuarios.ObterPorLoginAsync(login, cancellationToken) is not null)
        {
            throw new DomainException("Já existe um usuário com esse login.");
        }

        var usuario = Usuario.Criar(request.Nome, login, _hasher.Hash(request.Senha), perfil, _clock.UtcNow);
        await _usuarios.AdicionarAsync(usuario, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return usuario.ToDto();
    }

    /// <summary>RN-US-08: a dona redefine o PIN do atendente (ou a própria senha) sem precisar da antiga.</summary>
    public async Task<UsuarioDto> RedefinirSenhaAsync(Guid id, RedefinirSenhaRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await CarregarAsync(id, cancellationToken);
        PoliticaSenha.Validar(usuario.Perfil, request.NovaSenha);

        usuario.DefinirSenhaHash(_hasher.Hash(request.NovaSenha));
        await _uow.SaveChangesAsync(cancellationToken);

        return usuario.ToDto();
    }

    public async Task<UsuarioDto> AtivarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var usuario = await CarregarAsync(id, cancellationToken);
        usuario.Ativar();
        await _uow.SaveChangesAsync(cancellationToken);
        return usuario.ToDto();
    }

    /// <summary>
    /// RN-US-07: desativado não entra mais, histórico fica. RN-US-10: o Admin logado não pode desativar a si mesmo
    /// (a loja nunca fica sem ninguém que consiga entrar no painel).
    /// </summary>
    public async Task<UsuarioDto> DesativarAsync(Guid id, Guid usuarioLogadoId, CancellationToken cancellationToken = default)
    {
        if (id == usuarioLogadoId)
        {
            throw new DomainException("Você não pode desativar o seu próprio usuário. Peça para outro administrador.");
        }

        var usuario = await CarregarAsync(id, cancellationToken);
        usuario.Desativar();
        await _uow.SaveChangesAsync(cancellationToken);
        return usuario.ToDto();
    }

    private async Task<Usuario> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _usuarios.ObterPorIdAsync(id, cancellationToken)
        ?? throw new EntidadeNaoEncontradaException("Usuário", id);
}
