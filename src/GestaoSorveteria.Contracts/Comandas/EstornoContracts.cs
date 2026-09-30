using System.ComponentModel.DataAnnotations;

namespace GestaoSorveteria.Contracts.Comandas;

/// <summary>POST /api/comandas/{id}/estornar (Admin, RN-CM-09).</summary>
public sealed record EstornarComandaRequest(
    [Required(ErrorMessage = "Informe o motivo do estorno.")]
    [StringLength(300, MinimumLength = 3, ErrorMessage = "O motivo do estorno deve ter de 3 a 300 caracteres.")]
    string Motivo);

/// <summary>Resultado do estorno. <paramref name="Status"/> é "Estornada".</summary>
public sealed record EstornoResponse(Guid ComandaId, string Status, DateTime EstornadaEmUtc, string Motivo);
