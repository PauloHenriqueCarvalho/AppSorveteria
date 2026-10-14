namespace GestaoSorveteria.Contracts.Pedidos;

// Pedido ao fornecedor e conferência da entrega (docs/10 §2.4–2.5, RN-PF-02…08, RN-RC-01…06). Quantidades em
// embalagens (meia caixa só quando o fornecedor aceita). Valores em R$ já calculados pela API (o painel não calcula).
// Status: "Rascunho", "Enviado", "Recebido", "Cancelado". Origem da linha: "Sugestao", "Aviso", "FaltouAnterior",
// "AdicionadoPelaDona", "NaoPedido". Situação na entrega: "Certo", "Faltou", "VeioAMais", "NaoPedido".

public sealed record PedidoResumoDto(
    Guid Id,
    int Numero,
    Guid FornecedorId,
    string FornecedorNome,
    string Status,
    DateTime CriadoEm,
    DateTime? EnviadoEm,
    DateTime? RecebidoEm,
    DateTime? CanceladoEm,
    decimal TotalPedido,
    decimal? TotalRecebido,
    decimal? PercentualAtendido,
    int ItensComQuantidade);

/// <summary>
/// Pedido completo, na ordem da planilha (grupo, item). Serve para a tela do pedido, para a folha impressa (RN-PF-07: só as
/// linhas com quantidade, colunas Veio/Obs. em branco) e para a conferência. <paramref name="EntregaPrevista"/>: hoje + prazo.
/// </summary>
public sealed record PedidoDto(
    Guid Id,
    int Numero,
    Guid FornecedorId,
    string FornecedorNome,
    string Envio,
    bool MostrarPrecos,
    bool AceitaMeiaCaixa,
    string Status,
    string CriadoPorNome,
    DateTime CriadoEm,
    DateTime? EnviadoEm,
    DateTime? RecebidoEm,
    string? RecebidoPorNome,
    DateTime? CanceladoEm,
    string? CanceladoPorNome,
    string? MotivoCancelamento,
    DateOnly EntregaPrevista,
    decimal TotalPedido,
    decimal? TotalRecebido,
    decimal? PercentualAtendido,
    IReadOnlyList<LinhaPedidoDto> Linhas);

/// <summary>
/// <paramref name="ContadoUnidades"/>: última contagem concluída do item (referência para a dona revisar).
/// <paramref name="Subtotal"/> = pedido × custo; <paramref name="DiferencaValor"/> = (recebido − pedido) × custo.
/// </summary>
public sealed record LinhaPedidoDto(
    Guid ItemEstoqueId,
    Guid GrupoId,
    string GrupoNome,
    string ItemNome,
    string Unidade,
    int UnidadesPorEmbalagem,
    decimal Passo,
    int? ContadoUnidades,
    decimal SugeridoEmbalagens,
    decimal PedidoEmbalagens,
    decimal? RecebidoEmbalagens,
    decimal CustoEmbalagem,
    decimal Subtotal,
    bool AlteradoPelaDona,
    string Origem,
    string? Observacao,
    bool PedirDeNovo,
    string? Situacao,
    decimal? DiferencaValor);

/// <summary>PUT /api/pedidos-fornecedor/{id}/itens: +/− da dona; item que não está no pedido é acrescentado.</summary>
public sealed record AlterarItensPedidoRequest(IReadOnlyList<LinhaAlteradaRequest> Linhas);

public sealed record LinhaAlteradaRequest(Guid ItemEstoqueId, decimal Embalagens);

public sealed record CancelarPedidoRequest(string Motivo);

/// <summary>POST /api/pedidos-fornecedor/{id}/enviar. Mensagem e link quando o fornecedor recebe por WhatsApp (ou ambos).</summary>
public sealed record EnviarPedidoResponse(PedidoDto Pedido, string? MensagemWhatsApp, string? LinkWhatsApp);

/// <summary>
/// POST /api/pedidos-fornecedor/{id}/receber (RN-RC-01). "Chegou tudo certo" = listas vazias.
/// <paramref name="Diferencas"/>: só as linhas que vieram diferentes. <paramref name="NaoPedidos"/>: sabores trocados.
/// <paramref name="NovosCustos"/>: preço novo na nota (vale para os próximos pedidos, RN-RC-05).
/// </summary>
public sealed record ReceberPedidoRequest(
    IReadOnlyList<LinhaRecebidaRequest>? Diferencas,
    IReadOnlyList<LinhaRecebidaRequest>? NaoPedidos,
    IReadOnlyList<NovoCustoRequest>? NovosCustos);

public sealed record LinhaRecebidaRequest(Guid ItemEstoqueId, decimal RecebidoEmbalagens, bool PedirDeNovo = true);

public sealed record NovoCustoRequest(Guid ItemEstoqueId, decimal CustoEmbalagem);

/// <summary>
/// Resultado da conferência: os avisos que fecharam com a entrega (RN-AV-05a) e, se veio diferente, a mensagem para o
/// fornecedor com o que faltou, veio a mais ou veio trocado (RN-RC-04).
/// </summary>
public sealed record ReceberPedidoResponse(PedidoDto Pedido, int AvisosFechados, string? MensagemFornecedor, string? LinkWhatsApp);

/// <summary>
/// GET /api/pedidos-fornecedor/{id}/mensagens: para reabrir o WhatsApp depois. <paramref name="Pedido"/> existe para pedido
/// enviado ou recebido de fornecedor que recebe por WhatsApp (ou ambos), como no envio; <paramref name="Divergencias"/>, para
/// entrega conferida que veio diferente. Links abrem o WhatsApp do fornecedor (sem número cadastrado, a pessoa escolhe o contato).
/// </summary>
public sealed record MensagensPedidoDto(string? Pedido, string? LinkPedido, string? Divergencias, string? LinkDivergencias);

/// <summary>GET /api/fornecedores/{id}/historico (RN-RC-06): últimos 30 dias.</summary>
public sealed record HistoricoFornecedorDto(
    Guid FornecedorId,
    string FornecedorNome,
    int PedidosRecebidos,
    decimal ValorPedido,
    decimal ValorRecebido,
    decimal? PercentualAtendidoMedio,
    IReadOnlyList<ItemQueMaisFaltaDto> ItensQueMaisFaltam);

public sealed record ItemQueMaisFaltaDto(Guid ItemEstoqueId, string Descricao, int VezesQueFaltou, decimal EmbalagensQueFaltaram);
