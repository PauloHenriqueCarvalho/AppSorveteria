using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using SorveteriaMaui.Data;
using Comanda = GestaoSorveteria.Domain.Comandas.Comanda;
using Produto = GestaoSorveteria.Domain.Produtos.Produto;

namespace SorveteriaMaui.Services;

/// <summary>
/// Casos de uso da comanda no aparelho: carrega do SQLite → aplica a regra do Domain → grava tudo numa transação.
/// Total, troco e validações vêm do Domain (mesmo código da API). Erros de regra saem como <see cref="DomainException"/>.
/// </summary>
public sealed class ComandaAppService(ComandaRepository comandas, ProdutoRepository produtos, CaixaProvisorio caixa)
{
    // Uma operação por vez: dois toques rápidos não podem ler a mesma versão da comanda
    // (item perdido, pagamento em dobro) nem pegar o mesmo número (RN-CM-02).
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    public Task<Guid> AbrirAsync(string? nomeCliente) =>
        UmaPorVezAsync(async () =>
        {
            var comanda = await NovaComandaAsync();
            await comandas.GravarAsync(comanda, nomeCliente);
            return comanda.Id;
        });

    /// <summary>RN-CM-03/04: mesmo produto soma na linha existente.</summary>
    public Task AdicionarProdutoAsync(Guid comandaId, Guid produtoId, int quantidade) =>
        AlterarAsync(comandaId, async comanda =>
        {
            var linha = await produtos.ObterAsync(produtoId)
                ?? throw new DomainException("Produto não encontrado. Atualize a lista de produtos.");

            var produto = Produto.Criar(linha.Nome, linha.Categoria, linha.Preco, linha.PermiteValorLivre, linha.Ordem, linha.CriadoEm, linha.Id);
            if (!linha.Ativo)
            {
                produto.Desativar(DateTime.UtcNow);
            }

            comanda.AdicionarProduto(produto, quantidade);
        });

    /// <summary>RN-CM-03: self-service / valor digitado, sem produto.</summary>
    public Task AdicionarItemLivreAsync(Guid comandaId, string descricao, decimal valor) =>
        AlterarAsync(comandaId, comanda =>
        {
            comanda.AdicionarItemLivre(descricao, valor);
            return Task.CompletedTask;
        });

    public Task AlterarQuantidadeAsync(Guid comandaId, Guid itemId, int quantidade) =>
        AlterarAsync(comandaId, comanda =>
        {
            comanda.AlterarQuantidade(itemId, quantidade);
            return Task.CompletedTask;
        });

    public Task RemoverItemAsync(Guid comandaId, Guid itemId) =>
        AlterarAsync(comandaId, comanda =>
        {
            comanda.RemoverItem(itemId);
            return Task.CompletedTask;
        });

    public Task AlterarNomeClienteAsync(Guid comandaId, string? nomeCliente) =>
        UmaPorVezAsync(async () =>
        {
            await comandas.AlterarNomeClienteAsync(comandaId, nomeCliente?.Trim());
            return true;
        });

    /// <summary>RN-CM-07 / RN-PG-03: pagamento soma exatamente o total; troco calculado pelo Domain.</summary>
    public Task<Comanda> FecharAsync(Guid comandaId, DadosPagamento pagamento) =>
        AlterarAsync(comandaId, comanda =>
        {
            comanda.Fechar([pagamento], DateTime.UtcNow);
            return Task.CompletedTask;
        });

    /// <summary>
    /// RN-CM-08 / B10: cancela comanda aberta (motivo opcional). Nada é apagado: itens ficam gravados e a
    /// comanda cancelada entra na fila de envio.
    /// </summary>
    public Task<Comanda> CancelarAsync(Guid comandaId, string? motivo) =>
        AlterarAsync(comandaId, comanda =>
        {
            comanda.Cancelar(string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(), DateTime.UtcNow);
            return Task.CompletedTask;
        });

    /// <summary>
    /// RN-CM-10 / B1: abre a comanda, lança "Venda avulsa" e fecha com o pagamento numa única gravação.
    /// Se o pagamento for recusado, nada é gravado (não sobra comanda aberta).
    /// </summary>
    public Task<Comanda> VendaRapidaAsync(decimal valor, DadosPagamento pagamento) =>
        UmaPorVezAsync(async () =>
        {
            var comanda = await NovaComandaAsync();
            comanda.AdicionarItemLivre(Comanda.DescricaoVendaAvulsa, valor);
            comanda.Fechar([pagamento], DateTime.UtcNow);
            await comandas.GravarAsync(comanda);
            return comanda;
        });

    private async Task<Comanda> NovaComandaAsync()
    {
        var caixaAtual = caixa.Atual();
        var numero = await comandas.ProximoNumeroAsync(caixaAtual.Id);
        var agora = DateTime.UtcNow;
        return Comanda.Abrir(caixaAtual, caixa.UsuarioId, numero, TipoComanda.Balcao, agora, agora);
    }

    private Task<Comanda> AlterarAsync(Guid comandaId, Func<Comanda, Task> alteracao) =>
        UmaPorVezAsync(async () =>
        {
            var comanda = await comandas.ObterAsync(comandaId)
                ?? throw new DomainException("Comanda não encontrada.");
            await alteracao(comanda);
            await comandas.GravarAsync(comanda);
            return comanda;
        });

    private async Task<T> UmaPorVezAsync<T>(Func<Task<T>> operacao)
    {
        await _umaPorVez.WaitAsync();
        try
        {
            return await operacao();
        }
        finally
        {
            _umaPorVez.Release();
        }
    }
}
