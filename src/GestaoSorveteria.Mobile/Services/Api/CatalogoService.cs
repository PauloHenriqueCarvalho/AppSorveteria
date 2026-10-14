using System.Diagnostics;
using GestaoSorveteria.Contracts.Produtos;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;

namespace SorveteriaMaui.Services.Api;

/// <summary>
/// Catálogo vindo da API (C2, RN-PR-03): <c>GET /api/produtos?desde=</c> ao abrir o app e quando o atendente pede.
/// O app guarda o <c>GeradoEmUtc</c> da última resposta e manda de volta em <c>desde</c>, baixando só o que mudou.
/// Sem internet a venda usa o cache do SQLite; o catálogo nunca trava a venda.
/// </summary>
public sealed class CatalogoService(ApiCliente api, ProdutoRepository produtos, ConfiguracaoApi configuracao)
{
    private const string ChaveGeradoEm = "catalogo_gerado_em_ticks";
    private const string ChaveUrl = "catalogo_url"; // de qual servidor veio o carimbo

    private readonly SemaphoreSlim _umaPorVez = new(1, 1);
    private bool _tentouAoAbrir;

    /// <summary>Carimbo do servidor da última carga; <c>null</c> enquanto o aparelho nunca baixou o catálogo.</summary>
    public DateTime? UltimaAtualizacaoUtc
    {
        get
        {
            var ticks = Preferences.Default.Get(ChaveGeradoEm, 0L);
            return ticks == 0 ? null : new DateTime(ticks, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Baixa e grava o catálogo. Devolve quantos produtos vieram. A primeira carga substitui o cache inteiro
    /// (fim dos produtos do seed/cadastro local, B16); as seguintes só gravam o que mudou.
    /// Falhas saem como <see cref="ApiException"/> (mensagem pronta para a tela).
    /// </summary>
    public async Task<int> AtualizarAsync(CancellationToken cancellationToken = default)
    {
        await _umaPorVez.WaitAsync(cancellationToken);
        try
        {
            // Carga cheia quando nunca baixou, quando o cache está vazio (banco novo) ou quando o servidor mudou
            // (URL trocada em Debug / APK apontando para outro lugar): um "desde" de outro servidor deixaria o catálogo
            // incompleto em silêncio. Senão, incremental.
            var cargaCheia = UltimaAtualizacaoUtc is null
                || Preferences.Default.Get(ChaveUrl, string.Empty) != configuracao.UrlBase
                || await produtos.ContarAsync() == 0;
            var desde = cargaCheia ? null : UltimaAtualizacaoUtc;

            var catalogo = await api.ObterCatalogoAsync(desde, cancellationToken);
            var linhas = catalogo.Produtos.Select(ParaLinha).ToList();

            if (cargaCheia)
            {
                await produtos.SubstituirTudoAsync(linhas);
            }
            else
            {
                await produtos.GravarAsync(linhas);
            }

            Preferences.Default.Set(ChaveGeradoEm, DateTime.SpecifyKind(catalogo.GeradoEmUtc, DateTimeKind.Utc).Ticks);
            Preferences.Default.Set(ChaveUrl, configuracao.UrlBase);
            return linhas.Count;
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    /// <summary>C4: no ciclo de 1 min, atualiza o catálogo no máximo a cada <paramref name="idade"/> (preço mudado no painel chega ao balcão).</summary>
    public async Task AtualizarSeAntigoAsync(TimeSpan idade)
    {
        var ultima = UltimaAtualizacaoUtc;
        if (ultima is not null && DateTime.UtcNow - ultima < idade)
        {
            return;
        }

        try
        {
            await AtualizarAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Catálogo não atualizado no ciclo: {ex.Message}");
        }
    }

    /// <summary>Volta a permitir a atualização automática (chamado quando o app volta do segundo plano).</summary>
    public void PermitirNovaAtualizacaoAoAbrir() => _tentouAoAbrir = false;

    /// <summary>
    /// Ao abrir o app (e ao voltar do segundo plano), uma vez por vez e sem segurar a tela. Sem conexão fica quieto
    /// (o cache serve para vender); sessão vencida ou outro erro aparece como aviso rápido, e a aba Produtos tem o
    /// botão "Atualizar" para tentar de novo. O C4 traz o ciclo de 1 min, que também passa a atualizar o catálogo.
    /// </summary>
    public async Task AtualizarAoAbrirAsync()
    {
        if (_tentouAoAbrir)
        {
            return;
        }

        _tentouAoAbrir = true;
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            Debug.WriteLine("[Sorveteria] Sem internet ao abrir: catálogo do aparelho.");
            return;
        }

        try
        {
            var quantidade = await AtualizarAsync();
            if (quantidade > 0)
            {
                AvisoRapido.Mostrar($"Catálogo atualizado: {quantidade} produto(s)");
            }
        }
        catch (SessaoExpiradaException)
        {
            AvisoRapido.Mostrar("Sessão vencida: na aba Produtos, toque em Atualizar para entrar de novo");
        }
        catch (ApiIndisponivelException ex)
        {
            Debug.WriteLine($"[Sorveteria] Catálogo não atualizado ao abrir: {ex.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Sorveteria] Catálogo não atualizado ao abrir: {ex}");
            AvisoRapido.Mostrar($"Catálogo não atualizado: {ex.Message}");
        }
    }

    private static Produto ParaLinha(ProdutoDto dto) => new()
    {
        Id = dto.Id,
        Nome = dto.Nome,
        Categoria = dto.Categoria,
        Preco = dto.Preco,
        PermiteValorLivre = dto.PermiteValorLivre,
        Ativo = dto.Ativo,
        Ordem = dto.Ordem,
        CriadoEm = dto.AtualizadoEmUtc, // a API não manda a criação; o app só usa a data para remontar o Produto do Domain
        AtualizadoEm = dto.AtualizadoEmUtc,
    };
}
