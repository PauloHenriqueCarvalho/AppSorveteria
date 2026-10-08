using System.Windows.Input;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Usuarios;
using SorveteriaMaui.Services.Api;
using SorveteriaMaui.View;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Login com PIN (RN-US-03). Exige internet só aqui: depois o token fica no aparelho e a venda segue offline.
/// Dois modos: <b>entrar</b> (sem atendente no aparelho, tela inicial) e <b>renovar</b> (token vencido ou recusado,
/// RN-US-06: aberta por cima da tela atual, mantém o atendente e volta para onde estava — docs/03, seção 7).
/// Enquanto não existem os endpoints de usuários, testa-se com o Admin do seed (login <c>admin</c>, senha <c>admin12345</c>).
/// </summary>
[QueryProperty(nameof(Renovar), "renovar")]
public class LoginViewModel : BindableObject
{
    private readonly ApiCliente _api;
    private readonly SessaoUsuario _sessao;
    private readonly ConfiguracaoApi _configuracao;

    private string _login = string.Empty;
    private string _pin = string.Empty;
    private string _urlApi = string.Empty;
    private string _status = string.Empty;
    private string _erro = string.Empty;
    private bool _ocupado;
    private bool _mostrarServidor;
    private bool _renovar;

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
        VoltarCommand = new Command(async () => await Shell.Current.GoToAsync(".."), () => !Ocupado);
        AlternarServidorCommand = new Command(() => MostrarServidor = !MostrarServidor);
    }

    /// <summary>Modo renovar sessão (<c>RenovarSessaoView?renovar=true</c>): mantém o atendente e volta ao terminar.</summary>
    public bool Renovar
    {
        get => _renovar;
        set
        {
            _renovar = value;
            if (value && !string.IsNullOrEmpty(_sessao.Login))
            {
                Login = _sessao.Login;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(Subtitulo));
        }
    }

    public string Subtitulo => Renovar
        ? "Sua sessão venceu. Digite o PIN de novo para continuar."
        : "Entre com o seu PIN para começar o turno";

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

    /// <summary>Andamento ("Conectando…"), em cinza.</summary>
    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemStatus)); }
    }

    public bool TemStatus => !string.IsNullOrWhiteSpace(Status);

    /// <summary>Falha, em vermelho.</summary>
    public string Erro
    {
        get => _erro;
        set { _erro = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemErro)); }
    }

    public bool TemErro => !string.IsNullOrWhiteSpace(Erro);

    public bool Ocupado
    {
        get => _ocupado;
        set
        {
            _ocupado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Livre));
            foreach (var comando in new[] { DigitarCommand, ApagarCommand, LimparCommand, EntrarCommand, VoltarCommand })
            {
                ((Command)comando).ChangeCanExecute();
            }
        }
    }

    public bool Livre => !Ocupado;

    /// <summary>Campo "Servidor" só existe em Debug; no Release a URL é a do build.</summary>
    public bool UrlEditavel => ConfiguracaoApi.UrlEditavel;

    public bool MostrarServidor
    {
        get => _mostrarServidor;
        set { _mostrarServidor = value; OnPropertyChanged(); }
    }

    public ICommand DigitarCommand { get; }
    public ICommand ApagarCommand { get; }
    public ICommand LimparCommand { get; }
    public ICommand EntrarCommand { get; }
    public ICommand VoltarCommand { get; }
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
        Erro = string.Empty;

        string login;
        try
        {
            login = Usuario.ValidarLogin(Login); // RN-US-02: mesma regra (e mensagem) da API
        }
        catch (DomainException ex)
        {
            Erro = ex.Message;
            return;
        }

        if (string.IsNullOrWhiteSpace(Pin))
        {
            Erro = "Digite o PIN.";
            return;
        }

        if (UrlEditavel)
        {
            if (!_configuracao.TentarSalvar(UrlApi))
            {
                Erro = "Endereço do servidor inválido. Use o formato http://ip:porta ou https://endereco.";
                MostrarServidor = true;
                return;
            }
        }
        else if (!_configuracao.EstaConfigurada)
        {
            // APK Release gerado sem -p:ApiUrlPadrao (docs/07)
            Erro = "Este app foi instalado sem o endereço do servidor. Peça ao responsável uma versão atualizada.";
            return;
        }

        Login = login;
        Ocupado = true;
        Status = "Conectando ao servidor… se ele estava dormindo, pode levar até 1 minuto.";
        try
        {
            var resposta = await _api.LoginAsync(login, Pin);
            await _sessao.IniciarAsync(resposta);
            Pin = string.Empty;
            await (Renovar
                ? Shell.Current.GoToAsync("..")
                : Shell.Current.GoToAsync($"//{nameof(ListarComandasView)}"));
        }
        catch (ApiException ex)
        {
            Erro = ex.Message;
            if (ex.Status == 401)
            {
                Pin = string.Empty;
            }
        }
        catch (Exception ex)
        {
            // Falha ao guardar o token ou navegar: não pode ficar em silêncio (B7)
            Erro = $"Não foi possível concluir o login. Detalhe: {ex.Message}";
        }
        finally
        {
            Status = string.Empty;
            Ocupado = false;
        }
    }
}
