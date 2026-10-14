using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// Grupo de itens como na planilha da dona (Picolé de fruta, Pote 1L, Massa, Descartáveis...).
/// O modo de controle é do grupo (RN-ES-02).
/// </summary>
public sealed class GrupoEstoque : Entity
{
    public string Nome { get; private set; }
    public string NomeNormalizado { get; private set; }
    public ModoControle ModoControle { get; private set; }

    /// <summary>Ordem das seções na contagem e na folha do pedido (a mesma da planilha).</summary>
    public int Ordem { get; private set; }

    // EF Core
    private GrupoEstoque()
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
    }

    private GrupoEstoque(Guid? id) : base(id)
    {
        Nome = string.Empty;
        NomeNormalizado = string.Empty;
    }

    public static GrupoEstoque Criar(string nome, ModoControle modo, int ordem, Guid? id = null)
    {
        var grupo = new GrupoEstoque(id);
        grupo.Renomear(nome);
        grupo.AlterarModo(modo);
        grupo.Ordem = ordem;
        return grupo;
    }

    public void Renomear(string nome)
    {
        Nome = NomeEstoque.Limpar(Guard.Texto(nome, "o nome do grupo", 2, 40));
        NomeNormalizado = NomeEstoque.Normalizar(Nome);
    }

    /// <summary>RN-ES-02: a dona pode trocar (ex.: potes voltam para contagem se o teste não der certo).</summary>
    public void AlterarModo(ModoControle modo)
    {
        Guard.Contra(!Enum.IsDefined(modo), "Modo de controle inválido.");
        ModoControle = modo;
    }

    public void AlterarOrdem(int ordem) => Ordem = ordem;

    /// <summary>Entra na contagem semanal (RN-ES-05)? Insumos (<see cref="ModoControle.SoAviso"/>) não.</summary>
    public bool EntraNaContagem => ModoControle != ModoControle.SoAviso;
}
