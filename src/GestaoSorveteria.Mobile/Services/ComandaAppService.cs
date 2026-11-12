using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Data;
using SorveteriaMaui.Model;
using SorveteriaMaui.Services.Api;
using Comanda = GestaoSorveteria.Domain.Comandas.Comanda;
using Produto = GestaoSorveteria.Domain.Produtos.Produto;

namespace SorveteriaMaui.Services;

/// <summary>
/// RN-ES-09: o que a tela precisa antes de lançar um produto. <paramref name="PedeSabor"/>: o produto é um pote (grupo
/// <c>SaldoPelaVenda</c>) e a tela mostra a grade com os <paramref name="Sabores"/> ativos do grupo, na ordem da planilha.
/// Pote sem sabor no tablet (catálogo de estoque ainda não baixado) vende sem sabor.
/// </summary>
public sealed record SaboresDoProduto(bool PedeSabor, IReadOnlyList<ItemEstoqueLocal> Sabores)
{
    public static readonly SaboresDoProduto Nenhum = new(false, []);

    /// <summary>Pede sabor, mas o tablet ainda não tem nenhum sabor ativo do grupo: vende sem sabor e avisa.</summary>
    public bool SemSaboresNoTablet => PedeSabor && Sabores.Count == 0;
}

/// <summary>
/// RN-CM-13: um item tocado na tela do balcão. Com <paramref name="ProdutoId"/>: produto do catálogo (pote com
/// <paramref name="SaborId"/> ou sem sabor; <paramref name="ValorInformado"/> só no produto de valor livre). Sem produto:
/// valor digitado (item livre "Venda avulsa", RN-CM-03).
/// </summary>
public sealed record ItemDoBalcao(Guid? ProdutoId, int Quantidade, Guid? SaborId = null, decimal? ValorInformado = null, string? Descricao = null);

