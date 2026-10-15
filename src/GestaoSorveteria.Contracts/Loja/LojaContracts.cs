namespace GestaoSorveteria.Contracts.Loja;

/// <summary>
/// GET /api/configuracoes/primeiros-passos (Admin): o que já foi feito para a loja começar a usar o sistema (RN-LJ-01).
/// O painel mostra a lista no Início até tudo estar feito.
/// <paramref name="CatalogoDeEstoque"/>: há item ativo no catálogo (planilha importada) — sem ele não há contagem nem pedido.
/// <paramref name="Produtos"/>: há produto ativo no cardápio do tablet. <paramref name="Atendentes"/>: há atendente ativo.
/// <paramref name="WhatsAppDona"/>: o número que recebe o urgente e o resumo do dia. <paramref name="ContagemConcluida"/>: já
/// houve uma contagem do freezer concluída (base do pedido sugerido e do saldo dos potes). <paramref name="VendaDoTablet"/>: a
/// primeira venda do tablet já chegou.
/// </summary>
public sealed record PrimeirosPassosDto(
    bool CatalogoDeEstoque,
    bool Produtos,
    bool Atendentes,
    bool WhatsAppDona,
    bool ContagemConcluida,
    bool VendaDoTablet);
