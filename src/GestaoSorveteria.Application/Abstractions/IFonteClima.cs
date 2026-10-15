using GestaoSorveteria.Domain.Clima;

namespace GestaoSorveteria.Application.Abstractions;

/// <summary>
/// Serviço de clima (RN-CL-07). A implementação fica na Infrastructure (Visual Crossing); trocar de fonte não muda as tabelas,
/// porque cada registro guarda o <see cref="Nome"/> de onde veio.
/// </summary>
public interface IFonteClima
{
    /// <summary>Gravado em cada registro (ex.: "VisualCrossing").</summary>
    string Nome { get; }

    /// <summary>Crédito que o painel mostra perto dos dados (exigência do plano grátis).</summary>
    string Atribuicao { get; }

    string AtribuicaoLink { get; }

    /// <summary>Há chave configurada? Sem ela a coleta fica desligada (RN-CL-07).</summary>
    bool Configurada { get; }

    /// <summary>
    /// O clima de cada dia em [<paramref name="de"/>, <paramref name="ate"/>] no ponto: dias passados observados e dias
    /// futuros previstos, na mesma resposta (cada leitura diz a <see cref="LeituraClima.Origem"/>). Dia sem dado fica de fora.
    /// </summary>
    /// <exception cref="FonteClimaException">Chave recusada, limite do dia, serviço fora do ar ou resposta inválida.</exception>
    Task<IReadOnlyList<LeituraClima>> LerDiasAsync(PontoClima ponto, DateOnly de, DateOnly ate, CancellationToken cancellationToken = default);

    /// <summary>RN-CL-01: o lugar que o serviço entende pelo texto (cidade, endereço); nulo = não encontrou.</summary>
    /// <exception cref="FonteClimaException">Chave recusada, limite do dia, serviço fora do ar ou resposta inválida.</exception>
    Task<LocalEncontrado?> ProcurarLocalAsync(string texto, CancellationToken cancellationToken = default);
}

/// <summary>Lugar encontrado pelo serviço de clima (RN-CL-01).</summary>
public sealed record LocalEncontrado(string Nome, decimal Latitude, decimal Longitude);

/// <summary>Falha ao falar com o serviço de clima; a mensagem já vem pronta para o painel.</summary>
public sealed class FonteClimaException : Exception
{
    public FonteClimaException(string mensagem) : base(mensagem)
    {
    }

    public FonteClimaException(string mensagem, Exception interna) : base(mensagem, interna)
    {
    }
}
