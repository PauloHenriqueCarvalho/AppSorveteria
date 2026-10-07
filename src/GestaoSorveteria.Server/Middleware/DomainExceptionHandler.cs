using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GestaoSorveteria.Server.Middleware;

/// <summary>
/// Converte exceções conhecidas em ProblemDetails (RFC 9457):
/// DomainException → 400 · EntidadeNaoEncontradaException → 404 · AcessoNegadoException → 403 ·
/// violação de índice único no PostgreSQL (ex.: dois caixas abertos ao mesmo tempo) ou alteração concorrente
/// do mesmo registro (token de concorrência, ex.: dois fechamentos do mesmo caixa) → 409.
/// Qualquer outra exceção segue para o tratamento padrão (500 sem detalhes em produção).
/// </summary>
public sealed class DomainExceptionHandler : IExceptionHandler
{
    private const string ViolacaoUnicidadePostgres = "23505";

    // No painel, o usuário tenta de novo; na sincronização, o app reenvia sozinho (idempotente, RN-SY-02).
    private const string MensagemConflito = "Outro registro foi gravado ao mesmo tempo. Tente de novo em instantes.";

    private readonly IProblemDetailsService _problemDetails;

    public DomainExceptionHandler(IProblemDetailsService problemDetails)
    {
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, titulo, detalhe) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, "Regra de negócio", exception.Message),
            EntidadeNaoEncontradaException => (StatusCodes.Status404NotFound, "Não encontrado", exception.Message),
            AcessoNegadoException => (StatusCodes.Status403Forbidden, "Acesso negado", exception.Message),
            DbUpdateException { InnerException: PostgresException { SqlState: ViolacaoUnicidadePostgres } } =>
                (StatusCodes.Status409Conflict, "Conflito", MensagemConflito),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflito", MensagemConflito),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;

        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = titulo,
                Detail = detalhe,
                Instance = httpContext.Request.Path,
            },
        });
    }
}
