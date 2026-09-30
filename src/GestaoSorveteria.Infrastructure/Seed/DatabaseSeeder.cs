using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestaoSorveteria.Infrastructure.Seed;

/// <summary>RN-US-09: cria o Admin inicial se a tabela de usuários estiver vazia.</summary>
public sealed class DatabaseSeeder
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;
    private readonly SeedOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IUsuarioRepository usuarios,
        IUnitOfWork unitOfWork,
        IPasswordHasher hasher,
        IClock clock,
        IOptions<SeedOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _usuarios = usuarios;
        _unitOfWork = unitOfWork;
        _hasher = hasher;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _usuarios.ExisteAlgumAsync(cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.AdminSenha))
        {
            _logger.LogWarning("Seed:AdminSenha não configurada — nenhum usuário foi criado. Configure e reinicie.");
            return;
        }

        PoliticaSenha.Validar(PerfilUsuario.Admin, _options.AdminSenha);

        var admin = Usuario.Criar(
            _options.AdminNome,
            _options.AdminLogin,
            _hasher.Hash(_options.AdminSenha),
            PerfilUsuario.Admin,
            _clock.UtcNow);

        await _usuarios.AdicionarAsync(admin, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Usuário administrador '{Login}' criado (RN-US-09). Troque a senha após o primeiro login.", admin.Login);
    }
}
