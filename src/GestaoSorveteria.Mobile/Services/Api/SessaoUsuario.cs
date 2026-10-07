using System.Diagnostics;
using GestaoSorveteria.Contracts.Auth;

namespace SorveteriaMaui.Services.Api;

/// <summary>
/// Quem está usando o aparelho. O token JWT fica no <see cref="SecureStorage"/>; os dados do usuário
/// (id, nome, perfil, validade do token) ficam em <see cref="Preferences"/> para a tela decidir sem esperar I/O.
/// Token vencido (RN-US-06) ou perdido <b>não</b> bloqueia a venda: o app segue com o usuário conhecido e
/// pede o PIN de novo quando precisar falar com a API (docs/03, seção 7).
/// </summary>
public sealed class SessaoUsuario
{
    private const string ChaveToken = "api_token";
    private const string ChaveUsuarioId = "sessao_usuario_id";
    private const string ChaveNome = "sessao_usuario_nome";
    private const string ChaveLogin = "sessao_usuario_login";
    private const string ChavePerfil = "sessao_usuario_perfil";
    private const string ChaveExpiraEm = "sessao_token_expira_em_ticks";
    private const string ChaveUltimoLogin = "sessao_ultimo_login";

    private readonly SemaphoreSlim _umaPorVez = new(1, 1);
    private string? _token;
    private bool _tokenCarregado;

    public SessaoUsuario()
    {
        UsuarioId = Guid.TryParse(Preferences.Default.Get(ChaveUsuarioId, string.Empty), out var id) ? id : null;
        Nome = Preferences.Default.Get(ChaveNome, string.Empty);
        Login = Preferences.Default.Get(ChaveLogin, string.Empty);
        Perfil = Preferences.Default.Get(ChavePerfil, string.Empty);
        var ticks = Preferences.Default.Get(ChaveExpiraEm, 0L);
        TokenExpiraEmUtc = ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
    }

    public Guid? UsuarioId { get; private set; }
    public string Nome { get; private set; }
    public string Login { get; private set; }
    public string Perfil { get; private set; }
    public DateTime? TokenExpiraEmUtc { get; private set; }

    /// <summary>Alguém já entrou neste aparelho (mesmo que o token tenha vencido).</summary>
    public bool TemUsuario => UsuarioId is not null;

    /// <summary>Último login digitado, para não precisar digitar de novo.</summary>
    public string UltimoLogin => Preferences.Default.Get(ChaveUltimoLogin, string.Empty);

    /// <summary>Token para o <c>Authorization: Bearer</c>, ou <c>null</c> se não há token ou ele venceu.</summary>
    public async Task<string?> TokenAsync()
    {
        if (!_tokenCarregado)
        {
            await _umaPorVez.WaitAsync();
            try
            {
                if (!_tokenCarregado)
                {
                    _token = await LerTokenAsync();
                    _tokenCarregado = true;
                }
            }
            finally
            {
                _umaPorVez.Release();
            }
        }

        return _token is not null && TokenExpiraEmUtc > DateTime.UtcNow ? _token : null;
    }

    /// <summary>Guarda o resultado do login (token + usuário).</summary>
    public async Task IniciarAsync(LoginResponse resposta)
    {
        await _umaPorVez.WaitAsync();
        try
        {
            await SecureStorage.Default.SetAsync(ChaveToken, resposta.Token);
            _token = resposta.Token;
            _tokenCarregado = true;

            UsuarioId = resposta.Usuario.Id;
            Nome = resposta.Usuario.Nome;
            Login = resposta.Usuario.Login;
            Perfil = resposta.Usuario.Perfil;
            TokenExpiraEmUtc = DateTime.SpecifyKind(resposta.ExpiraEmUtc, DateTimeKind.Utc);

            Preferences.Default.Set(ChaveUsuarioId, UsuarioId.Value.ToString());
            Preferences.Default.Set(ChaveNome, Nome);
            Preferences.Default.Set(ChaveLogin, Login);
            Preferences.Default.Set(ChavePerfil, Perfil);
            Preferences.Default.Set(ChaveExpiraEm, TokenExpiraEmUtc.Value.Ticks);
            Preferences.Default.Set(ChaveUltimoLogin, Login);
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    /// <summary>Sai do aparelho (trocar de atendente). O próximo login exige internet.</summary>
    public async Task EncerrarAsync()
    {
        await _umaPorVez.WaitAsync();
        try
        {
            SecureStorage.Default.Remove(ChaveToken);
            _token = null;
            _tokenCarregado = true;

            UsuarioId = null;
            Nome = Login = Perfil = string.Empty;
            TokenExpiraEmUtc = null;

            Preferences.Default.Remove(ChaveUsuarioId);
            Preferences.Default.Remove(ChaveNome);
            Preferences.Default.Remove(ChaveLogin);
            Preferences.Default.Remove(ChavePerfil);
            Preferences.Default.Remove(ChaveExpiraEm);
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    private static async Task<string?> LerTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(ChaveToken);
        }
        catch (Exception ex)
        {
            // Keystore do aparelho indisponível ou corrompido: vale como "sem token" (pede o PIN quando houver internet).
            Debug.WriteLine($"[Sorveteria] Não foi possível ler o token: {ex.Message}");
            return null;
        }
    }
}
