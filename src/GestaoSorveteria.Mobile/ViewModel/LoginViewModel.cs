using System.Windows.Input;
using SorveteriaMaui.Services.Api;
using SorveteriaMaui.View;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Login com PIN (RN-US-03). Exige internet só aqui: depois o token fica no aparelho e a venda segue offline.
/// Enquanto não existem os endpoints de usuários, testa-se com o Admin do seed (login <c>admin</c>, senha <c>admin12345</c>).
/// </summary>
public class LoginViewModel : BindableObject
{
    private readonly ApiCliente _api;
    private readonly SessaoUsuario _sessao;
    private readonly ConfiguracaoApi _configuracao;

    private string _login = string.Empty;
    private string _pin = string.Empty;
    private string _urlApi = string.Empty;
    private string _mensagem = string.Empty;
    private bool _ocupado;
    private bool _mostrarServidor;

    public LoginViewModel(ApiCliente api, SessaoUsuario sessao, ConfiguracaoApi configuracao)
    {
        _api = api;
        _sessao = sessao;
        _configuracao = configuracao;

        _login = sessao.UltimoLogin;
        _urlApi = configuracao.UrlBase;

        DigitarCommand = new Command<string>(Digitar, _ => !Ocupado);
        ApagarCommand = new Command(() => Pin = Pin.Length == 0 ? Pin : Pin[..^1], () => !Ocupado);
        LimparCommand = new Command(() => Pin = string.Empty, () => !Ocupado);
        EntrarCommand = new Command(async () => await EntrarAsync(), () => !Ocupado);
        AlternarServidorCommand = new Command(() => MostrarServidor = !MostrarServidor);
    }

    public string Login
    {
        get => _login;
        set { _login = value; OnPropertyChanged(); }
    }

    public string Pin
    {
        get => _pin;
        set { _pin = value; OnPropertyChanged(); }
    }

    /// <summary>Endereço da API (editável para apontar ao PC na rede local ou ao Render).</summary>
    public string UrlApi
    {
        get => _urlApi;
        set { _urlApi = value; OnPropertyChanged(); }
    }

    public string Mensagem
    {
        get => _mensagem;
        set { _mensagem = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemMensagem)); }
    }

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    public bool Ocupado
    {
        get => _ocupado;
        set
        {
            _ocupado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Livre));
            foreach (var comando in new[] { DigitarCommand, ApagarCommand, LimparCommand, EntrarCommand })
            {
                ((Command)comando).ChangeCanExecute();
            }
        }
    }

    public bool Livre => !Ocupado;

    public bool MostrarServidor
    {
        get => _mostrarServidor;
        set { _mostrarServidor = value; OnPropertyChanged(); }
    }

    public ICommand DigitarCommand { get; }
    public ICommand ApagarCommand { get; }
    public ICommand LimparCommand { get; }
    public ICommand EntrarCommand { get; }
    public ICommand AlternarServidorCommand { get; }

    private void Digitar(string digito)
    {
        if (Pin.Length < 100) // mesmo limite do LoginRequest
        {
            Pin += digito;
        }
    }

    private async Task EntrarAsync()
    {
        var login = Login.Trim().ToLowerInvariant(); // RN-US-02: login é minúsculo
        if (login.Length < 3)
        {
            Mensagem = "Informe o seu login (pelo menos 3 letras).";
            return;
        }

        if (string.IsNullOrWhiteSpace(Pin))
        {
            Mensagem = "Digite o PIN.";
            return;
        }

        if (!_configuracao.TentarSalvar(UrlApi))
        {
            Mensagem = "Endereço do servidor inválido. Use o formato http://ip:porta ou https://endereco.";
            MostrarServidor = true;
            return;
        }

        Login = login;
        Ocupado = true;
        Mensagem = "Conectando ao servidor… se ele estava dormindo, pode levar até 1 minuto.";
        try
        {
            var resposta = await _api.LoginAsync(login, Pin);
            await _sessao.IniciarAsync(resposta);
            Pin = string.Empty;
            Mensagem = string.Empty;
            await Shell.Current.GoToAsync($"//{nameof(ListarComandasView)}");
        }
        catch (ApiException ex)
        {
            Mensagem = ex.Message;
            if (ex.Status == 401)
            {
                Pin = string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Falha ao guardar o token ou navegar: não pode ficar em silêncio (B7)
            Mensagem = $"Não foi possível concluir o login. Detalhe: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
