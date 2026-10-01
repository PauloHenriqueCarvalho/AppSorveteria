using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Sync;

// Sincronização local-first (docs/03, seções 6 e 8): o app envia caixas e comandas prontos,
// com Ids (Guid) gerados no celular. A API é idempotente pelo Id (RN-SY-02).
//
// Só o lote tem validação por atributo. Os itens do lote NÃO têm: um item inválido não pode
// derrubar o lote inteiro com 400 — ele vira "rejeitada" com motivo no resultado daquele item,
// a partir das regras do Domain. Por isso os textos e listas dos itens são anuláveis (string?, lista?):
// com Nullable ligado, o [ApiController] trataria um tipo não anulável como [Required] implícito.
//
// Enums trafegam como texto, com os mesmos nomes do Domain (ex.: "Dinheiro", "Balcao", "Fechada").
// Datas em UTC (ISO 8601 com "Z"; data local é rejeitada). Dinheiro em decimal com 2 casas.

/// <summary>Limites do lote de sincronização.</summary>
public static class LimitesSync
{
    public const int MaximoPorLote = 100;
}

/// <summary>Resultado de cada item do lote.</summary>
public static class StatusSync
{
    /// <summary>Gravado agora.</summary>
    public const string Aceita = "aceita";

    /// <summary>Já existia com o mesmo Id; nada foi duplicado. O app marca como enviado.</summary>
    public const string JaRecebida = "ja_recebida";

    /// <summary>Violou uma regra; o motivo aparece para o atendente e nada foi gravado.</summary>
    public const string Rejeitada = "rejeitada";
}

/// <summary>Resposta 200 de <c>POST /api/sync/caixas</c> e <c>POST /api/sync/comandas</c>: um resultado por item, na ordem do lote.</summary>
public sealed record SyncResponse(IReadOnlyList<ResultadoSyncDto> Resultados);

/// <summary><paramref name="Status"/> é um dos valores de <see cref="StatusSync"/>; <paramref name="Motivo"/> vem preenchido quando rejeitada.</summary>
public sealed record ResultadoSyncDto(Guid Id, string Status, string? Motivo = null);

// ---------------------------------------------------------------- caixas

/// <summary>
/// POST /api/sync/caixas. Ordem de envio: (1) o caixa aberto, sem fechamento; (2) as comandas dele;
/// (3) o caixa de novo, com <see cref="CaixaSyncDto.Fechamento"/> (RN-SY-03). Venda que chegar depois do fechamento
/// é aceita e marcada para conferência, sem mudar os valores do caixa (RN-CX-07/08).
/// </summary>
public sealed record SyncCaixasRequest(
    [Required(ErrorMessage = "Informe os caixas.")]
    [MinLength(1, ErrorMessage = "Envie pelo menos um caixa.")]
    [MaxLength(LimitesSync.MaximoPorLote, ErrorMessage = "Envie no máximo 100 caixas por vez.")]
    IReadOnlyList<CaixaSyncDto> Caixas);

/// <summary>
/// Caixa como está no celular: aberto (sem <paramref name="Fechamento"/>) ou fechado.
/// Reenvio do mesmo Id só acrescenta (RN-SY-02, RN-TD-03):
/// <list type="bullet">
/// <item>movimentos novos ou fechamento novo → <see cref="StatusSync.Aceita"/>;</item>
/// <item>nada novo → <see cref="StatusSync.JaRecebida"/>;</item>
/// <item>movimento que já estava no servidor e não veio no reenvio → continua gravado (nada é apagado);</item>
/// <item>caixa já fechado no servidor com fechamento diferente, ou movimento novo depois do fechamento → <see cref="StatusSync.Rejeitada"/> (RN-CX-07).</item>
/// </list>
/// </summary>
public sealed record CaixaSyncDto(
    Guid Id,
    Guid AbertoPorUsuarioId,
    DateTime AbertoEm,
    decimal FundoTroco,
    IReadOnlyList<MovimentoCaixaSyncDto>? Movimentos,
    FechamentoCaixaSyncDto? Fechamento = null);

/// <summary>RN-CX-04. <paramref name="Tipo"/>: "Sangria" ou "Suprimento".</summary>
public sealed record MovimentoCaixaSyncDto(
    Guid Id,
    string? Tipo,
    decimal Valor,
    string? Motivo,
    Guid UsuarioId,
    DateTime Em);

/// <summary>
/// RN-CX-06: fechamento feito no celular com <c>Caixa.Fechar</c>. Os valores calculados
/// (vendas em dinheiro, esperado, diferença) vão junto para comparação: a API grava os valores dela
/// e, se forem diferentes, guarda os do celular e marca divergência — o caixa nunca é rejeitado por isso (RN-CX-10).
/// </summary>
public sealed record FechamentoCaixaSyncDto(
    Guid FechadoPorUsuarioId,
    DateTime FechadoEm,
    decimal ValorContado,
    decimal TotalVendasDinheiro,
    decimal ValorEsperado,
    decimal Diferenca,
    string? Observacao = null);

// ---------------------------------------------------------------- comandas

/// <summary>POST /api/sync/comandas. Só comandas fechadas ou canceladas.</summary>
public sealed record SyncComandasRequest(
    [Required(ErrorMessage = "Informe as comandas.")]
    [MinLength(1, ErrorMessage = "Envie pelo menos uma comanda.")]
    [MaxLength(LimitesSync.MaximoPorLote, ErrorMessage = "Envie no máximo 100 comandas por vez.")]
    IReadOnlyList<ComandaSyncDto> Comandas);

/// <summary>
/// Comanda completa como ficou no celular. <paramref name="Status"/>: "Fechada" ou "Cancelada".
/// <paramref name="Tipo"/>: "Balcao" ou "Delivery". <paramref name="Total"/> é o total calculado no
/// celular; a API reconstrói a comanda com as regras do Domain e rejeita se não bater.
/// <paramref name="CriadaEm"/> é a hora do celular (RN-CM-12); a hora de recebimento é do servidor.
/// </summary>
public sealed record ComandaSyncDto(
    Guid Id,
    Guid CaixaId,
    Guid UsuarioId,
    int Numero,
    string? Tipo,
    string? Status,
    decimal Total,
    DateTime CriadaEm,
    IReadOnlyList<ItemComandaSyncDto>? Itens,
    IReadOnlyList<PagamentoSyncDto>? Pagamentos,
    string? Observacao = null,
    DateTime? FechadaEm = null,
    DateTime? CanceladaEm = null,
    string? MotivoCancelamento = null);

/// <summary>
/// RN-CM-03/05. <paramref name="ProdutoId"/> nulo = item livre (self-service, venda avulsa).
/// Descrição e preço são os praticados na venda (RN-PR-04).
/// </summary>
public sealed record ItemComandaSyncDto(
    Guid Id,
    Guid? ProdutoId,
    string? Descricao,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

/// <summary>
/// RN-PG-01/03. <paramref name="Forma"/>: "Dinheiro", "Pix", "CartaoDebito" ou "CartaoCredito".
/// <paramref name="Troco"/> é o calculado no celular; a API recalcula e rejeita se não bater (RN-PG-04).
/// </summary>
public sealed record PagamentoSyncDto(
    Guid Id,
    string? Forma,
    decimal Valor,
    decimal ValorRecebido,
    decimal Troco);
