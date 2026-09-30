namespace GestaoSorveteria.Infrastructure.Seed;

/// <summary>RN-US-09: dados do primeiro Admin, lidos da seção "Seed" da configuração.</summary>
public sealed class SeedOptions
{
    public const string Secao = "Seed";

    public string AdminNome { get; set; } = "Administrador";
    public string AdminLogin { get; set; } = "admin";
    public string AdminSenha { get; set; } = string.Empty;
}
