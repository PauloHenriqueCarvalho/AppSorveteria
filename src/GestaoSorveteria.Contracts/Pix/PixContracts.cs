using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Pix;

/// <summary>
/// GET /api/pix/situacao (app e painel): o Pix integrado está ligado? (RN-PX-01). Desligado, o botão Pix do app continua
/// como hoje (conferido à mão). <paramref name="Provedor"/>: "Inter" ou "Simulado" (o app mostra uma faixa em Simulado).
/// <paramref name="ExpiracaoSegundos"/>: quanto tempo o QR Code vale.
/// </summary>
public sealed record PixSituacaoDto(bool Ligado, string Provedor, int ExpiracaoSegundos);

/// <summary>
/// POST /api/pix/cobrancas (RN-PX-02). <paramref name="Id"/> é gerado pelo app para esta tentativa de cobrança: repetir o
/// pedido com o mesmo Id devolve a mesma cobrança (idempotente). Mudou a venda? Novo Id. Id vazio é recusado pelo Domain.
/// </summary>
public sealed record CriarCobrancaPixRequest(
    Guid Id,
    [Range(typeof(decimal), "0.01", "99999.99", ErrorMessage = "O valor do Pix precisa ser maior que zero.")] decimal Valor);

/// <summary>
/// Cobrança Pix com valor (RN-PX-02/03). <paramref name="Status"/>: "Ativa", "Concluida", "Removida" ou "Expirada".
/// <paramref name="PixCopiaECola"/> vira o QR Code no app. <paramref name="Paga"/> = concluída com o valor exato (RN-PX-04):
/// só então o app grava a venda, com o <paramref name="Txid"/> no pagamento (RN-PX-05).
/// </summary>
public sealed record CobrancaPixDto(
    Guid Id,
    string Txid,
    string Status,
    bool Paga,
    decimal Valor,
    string PixCopiaECola,
    DateTime CriadaEm,
    DateTime ExpiraEm,
    DateTime? PagaEm,
    string? EndToEndId,
    decimal? ValorPago);
