using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Caixas;

/// <summary>Sangria ou suprimento registrado num caixa aberto. Criado somente pelo <see cref="Caixa"/>.</summary>
public sealed class MovimentoCaixa : Entity
{
    public Guid CaixaId { get; private set; }
    public TipoMovimentoCaixa Tipo { get; private set; }
    public decimal Valor { get; private set; }
    public string Motivo { get; private set; }
    public Guid UsuarioId { get; private set; }
    public DateTime Em { get; private set; }

    // EF Core
    private MovimentoCaixa()
    {
        Motivo = string.Empty;
    }

    internal MovimentoCaixa(Guid caixaId, TipoMovimentoCaixa tipo, decimal valor, string motivo, Guid usuarioId, DateTime emUtc, Guid? id)
        : base(id)
    {
        CaixaId = caixaId;
        Tipo = tipo;
        Valor = Guard.Dinheiro(valor, "o valor", permiteZero: false);
        Motivo = Guard.Texto(motivo, "o motivo", 2, 200);
        UsuarioId = Guard.NaoVazio(usuarioId, "o usuário");
        Em = Guard.Utc(emUtc, "a data");
    }
}
