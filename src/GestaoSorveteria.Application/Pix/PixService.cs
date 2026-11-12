using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Pix;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Pix;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Pix;

/// <summary>
/// RN-PX-01…06: Pix integrado do balcão. O app pede a cobrança (com o Id da tentativa), mostra o QR Code e consulta até o
/// banco confirmar; só então grava a venda com o txid no pagamento. O banco é sempre quem diz se o dinheiro entrou.
/// </summary>
public sealed class PixService(
    IProvedorPix provedor,
    ICobrancaPixRepository cobrancas,
    IConfiguracaoLojaRepository configuracao,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const int TamanhoMaximoDescricao = 140;

    public PixSituacaoDto Situacao() => new(provedor.Configurado, provedor.Nome, provedor.ExpiracaoSegundos);

    /// <summary>
    /// RN-PX-02: cria a cobrança no banco. Mesmo Id de novo → a mesma cobrança, sem chamar o banco outra vez (o app repete o
    /// pedido quando a resposta se perde). Mesmo Id com outro valor é erro do app: a venda mudou e precisava de um Id novo.
    /// </summary>
    public async Task<CobrancaPixDto> CriarAsync(CriarCobrancaPixRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!provedor.Configurado)
        {
            throw new DomainException("O Pix integrado não está ligado. Cobre o Pix como antes e confira no aplicativo do banco (RN-PX-01).");
        }

        var existente = request.Id == Guid.Empty ? null : await cobrancas.ObterPorIdAsync(request.Id, cancellationToken);
        if (existente is not null)
        {
            if (existente.Valor != request.Valor)
            {
                throw new DomainException("Esta cobrança Pix já foi criada com outro valor. Comece a cobrança de novo (RN-PX-02).");
            }

            return ParaDto(existente);
        }

        // Antes de chamar o banco: valor com mais de 2 casas viraria outro valor lá (arredondado) e uma cobrança órfã
        if (request.Valor <= 0 || !Moeda.TemNoMaximoDuasCasas(request.Valor))
        {
            throw new DomainException("O valor do Pix precisa ser maior que zero e ter no máximo 2 casas decimais.");
        }

        var txid = CobrancaPix.TxidDe(request.Id);
        var nomeLoja = (await configuracao.ObterAsync(cancellationToken))?.NomeLoja ?? ConfiguracaoLoja.NomePadrao;
        var descricao = Limitar($"{nomeLoja} — compra no balcão");
        var noBanco = await provedor.CriarAsync(new NovaCobrancaPix(txid, request.Valor, provedor.ExpiracaoSegundos, descricao), cancellationToken);
        if (noBanco.Valor != request.Valor)
        {
            // Txid repetido no banco com outro valor (não deveria acontecer: o Id é novo a cada tentativa)
            throw new DomainException("O banco já tem esta cobrança com outro valor. Comece a cobrança de novo (RN-PX-02).");
        }

        var cobranca = CobrancaPix.Criar(
            request.Id, usuarioId, request.Valor, noBanco.PixCopiaECola, noBanco.Situacao, clock.UtcNow, provedor.ExpiracaoSegundos);
        await cobrancas.AdicionarAsync(cobranca, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Dois pedidos com o mesmo Id ao mesmo tempo (o app repete enquanto a API acorda): o outro gravou primeiro.
            unitOfWork.DescartarAlteracoes();
            var gravada = await cobrancas.ObterPorIdAsync(request.Id, cancellationToken);
            if (gravada is null || gravada.Valor != request.Valor)
            {
                throw;
            }

            return ParaDto(gravada);
        }

        return ParaDto(cobranca);
    }

    /// <summary>
    /// RN-PX-03: a cobrança como está agora. Enquanto ativa, pergunta ao banco; concluída, removida ou expirada já é final e
    /// não gasta chamada. Banco fora do ar → <see cref="ProvedorPixException"/> (o app mostra e continua tentando).
    /// </summary>
    public async Task<CobrancaPixDto> ConsultarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cobranca = await ObterAsync(id, cancellationToken);
        if (cobranca.Status == StatusCobrancaPix.Ativa)
        {
            var noBanco = await provedor.ConsultarAsync(cobranca.Txid, cancellationToken);
            var mudou = cobranca.AtualizarPeloBanco(noBanco.Situacao, clock.UtcNow);
            mudou |= cobranca.ExpirarSePassou(clock.UtcNow);
            if (mudou)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        return ParaDto(cobranca);
    }

    /// <summary>
    /// RN-PX-06: o atendente desistiu do Pix integrado. Se o cliente pagou no último segundo, o banco responde concluída e a
    /// cobrança volta paga — o app grava a venda em vez de cancelar.
    /// </summary>
    public async Task<CobrancaPixDto> CancelarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var cobranca = await ObterAsync(id, cancellationToken);
        if (cobranca.Status == StatusCobrancaPix.Ativa)
        {
            var noBanco = await provedor.CancelarAsync(cobranca.Txid, cancellationToken);
            if (noBanco.Situacao.Status == StatusCobrancaPix.Ativa)
            {
                // O banco não removeu: o QR Code ainda vale. Marcar removida aqui faria a API parar de consultar e um
                // pagamento feito depois nunca apareceria (RN-PX-03/06). Fica ativa; o app tenta de novo ou espera.
                throw new ProvedorPixException("O banco não confirmou o cancelamento do Pix. O QR Code ainda vale: tente cancelar de novo.");
            }

            cobranca.AtualizarPeloBanco(noBanco.Situacao, clock.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return ParaDto(cobranca);
    }

    private async Task<CobrancaPix> ObterAsync(Guid id, CancellationToken cancellationToken) =>
        await cobrancas.ObterPorIdAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Cobrança Pix", id);

    private static string Limitar(string texto) => texto.Length <= TamanhoMaximoDescricao ? texto : texto[..TamanhoMaximoDescricao];

    private static CobrancaPixDto ParaDto(CobrancaPix c) => new(
        c.Id, c.Txid, c.Status.ToString(), c.PagaComValorCerto, c.Valor, c.PixCopiaECola, c.CriadaEm, c.ExpiraEm, c.PagaEm, c.EndToEndId, c.ValorPago);
}
