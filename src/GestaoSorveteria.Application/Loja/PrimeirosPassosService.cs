using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Contracts.Loja;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Repositories;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Loja;

/// <summary>
/// RN-LJ-01: a lista de "primeiros passos" do painel — catálogo, cardápio, atendentes, WhatsApp da dona, primeira contagem e
/// primeira venda do tablet. Só leitura; cada passo é "já tem pelo menos um".
/// </summary>
public sealed class PrimeirosPassosService(
    IEstoqueRepository estoque,
    IProdutoRepository produtos,
    IUsuarioRepository usuarios,
    IConfiguracaoLojaRepository configuracao,
    IContagemRepository contagens,
    IComandaRepository comandas,
    IClock clock)
{
    private static readonly DateTime Inicio = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public async Task<PrimeirosPassosDto> ObterAsync(CancellationToken cancellationToken = default)
    {
        var agora = clock.UtcNow;
        var (_, vendas) = await comandas.ListarPorPeriodoAsync(Inicio, agora.AddDays(1), new FiltroVendas(), pular: 0, quantidade: 1, cancellationToken);

        return new PrimeirosPassosDto(
            (await estoque.ListarItensAsync(somenteAtivos: true, cancellationToken)).Count > 0,
            (await produtos.ListarAsync(somenteAtivos: true, cancellationToken)).Count > 0,
            (await usuarios.ListarAsync(cancellationToken)).Any(u => u.Ativo && u.Perfil == PerfilUsuario.Atendente),
            !string.IsNullOrWhiteSpace((await configuracao.ObterAsync(cancellationToken))?.WhatsAppDona),
            (await contagens.UltimaContagemPorItemAsync(agora, cancellationToken)).Count > 0,
            vendas > 0);
    }
}