/// <summary>
/// Casos de uso da comanda no aparelho: carrega do SQLite → aplica a regra do Domain → grava tudo numa transação.
/// Total, troco e validações vêm do Domain (mesmo código da API). Erros de regra saem como <see cref="DomainException"/>.
/// </summary>
public sealed class ComandaAppService(
    ComandaRepository comandas,
    ProdutoRepository produtos,
    CaixaRepository caixas,
    ItemEstoqueRepository itensEstoque,
    SessaoUsuario sessao,
    SyncService sync)
{
    // Uma operação por vez: dois toques rápidos não podem ler a mesma versão da comanda
    // (item perdido, pagamento em dobro) nem pegar o mesmo número (RN-CM-02).
    private readonly SemaphoreSlim _umaPorVez = new(1, 1);

    /// <summary>RN-CM-02/11: balcão ou delivery; no delivery a observação guarda nome/endereço.</summary>
    public Task<Guid> AbrirAsync(string? nomeCliente, TipoComanda tipo = TipoComanda.Balcao, string? observacao = null) =>
        UmaPorVezAsync(async () =>
        {
            var comanda = await NovaComandaAsync(tipo, observacao);
            await comandas.GravarAsync(comanda, nomeCliente);
            return comanda.Id;
        });

    /// <summary>
    /// RN-PR-06 / RN-ES-09: o produto pede sabor? Decide pelo modo do grupo baixado do catálogo de estoque (muda junto quando
    /// a dona troca o modo); se o tablet ainda não conhece o grupo, vale o <c>PedeSabor</c> que veio com o produto.
    /// Só lê o SQLite: funciona sem internet.
    /// </summary>
    public async Task<SaboresDoProduto> SaboresAsync(Guid produtoId)
    {
        var linha = await produtos.ObterAsync(produtoId);
        if (linha?.GrupoEstoqueId is not { } grupoId)
        {
            return SaboresDoProduto.Nenhum;
        }

        var doGrupo = await itensEstoque.ListarDoGrupoAsync(grupoId);
        ModoControle? modo = doGrupo.Count > 0 ? doGrupo[0].ModoControleGrupo : null;
        if (!linha.PedeSaborNoModo(modo))
        {
            return SaboresDoProduto.Nenhum;
        }

        return new SaboresDoProduto(true, doGrupo.Where(i => i.Ativo).ToList());
    }

    /// <summary>
    /// RN-CM-03/04: mesmo produto (e mesmo sabor) soma na linha existente. <paramref name="saborId"/>: RN-ES-09, o sabor
    /// escolhido na grade do pote — o Domain confere que é do grupo do produto e está ativo, e cada sabor vira uma linha
    /// ("Pote 1L · Flocos"). Nulo = sem sabor (picolé, massa, pote em que o atendente tocou "Sem sabor" — RN-ES-09 —, ou
    /// pote enquanto o tablet não tem os sabores).
    /// A venda nunca é recusada por saldo de estoque.
    /// </summary>
    public Task AdicionarProdutoAsync(Guid comandaId, Guid produtoId, int quantidade, Guid? saborId = null) =>
        AlterarAsync(comandaId, comanda => LancarProdutoAsync(comanda, produtoId, quantidade, saborId, valorInformado: null));

    /// <summary>
    /// RN-CM-13: total da venda do balcão antes de cobrar, calculado pelo Domain (mesma conta do fechamento).
    /// Não grava nada. Exige caixa aberto, como a venda.
    /// </summary>
    public async Task<decimal> TotalDoBalcaoAsync(IReadOnlyList<ItemDoBalcao> itens)
    {
        if (itens.Count == 0)
        {
            return 0m;
        }

        var rascunho = await NovaComandaAsync();
        await LancarItensAsync(rascunho, itens);
        return rascunho.Total;
    }

    /// <summary>
    /// RN-CM-13 / RN-CM-10 / B1: venda do balcão — abre a comanda, lança os itens tocados (com o sabor, nos potes, e o valor
    /// digitado como "Venda avulsa") e fecha com o pagamento numa única gravação. Se algo for recusado, nada é gravado (não
    /// sobra comanda aberta).
    /// </summary>
    /// <summary>RN-PX-05: o Pix integrado com este txid já foi gravado como venda aqui?</summary>
    public Task<bool> JaRegistrouPixAsync(string txid) => comandas.ExistePagamentoComPixTxidAsync(txid);

    public Task<Comanda> VenderNoBalcaoAsync(IReadOnlyList<ItemDoBalcao> itens, IReadOnlyList<DadosPagamento> pagamentos) =>
        DepoisEnviar(UmaPorVezAsync(async () =>
        {
            if (itens.Count == 0)
            {
                throw new DomainException("Toque nos produtos antes de cobrar.");
            }

            var comanda = await NovaComandaAsync();
            await LancarItensAsync(comanda, itens);
            comanda.Fechar(pagamentos, DateTime.UtcNow);
            await comandas.GravarAsync(comanda);
            return comanda;
        }));

    private async Task LancarItensAsync(Comanda comanda, IReadOnlyList<ItemDoBalcao> itens)
    {
        foreach (var item in itens)
        {
            if (item.ProdutoId is { } produtoId)
            {
                await LancarProdutoAsync(comanda, produtoId, item.Quantidade, item.SaborId, item.ValorInformado);
            }
            else
            {
                // RN-CM-03: valor digitado no balcão é item livre (ProdutoId nulo), como a venda rápida
                comanda.AdicionarItemLivre(item.Descricao ?? Comanda.DescricaoVendaAvulsa,
                    item.ValorInformado ?? throw new DomainException("Informe o valor da venda avulsa."), item.Quantidade);
            }
        }
    }

    private async Task LancarProdutoAsync(Comanda comanda, Guid produtoId, int quantidade, Guid? saborId, decimal? valorInformado)
    {
        var linha = await produtos.ObterAsync(produtoId)
            ?? throw new DomainException("Produto não encontrado. Atualize a lista de produtos.");

        var agora = DateTime.UtcNow;
        var produto = Produto.Criar(linha.Nome, linha.Categoria, linha.Preco, linha.PermiteValorLivre, linha.Ordem, linha.CriadoEm, linha.Id);
        if (linha.GrupoEstoqueId is { } grupoId && !linha.PermiteValorLivre)
        {
            // RN-PR-06: mesmo produto da API, ligado ao grupo (o Domain confere o sabor contra ele)
            produto.LigarAoEstoque(grupoId, Math.Clamp(linha.UnidadesPorVenda, 1, Produto.UnidadesPorVendaMaximo), agora);
        }

        if (!linha.Ativo)
        {
            produto.Desativar(agora);
        }

        ItemEstoque? sabor = null;
        if (saborId is { } id)
        {
            var saborLocal = await itensEstoque.ObterAsync(id)
                ?? throw new DomainException("Este sabor não está mais na lista do tablet. Toque no produto de novo.");
            sabor = saborLocal.ParaDominio();
        }

        // RN-PR-02: produto de valor livre leva o valor digitado; os outros, o preço do cadastro
        comanda.AdicionarProduto(produto, quantidade, valorInformado, sabor: sabor);
    }

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

    /// <summary>RN-CM-06/11: observação (no delivery, nome/endereço) editável enquanto a comanda está aberta (até 300 caracteres).</summary>
    public Task AlterarObservacaoAsync(Guid comandaId, string? observacao) =>
        AlterarAsync(comandaId, comanda =>
        {
            comanda.AlterarObservacao(observacao);
            return Task.CompletedTask;
        });

    public Task AlterarNomeClienteAsync(Guid comandaId, string? nomeCliente) =>
        UmaPorVezAsync(async () =>
        {
            await comandas.AlterarNomeClienteAsync(comandaId, nomeCliente?.Trim());
            return true;
        });

    /// <summary>RN-CM-07 / RN-PG-02/03: pagamentos (um ou vários) somam exatamente o total; troco calculado pelo Domain.</summary>
    public Task<Comanda> FecharAsync(Guid comandaId, IReadOnlyList<DadosPagamento> pagamentos) =>
        DepoisEnviar(AlterarAsync(comandaId, async comanda =>
        {
            // RN-CX-05/06: o dinheiro desta venda tem que cair no esperado do caixa aberto, nunca num já fechado.
            var aberto = await caixas.ObterAbertoLinhaAsync();
            if (aberto is null || aberto.Id != comanda.CaixaId)
            {
                throw new DomainException("Esta comanda pertence a um caixa já fechado. Cancele-a e lance de novo no caixa atual.");
            }

            comanda.Fechar(pagamentos, DateTime.UtcNow);
        }));

    /// <summary>
    /// RN-CM-08 / B10: cancela comanda aberta (motivo opcional). Nada é apagado: itens ficam gravados e a
    /// comanda cancelada entra na fila de envio.
    /// </summary>
    public Task<Comanda> CancelarAsync(Guid comandaId, string? motivo) =>
        DepoisEnviar(AlterarAsync(comandaId, comanda =>
        {
            comanda.Cancelar(string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(), DateTime.UtcNow);
            return Task.CompletedTask;
        }));

    /// <summary>RN-SY-03: comanda pronta (fechada/cancelada) sobe logo em seguida, em segundo plano. A venda já está gravada.</summary>
    private async Task<Comanda> DepoisEnviar(Task<Comanda> operacao)
    {
        var comanda = await operacao;
        sync.Disparar();
        return comanda;
    }

    private async Task<Comanda> NovaComandaAsync(TipoComanda tipo = TipoComanda.Balcao, string? observacao = null)
    {
        // A comanda leva quem vendeu (atendente logado, C1). Sem login o app nem chega nesta tela.
        var usuarioId = sessao.UsuarioId
            ?? throw new DomainException("Nenhum atendente conectado. Entre com o PIN para vender.");

        // RN-CX-03 / C3: não há venda sem caixa aberto (a tela já avisa antes; aqui é a garantia).
        var caixaAtual = await caixas.ObterAbertoAsync()
            ?? throw new DomainException("Não há caixa aberto. Abra o caixa na aba Caixa antes de vender (RN-CX-03).");
        var numero = await comandas.ProximoNumeroAsync(caixaAtual.Id);
        var agora = DateTime.UtcNow;
        return Comanda.Abrir(caixaAtual, usuarioId, numero, tipo, agora, agora, observacao);
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
