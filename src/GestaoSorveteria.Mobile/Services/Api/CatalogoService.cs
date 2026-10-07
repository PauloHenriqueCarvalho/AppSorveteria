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
public sealed class CatalogoService(ApiCliente api, ProdutoRepository produtos)
{
    private const string ChaveGeradoEm = "catalogo_gerado_em_ticks";

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
            var desde = UltimaAtualizacaoUtc;
            var catalogo = await api.ObterCatalogoAsync(desde, cancellationToken);
            var linhas = catalogo.Produtos.Select(ParaLinha).ToList();

            if (desde is null)
            {
                await produtos.SubstituirTudoAsync(linhas);
            }
            else
            {
                await produtos.GravarAsync(linhas);
            }

            Preferences.Default.Set(ChaveGeradoEm, DateTime.SpecifyKind(catalogo.GeradoEmUtc, DateTimeKind.Utc).Ticks);
            return linhas.Count;
        }
        finally
        {
            _umaPorVez.Release();
        }
    }

    /// <summary>
    /// Ao abrir o app, uma vez por execução e em segundo plano. Sem conexão fica quieto (o cache serve para vender);
    /// sessão vencida ou outro erro aparece como aviso rápido, e a aba Produtos tem o botão "Atualizar" para tentar de novo.
    /// </summary>
    public async Task AtualizarAoAbrirAsync()
    {
        if (_tentouAoAbrir)
        {
            return;
        }

        _tentouAoAbrir = true;
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
