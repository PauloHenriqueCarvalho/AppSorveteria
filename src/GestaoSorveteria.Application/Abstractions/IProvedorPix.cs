using GestaoSorveteria.Domain.Pix;

namespace GestaoSorveteria.Application.Abstractions;

/// <summary>
/// RN-PX-01: banco que cria e consulta cobranças Pix com valor (API Pix do Banco Central). A implementação fica na
/// Infrastructure (Inter; Simulado para testes). As credenciais ficam só no servidor, nunca no app.
/// </summary>
public interface IProvedorPix
{
    /// <summary>"Inter", "Simulado" ou "Desligado".</summary>
    string Nome { get; }

    /// <summary>Credenciais completas? Sem elas o Pix integrado fica desligado e o app cobra o Pix à mão (RN-PX-01).</summary>
    bool Configurado { get; }

    /// <summary>Validade do QR Code em segundos (RN-PX-02; padrão 10 minutos).</summary>
    int ExpiracaoSegundos { get; }

    /// <summary>
    /// Cria a cobrança com o txid informado. Se o banco já tiver uma cobrança com esse txid (pedido repetido depois de uma
    /// queda), devolve a existente.
    /// </summary>
    /// <exception cref="ProvedorPixException">Credenciais recusadas, banco fora do ar ou resposta inválida.</exception>
    Task<CobrancaNoBanco> CriarAsync(NovaCobrancaPix cobranca, CancellationToken cancellationToken = default);

    /// <exception cref="ProvedorPixException">Credenciais recusadas, banco fora do ar ou resposta inválida.</exception>
    Task<CobrancaNoBanco> ConsultarAsync(string txid, CancellationToken cancellationToken = default);

    /// <summary>RN-PX-06: remove a cobrança ativa (o QR Code deixa de valer). Devolve como ela ficou.</summary>
    /// <exception cref="ProvedorPixException">Credenciais recusadas, banco fora do ar ou resposta inválida.</exception>
    Task<CobrancaNoBanco> CancelarAsync(string txid, CancellationToken cancellationToken = default);
}

/// <summary>O que pedir ao banco: txid, valor, validade e o texto que aparece para o cliente no app do banco dele.</summary>
public sealed record NovaCobrancaPix(string Txid, decimal Valor, int ExpiracaoSegundos, string Descricao);

/// <summary>A cobrança como está no banco.</summary>
public sealed record CobrancaNoBanco(string Txid, decimal Valor, string PixCopiaECola, SituacaoCobrancaPix Situacao);

/// <summary>Falha ao falar com o banco; a mensagem já vem pronta para a tela (sem segredos).</summary>
public sealed class ProvedorPixException : Exception
{
    public ProvedorPixException(string mensagem) : base(mensagem)
    {
    }

    public ProvedorPixException(string mensagem, Exception interna) : base(mensagem, interna)
    {
    }
}
