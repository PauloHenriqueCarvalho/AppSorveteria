using GestaoSorveteria.Domain.Caixas;

namespace SorveteriaMaui.Services;

/// <summary>
/// Caixa provisório até a tela de abrir caixa (Etapa C, item C3).
/// Decisão do Paulo (30/09/2026): o app abre sozinho um caixa local com fundo R$ 0, só para a comanda
/// nascer num caixa aberto (RN-CX-03). Não é sincronizado; o C3 substitui esta classe.
/// Quem abriu é o atendente logado (C1) — o "usuário do aparelho" fixo deixou de existir.
/// </summary>
public sealed class CaixaProvisorio
{
    private const string ChaveCaixa = "caixa_provisorio_id";
    private const string ChaveAbertoEm = "caixa_provisorio_aberto_em_ticks";

    public Caixa Atual(Guid usuarioId)
    {
        var abertoEmTicks = Preferences.Default.Get(ChaveAbertoEm, 0L);
        if (abertoEmTicks == 0)
        {
            abertoEmTicks = DateTime.UtcNow.Ticks;
            Preferences.Default.Set(ChaveAbertoEm, abertoEmTicks);
        }

        return Caixa.Abrir(usuarioId, 0m, new DateTime(abertoEmTicks, DateTimeKind.Utc), ObterOuCriar(ChaveCaixa));
    }

    private static Guid ObterOuCriar(string chave)
    {
        if (Guid.TryParse(Preferences.Default.Get(chave, string.Empty), out var id))
        {
            return id;
        }

        id = Guid.NewGuid();
        Preferences.Default.Set(chave, id.ToString());
        return id;
    }
}
