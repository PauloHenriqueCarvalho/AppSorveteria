using GestaoSorveteria.Domain.Caixas;

namespace SorveteriaMaui.Services;

/// <summary>
/// Caixa e usuário provisórios até a Etapa C (tela de abrir caixa e login com PIN).
/// Decisão do Paulo (30/09/2026): o app abre sozinho um caixa local com fundo R$ 0 e um
/// "usuário do aparelho" fixo, só para a comanda nascer num caixa aberto (RN-CX-03).
/// Esses dados não são sincronizados; a Etapa C substitui esta classe.
/// </summary>
public sealed class CaixaProvisorio
{
    private const string ChaveCaixa = "caixa_provisorio_id";
    private const string ChaveUsuario = "usuario_aparelho_id";
    private const string ChaveAbertoEm = "caixa_provisorio_aberto_em_ticks";

    public Guid UsuarioId => ObterOuCriar(ChaveUsuario);

    public Caixa Atual()
    {
        var abertoEmTicks = Preferences.Default.Get(ChaveAbertoEm, 0L);
        if (abertoEmTicks == 0)
        {
            abertoEmTicks = DateTime.UtcNow.Ticks;
            Preferences.Default.Set(ChaveAbertoEm, abertoEmTicks);
        }

        return Caixa.Abrir(UsuarioId, 0m, new DateTime(abertoEmTicks, DateTimeKind.Utc), ObterOuCriar(ChaveCaixa));
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
