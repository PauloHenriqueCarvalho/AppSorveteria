namespace GestaoSorveteria.Application.Common;

/// <summary>Vira HTTP 404 na API.</summary>
public sealed class EntidadeNaoEncontradaException : Exception
{
    public EntidadeNaoEncontradaException(string entidade, Guid id)
        : base($"{entidade} não encontrado(a): {id}.")
    {
    }

    public EntidadeNaoEncontradaException(string mensagem) : base(mensagem)
    {
    }
}

/// <summary>Vira HTTP 403 na API (ex.: atendente tentando estornar).</summary>
public sealed class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string mensagem) : base(mensagem)
    {
    }
}
