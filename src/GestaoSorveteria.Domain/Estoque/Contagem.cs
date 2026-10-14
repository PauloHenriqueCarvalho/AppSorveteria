using GestaoSorveteria.Domain.Common;

namespace GestaoSorveteria.Domain.Estoque;

/// <summary>
/// RN-ES-05/06: contagem semanal do freezer (atendente ou dona, tablet ou painel). Pode ser salva parcial;
/// só vale depois de concluída, e então não muda mais. Item não contado fica "não contado" (nunca vira 0).
/// </summary>
public sealed class Contagem : Entity
{
    private readonly List<ItemContagem> _itens = new();

    public Guid UsuarioId { get; private set; }
    public DateTime IniciadaEm { get; private set; }
    public DateTime? ConcluidaEm { get; private set; }
    public OrigemContagem Origem { get; private set; }

    public IReadOnlyCollection<ItemContagem> Itens => _itens.AsReadOnly();

    public bool Concluida => ConcluidaEm is not null;

    // EF Core
    private Contagem()
    {
    }

    private Contagem(Guid? id) : base(id)
    {
    }

    public static Contagem Iniciar(Guid usuarioId, DateTime agoraUtc, OrigemContagem origem, Guid? id = null)
    {
        Guard.Contra(!Enum.IsDefined(origem), "Origem da contagem inválida.");
        return new Contagem(id)
        {
            UsuarioId = Guard.NaoVazio(usuarioId, "quem contou"),
            IniciadaEm = Guard.Utc(agoraUtc, "a data da contagem"),
            Origem = origem,
        };
    }

    /// <summary>
    /// Registra (ou corrige) o que foi contado de um item, em embalagens ("2,5" caixas; "Não tem nenhum" = 0).
    /// <paramref name="embalagens"/> nulo = volta a "não contado". <paramref name="saldoSistemaUnidades"/>: potes, para a conferência (RN-ES-08).
    /// <paramref name="contadoEmUtc"/>: quando o item foi olhado no freezer (a contagem pode levar horas ou ser concluída no dia
    /// seguinte); é a base do saldo dos potes. Sem ela, vale a conclusão da contagem.
    /// </summary>
    public ItemContagem Registrar(
        ItemEstoque item, GrupoEstoque grupo, decimal? embalagens, int? saldoSistemaUnidades = null, Guid? linhaId = null, DateTime? contadoEmUtc = null)
    {
        ExigirAberta();
        Guard.Contra(item.GrupoId != grupo.Id, "O item não pertence a este grupo.");
        Guard.Contra(!grupo.EntraNaContagem, $"{grupo.Nome} não entra na contagem: use o aviso \"Está acabando\".");

        int? unidades = embalagens is { } valor ? item.ParaUnidades(valor) : null;
        DateTime? contadoEm = contadoEmUtc is { } quando ? Guard.Utc(quando, "quando o item foi contado") : null;
        Guard.Contra(contadoEm < IniciadaEm, "O item não pode ter sido contado antes do início da contagem.");
        var existente = _itens.FirstOrDefault(i => i.ItemEstoqueId == item.Id);
        if (existente is not null)
        {
            existente.Alterar(unidades, saldoSistemaUnidades, contadoEm);
            return existente;
        }

        var linha = new ItemContagem(Id, item.Id, unidades, saldoSistemaUnidades, linhaId, unidades is null ? null : contadoEm);
        _itens.Add(linha);
        return linha;
    }

    /// <summary>Conclui: precisa de pelo menos um item contado. Depois disso a contagem não muda (correção = nova contagem).</summary>
    public void Concluir(DateTime agoraUtc)
    {
        ExigirAberta();
        var agora = Guard.Utc(agoraUtc, "a data de conclusão");
        Guard.Contra(agora < IniciadaEm, "A conclusão não pode ser anterior ao início da contagem.");
        Guard.Contra(_itens.All(i => i.ContadoUnidades is null), "Conte pelo menos um item antes de concluir.");
        ConcluidaEm = agora;
    }

    private void ExigirAberta() => Guard.Contra(Concluida, "Esta contagem já foi concluída e não muda mais. Faça uma nova contagem.");
}

/// <summary>Um item na contagem: quanto foi contado (na menor unidade) e, nos potes, o saldo que o sistema esperava.</summary>
public sealed class ItemContagem : Entity
{
    public Guid ContagemId { get; private set; }
    public Guid ItemEstoqueId { get; private set; }

    /// <summary>Nulo = não contado (RN-ES-06).</summary>
    public int? ContadoUnidades { get; private set; }

    /// <summary>Potes: saldo do sistema no momento da contagem (RN-ES-08).</summary>
    public int? SaldoSistemaUnidades { get; private set; }

    /// <summary>Quando o item foi contado (base do saldo dos potes). Nulo = não informado: vale a conclusão da contagem.</summary>
    public DateTime? ContadoEm { get; private set; }

    /// <summary>Contado − saldo do sistema (potes). Diferença maior que 1 vai no resumo da dona.</summary>
    public int? Diferenca => ContadoUnidades is { } contado && SaldoSistemaUnidades is { } saldo ? contado - saldo : null;

    // EF Core
    private ItemContagem()
    {
    }

    internal ItemContagem(Guid contagemId, Guid itemEstoqueId, int? contadoUnidades, int? saldoSistemaUnidades, Guid? id, DateTime? contadoEm = null)
        : base(id)
    {
        ContagemId = contagemId;
        ItemEstoqueId = itemEstoqueId;
        ContadoUnidades = contadoUnidades;
        SaldoSistemaUnidades = saldoSistemaUnidades;
        ContadoEm = contadoEm;
    }

    /// <summary>A hora da contagem do item só muda quando o número muda (salvar de novo o mesmo valor não "reconta").</summary>
    internal void Alterar(int? contadoUnidades, int? saldoSistemaUnidades, DateTime? contadoEm = null)
    {
        if (contadoUnidades != ContadoUnidades)
        {
            ContadoEm = contadoUnidades is null ? null : contadoEm;
        }

        ContadoUnidades = contadoUnidades;
        SaldoSistemaUnidades = saldoSistemaUnidades ?? SaldoSistemaUnidades;
    }
}
