using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Produtos;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Tests.Domain;

/// <summary>Dados de apoio: um instante fixo em UTC e fábricas de entidades válidas.</summary>
internal static class Cenario
{
    public static readonly DateTime Agora = new(2026, 9, 17, 15, 0, 0, DateTimeKind.Utc);
    public static readonly Guid Atendente = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Dona = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static Caixa CaixaAberto(decimal fundoTroco = 100m) => Caixa.Abrir(Atendente, fundoTroco, Agora);

    public static Produto Picole(decimal preco = 5m) => Produto.Criar("Picolé de morango", "Picolés", preco, false, 1, Agora);

    public static Produto MilkShake(decimal preco = 18.90m) => Produto.Criar("Milk-shake 500ml", "Milk-shakes", preco, false, 2, Agora);

    public static Produto SelfService() => Produto.Criar("Self-service (kg)", "Self-service", 0m, true, 3, Agora);

    public static Comanda ComandaAberta(Caixa? caixa = null, int numero = 1, TipoComanda tipo = TipoComanda.Balcao) =>
        Comanda.Abrir(caixa ?? CaixaAberto(), Atendente, numero, tipo, Agora, Agora);

    public static Usuario Admin() => Usuario.Criar("Dona Maria", "maria", "hash", PerfilUsuario.Admin, Agora);
}
