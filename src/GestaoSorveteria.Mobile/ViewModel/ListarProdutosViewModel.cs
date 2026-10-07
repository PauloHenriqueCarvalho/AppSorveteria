using System.Collections.ObjectModel;
using System.Windows.Input;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services;
using SorveteriaMaui.Services.Api;
using SorveteriaMaui.View;

namespace SorveteriaMaui.ViewModel;

/// <summary>
/// Aba Produtos (C2): catálogo baixado da API, só leitura — o cadastro é no painel da dona (B16 resolvido).
/// "Atualizar" (ou puxar a lista) baixa o que mudou; sem internet mostra o motivo e segue com o cache.
/// </summary>
public class ListarProdutosViewModel : BindableObject
{
    private readonly ProdutoRepository _produtos;
    private readonly CatalogoService _catalogo;
    private readonly SessaoUsuario _sessao;

    private bool _ocupado;
    private string _mensagem = string.Empty;
    private string _ultimaAtualizacao = string.Empty;
    private bool _semProdutos;

    public ListarProdutosViewModel(ProdutoRepository produtos, CatalogoService catalogo, SessaoUsuario sessao)
    {
        _produtos = produtos;
        _catalogo = catalogo;
        _sessao = sessao;
        // Sem canExecute: o RefreshView espelha CanExecute em IsEnabled e derrubaria IsRefreshing no meio; o botão usa Livre
        AtualizarCommand = new Command(async () => await AtualizarAsync());
    }

    public ObservableCollection<Produto> Produtos { get; } = new();

    public ICommand AtualizarCommand { get; }

    public bool Ocupado
    {
        get => _ocupado;
        set
        {
            _ocupado = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Livre));
        }
    }

    public bool Livre => !Ocupado;

    public string Mensagem
    {
        get => _mensagem;
        set { _mensagem = value; OnPropertyChanged(); OnPropertyChanged(nameof(TemMensagem)); }
    }

    public bool TemMensagem => !string.IsNullOrWhiteSpace(Mensagem);

    public string UltimaAtualizacao
    {
        get => _ultimaAtualizacao;
        set { _ultimaAtualizacao = value; OnPropertyChanged(); }
    }

    public bool SemProdutos
    {
        get => _semProdutos;
        set { _semProdutos = value; OnPropertyChanged(); }
    }

    /// <summary>Depois de renovar a sessão (401), a tela tenta a atualização de novo ao voltar.</summary>
    public bool TentarDeNovoAoVoltar { get; set; }

    public async Task CarregarProdutos()
    {
        await Operacao.CarregarAsync(async () =>
        {
            var lista = await _produtos.ListarAtivosAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                Produtos.Clear();
                foreach (var p in lista)
                {
                    Produtos.Add(p);
                }

                SemProdutos = Produtos.Count == 0;
                UltimaAtualizacao = _catalogo.UltimaAtualizacaoUtc is { } data
                    ? $"Catálogo atualizado em {data.ToLocalTime():dd/MM HH:mm} · {Produtos.Count} produto(s)"
                    : "Catálogo ainda não baixado neste aparelho";
            });
        });
    }

    /// <summary>Chamado ao voltar da tela de PIN: só tenta de novo se o login realmente aconteceu.</summary>
    public async Task TentarDeNovoSeRenovouAsync()
    {
        if (!TentarDeNovoAoVoltar)
        {
            return;
        }

        TentarDeNovoAoVoltar = false;
        if (await _sessao.TokenAsync() is not null)
        {
            await AtualizarAsync();
        }
    }

    public async Task AtualizarAsync()
    {
        if (Ocupado)
        {
            return;
        }

        Ocupado = true;
        Mensagem = "Baixando o catálogo… se o servidor estava dormindo, pode levar até 1 minuto.";
        try
        {
            var quantidade = await _catalogo.AtualizarAsync();
            Mensagem = quantidade == 0 ? "O catálogo já estava em dia." : $"{quantidade} produto(s) atualizado(s).";
            await CarregarProdutos();
        }
        catch (SessaoExpiradaException ex)
        {
            Mensagem = ex.Message;
            TentarDeNovoAoVoltar = true;
            await LoginView.PedirPinDeNovoAsync(); // RN-US-06: PIN de novo sem trocar o atendente
        }
        catch (ApiException ex)
        {
            Mensagem = ex.Message; // sem internet / servidor dormindo: a venda segue com o cache
        }
        catch (Exception ex)
        {
            // B7: falha ao gravar o catálogo no aparelho não fica em silêncio
            Mensagem = $"Não foi possível gravar o catálogo no aparelho. Detalhe: {ex.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }
}
