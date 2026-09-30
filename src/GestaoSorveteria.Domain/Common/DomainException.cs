namespace GestaoSorveteria.Domain.Common;

/// <summary>
/// Violação de regra de negócio. A API converte em HTTP 400 (ProblemDetails)
/// com a mensagem em português pronta para mostrar ao usuário.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string mensagem) : base(mensagem)
    {
    }
}
