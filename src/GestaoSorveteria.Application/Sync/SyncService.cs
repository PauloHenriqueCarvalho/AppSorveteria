using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Sync;

/// <summary>
/// Recebe os documentos prontos do app (local-first, docs/03 §8). Cada documento do lote é gravado separado:
/// regra violada (<see cref="DomainException"/>) → descarta só aquele documento e devolve <c>rejeitada</c> com motivo;
/// os demais seguem. Falha de infraestrutura (banco fora do ar, gravação concorrente → 409) não vira <c>rejeitada</c>:
/// a requisição falha e o app reenvia depois — o reenvio é idempotente (RN-SY-02), então nada se perde nem duplica.
/// </summary>
public sealed class SyncService
{
    private readonly ICaixaRepository _caixas;
    private readonly IComandaRepository _comandas;
    private readonly IUsuarioRepository _usuarios;
    private readonly IProdutoRepository _produtos;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public SyncService(
        ICaixaRepository caixas,
        IComandaRepository comandas,
        IUsuarioRepository usuarios,
        IProdutoRepository produtos,
        IUnitOfWork uow,
        IClock clock)
    {
        _caixas = caixas;
        _comandas = comandas;
        _usuarios = usuarios;
        _produtos = produtos;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>POST /api/sync/caixas. Semântica de reenvio em <see cref="CaixaSyncDto"/>.</summary>
    public Task<SyncResponse> ReceberCaixasAsync(SyncCaixasRequest request, CancellationToken cancellationToken = default)
    {
        var usuariosConhecidos = new HashSet<Guid>();
        return ProcessarLoteAsync(
            request.Caixas,
            "Caixa",
            dto => dto.Id,
            dto => ReceberCaixaAsync(dto, usuariosConhecidos, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// POST /api/sync/comandas (RN-SY-06, RN-CX-08). Comanda fechada ou cancelada no celular, remontada com
    /// <see cref="Comanda.Remontar"/>. Já existente → <c>ja_recebida</c> (inclusive se a dona estornou depois).
    /// </summary>
    public Task<SyncResponse> ReceberComandasAsync(SyncComandasRequest request, CancellationToken cancellationToken = default)
    {
        var usuariosConhecidos = new HashSet<Guid>();
        var produtosConhecidos = new HashSet<Guid>();
        return ProcessarLoteAsync(
            request.Comandas,
            "Comanda",
            dto => dto.Id,
            dto => ReceberComandaAsync(dto, usuariosConhecidos, produtosConhecidos, cancellationToken),
            cancellationToken);
    }

    private async Task<SyncResponse> ProcessarLoteAsync<T>(
        IReadOnlyList<T> documentos,
        string nome,
        Func<T, Guid> id,
        Func<T, Task<string>> receber,
        CancellationToken cancellationToken)
        where T : class
    {
        var resultados = new List<ResultadoSyncDto>(documentos.Count);

        foreach (var dto in documentos)
        {
            if (dto is null)
            {
                resultados.Add(new ResultadoSyncDto(Guid.Empty, StatusSync.Rejeitada, $"{nome} vazio(a) no lote."));
                continue;
            }

            try
            {
                var status = await receber(dto);
                if (status == StatusSync.Aceita)
                {
                    await _uow.SaveChangesAsync(cancellationToken);
                }

                resultados.Add(new ResultadoSyncDto(id(dto), status));
            }
            catch (DomainException ex)
            {
                _uow.DescartarAlteracoes();
                resultados.Add(new ResultadoSyncDto(id(dto), StatusSync.Rejeitada, ex.Message));
            }
        }

        return new SyncResponse(resultados);
    }

    private async Task<string> ReceberComandaAsync(
        ComandaSyncDto dto,
        HashSet<Guid> usuariosConhecidos,
        HashSet<Guid> produtosConhecidos,
        CancellationToken cancellationToken)
    {
        Guard(dto.Id == Guid.Empty, "Comanda sem Id.");
        var status = TextoEnum.Converter<StatusComanda>(dto.Status, "Status da comanda");
        var tipo = TextoEnum.Converter<TipoComanda>(dto.Tipo, "Tipo da comanda");

        var existente = await _comandas.ObterPorIdAsync(dto.Id, cancellationToken);
        if (existente is not null)
        {
            return JaRecebida(existente, dto, status);
        }

        var itens = dto.Itens ?? [];
        var pagamentos = dto.Pagamentos ?? [];
        Guard(itens.Any(i => i is null) || pagamentos.Any(p => p is null), "Item ou pagamento vazio na comanda.");
        Guard(pagamentos.Any(p => p.Id == Guid.Empty), "Pagamento sem Id.");

        var caixa = await _caixas.ObterPorIdAsync(dto.CaixaId, cancellationToken);
        Guard(caixa is null, "O caixa desta comanda ainda não chegou ao servidor. Envie o caixa antes das comandas (RN-SY-03).");
        await ExigirUsuarioAsync(dto.UsuarioId, usuariosConhecidos, cancellationToken);

        // Índices únicos e chaves estrangeiras conferidos aqui para virar rejeição com motivo,
        // em vez de 409/500 no lote inteiro, que travaria a fila do celular para sempre.
        Guard(await _comandas.ExisteNumeroNoCaixaAsync(dto.CaixaId, dto.Numero, cancellationToken),
            $"Já existe outra comanda nº {dto.Numero} neste caixa (RN-CM-02).");
        var filhos = itens.Select(i => i.Id).Concat(pagamentos.Select(p => p.Id)).ToList();
        Guard(filhos.Distinct().Count() != filhos.Count, "A comanda tem itens ou pagamentos com Id repetido.");
        Guard(await _comandas.ExisteItemOuPagamentoAsync(filhos, cancellationToken),
            "Um item ou pagamento desta comanda já foi usado em outra comanda.");
        foreach (var produtoId in itens.Select(i => i.ProdutoId).OfType<Guid>().Distinct())
        {
            await ExigirProdutoAsync(produtoId, produtosConhecidos, cancellationToken);
        }

        var comanda = Comanda.Remontar(
            caixa!,
            dto.Id,
            dto.UsuarioId,
            dto.Numero,
            tipo,
            status,
            dto.CriadaEm,
            _clock.UtcNow,
            dto.Observacao,
            itens.Select(i => new DadosItemRecebido(i.Id, i.ProdutoId, i.Descricao ?? string.Empty, i.Quantidade, i.PrecoUnitario, i.Subtotal)).ToList(),
            dto.Total,
            pagamentos.Select(p => new DadosPagamento(
                TextoEnum.Converter<FormaPagamento>(p.Forma, "Forma de pagamento"), p.Valor, p.ValorRecebido, p.Id, TrocoInformado: p.Troco)).ToList(),
            dto.FechadaEm,
            dto.CanceladaEm,
            dto.MotivoCancelamento);

        await _comandas.AdicionarAsync(comanda, cancellationToken);
        return StatusSync.Aceita;
    }

    /// <summary>
    /// Reenvio da mesma comanda: nada muda no servidor. Mesmo status e total → <c>ja_recebida</c>; a dona pode ter
    /// estornado depois (Fechada → Cancelada, RN-CM-09), o que também é <c>ja_recebida</c>. Qualquer outra diferença
    /// é erro do app → <c>rejeitada</c>, para aparecer em vez de sumir.
    /// </summary>
    private static string JaRecebida(Comanda existente, ComandaSyncDto dto, StatusComanda status)
    {
        var estornadaDepois = existente.Status == StatusComanda.Cancelada && existente.FechadaEm is not null && status == StatusComanda.Fechada;
        Guard(existente.CaixaId != dto.CaixaId || existente.Numero != dto.Numero || existente.Total != dto.Total
              || (existente.Status != status && !estornadaDepois),
            $"Esta comanda já foi recebida com outro caixa, número, status ou total (nº {existente.Numero}, {existente.Status}, " +
            $"{existente.Total:N2}); nada foi alterado.");
        return StatusSync.JaRecebida;
    }

    private async Task<string> ReceberCaixaAsync(CaixaSyncDto dto, HashSet<Guid> usuariosConhecidos, CancellationToken cancellationToken)
    {
        Guard(dto.Id == Guid.Empty, "Caixa sem Id.");
        var movimentos = dto.Movimentos ?? [];
        Guard(movimentos.Any(m => m is null), "Movimento vazio no caixa.");

        await ExigirUsuarioAsync(dto.AbertoPorUsuarioId, usuariosConhecidos, cancellationToken);
        foreach (var movimento in movimentos)
        {
            await ExigirUsuarioAsync(movimento.UsuarioId, usuariosConhecidos, cancellationToken);
        }

        if (dto.Fechamento is { } fechamentoDto)
        {
            await ExigirUsuarioAsync(fechamentoDto.FechadoPorUsuarioId, usuariosConhecidos, cancellationToken);
        }

        var caixa = await _caixas.ObterPorIdAsync(dto.Id, cancellationToken);
        if (caixa is null)
        {
            return await CriarAsync(dto, movimentos, cancellationToken);
        }

        Guard(caixa.AbertoPorUsuarioId != dto.AbertoPorUsuarioId || !MesmoInstante(caixa.AbertoEm, dto.AbertoEm) || caixa.FundoTroco != dto.FundoTroco,
            "A abertura deste caixa é diferente da que o servidor já recebeu.");

        foreach (var m in movimentos)
        {
            var existente = caixa.Movimentos.FirstOrDefault(e => e.Id == m.Id);
            Guard(existente is not null
                  && (existente.Tipo != TextoEnum.Converter<TipoMovimentoCaixa>(m.Tipo, "Tipo de movimento")
                      || existente.Valor != m.Valor || !MesmoInstante(existente.Em, m.Em)),
                $"O movimento {m.Id} já foi recebido com outro tipo, valor ou data; nada foi alterado.");
        }

        var novos = movimentos.Where(m => caixa.Movimentos.All(existente => existente.Id != m.Id)).ToList();
        await ExigirMovimentosInexistentesAsync(novos, cancellationToken);

        if (!caixa.EstaAberto)
        {
            // RN-CX-07: caixa fechado não muda.
            Guard(novos.Count > 0, "Este caixa já está fechado no servidor; movimento novo não é aceito (RN-CX-07). Registre um ajuste no próximo caixa.");

            if (dto.Fechamento is { } fechamento && !MesmoFechamento(caixa, fechamento))
            {
                // RN-CX-09: fechamento forçado pelo painel (ou outro fechamento) chegou antes.
                throw new DomainException(
                    $"Este caixa já foi fechado no servidor em {caixa.FechadoEm:dd/MM/yyyy HH:mm} (UTC). O fechamento do celular " +
                    $"(contado {fechamento.ValorContado:N2}) não foi gravado; registre um ajuste no próximo caixa (RN-CX-09).");
            }

            return StatusSync.JaRecebida;
        }

        AplicarMovimentos(caixa, novos);

        if (dto.Fechamento is { } fechar)
        {
            await FecharAsync(caixa, fechar, cancellationToken);
        }

        return novos.Count > 0 || dto.Fechamento is not null ? StatusSync.Aceita : StatusSync.JaRecebida;
    }

    private async Task<string> CriarAsync(CaixaSyncDto dto, IReadOnlyList<MovimentoCaixaSyncDto> movimentos, CancellationToken cancellationToken)
    {
        if (dto.Fechamento is null)
        {
            // RN-CX-01: o índice único no banco segura a corrida; aqui é a mensagem amigável.
            var aberto = await _caixas.ObterAbertoAsync(cancellationToken);
            Guard(aberto is not null,
                $"Já existe outro caixa aberto no servidor (aberto em {aberto?.AbertoEm:dd/MM/yyyy HH:mm} UTC). Feche-o antes (RN-CX-01).");
        }

        await ExigirMovimentosInexistentesAsync(movimentos, cancellationToken);
        var caixa = Caixa.Abrir(dto.AbertoPorUsuarioId, dto.FundoTroco, dto.AbertoEm, dto.Id);
        AplicarMovimentos(caixa, movimentos);

        if (dto.Fechamento is { } fechamento)
        {
            await FecharAsync(caixa, fechamento, cancellationToken);
        }

        await _caixas.AdicionarAsync(caixa, cancellationToken);
        return StatusSync.Aceita;
    }

    private static void AplicarMovimentos(Caixa caixa, IEnumerable<MovimentoCaixaSyncDto> movimentos)
    {
        foreach (var m in movimentos.OrderBy(m => m.Em))
        {
            var tipo = TextoEnum.Converter<TipoMovimentoCaixa>(m.Tipo, "Tipo de movimento");
            Guard(m.Id == Guid.Empty, "Movimento sem Id.");
            // Abertura e movimento vêm do mesmo relógio (o celular).
            Guard(m.Em < caixa.AbertoEm, "O movimento é anterior à abertura do caixa.");
            if (tipo == TipoMovimentoCaixa.Sangria)
            {
                caixa.RegistrarSangria(m.Valor, m.Motivo ?? string.Empty, m.UsuarioId, m.Em, m.Id);
            }
            else
            {
                caixa.RegistrarSuprimento(m.Valor, m.Motivo ?? string.Empty, m.UsuarioId, m.Em, m.Id);
            }
        }
    }

    /// <summary>RN-CX-10: vendas em dinheiro vêm das comandas que o servidor já recebeu deste caixa.</summary>
    private async Task FecharAsync(Caixa caixa, FechamentoCaixaSyncDto fechamento, CancellationToken cancellationToken)
    {
        Guard(caixa.Movimentos.Any(m => m.Em > fechamento.FechadoEm), "Há movimento com data posterior ao fechamento do caixa.");
        var vendasDinheiro = await _comandas.TotalDinheiroFechadasNoCaixaAsync(caixa.Id, cancellationToken);
        caixa.FecharSincronizado(
            fechamento.ValorContado,
            vendasDinheiro,
            fechamento.TotalVendasDinheiro,
            fechamento.ValorEsperado,
            fechamento.FechadoPorUsuarioId,
            fechamento.FechadoEm,
            fechamento.Observacao);
    }

    /// <summary>RN-SY-02: Id de movimento já usado em outro caixa é erro do app — rejeita em vez de travar a fila com 409.</summary>
    private async Task ExigirMovimentosInexistentesAsync(IEnumerable<MovimentoCaixaSyncDto> movimentos, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        foreach (var m in movimentos)
        {
            Guard(!ids.Add(m.Id), $"O movimento {m.Id} aparece duas vezes no caixa.");
            Guard(await _caixas.ExisteMovimentoAsync(m.Id, cancellationToken), $"O Id do movimento {m.Id} já foi usado em outro caixa.");
        }
    }

    private static bool MesmoFechamento(Caixa caixa, FechamentoCaixaSyncDto fechamento) =>
        caixa.FechadoPorUsuarioId == fechamento.FechadoPorUsuarioId
        && caixa.FechadoEm is { } fechadoEm && MesmoInstante(fechadoEm, fechamento.FechadoEm)
        && caixa.ValorContado == fechamento.ValorContado;

    /// <summary>
    /// O PostgreSQL guarda timestamptz em microssegundos; o .NET em 100 ns. Sem essa tolerância, o reenvio de um
    /// documento com data de 7 casas decimais pareceria "diferente" do que já foi gravado.
    /// </summary>
    private static bool MesmoInstante(DateTime a, DateTime b) => Math.Abs((a - b).Ticks) < TimeSpan.TicksPerMicrosecond;

    private async Task ExigirProdutoAsync(Guid produtoId, HashSet<Guid> conhecidos, CancellationToken cancellationToken)
    {
        if (conhecidos.Contains(produtoId))
        {
            return;
        }

        // RN-SY-06: produto desativado ou com outro preço vale (o item guarda o preço da venda); inexistente, não.
        Guard(await _produtos.ObterPorIdAsync(produtoId, cancellationToken) is null, $"Produto não encontrado no servidor: {produtoId}.");
        conhecidos.Add(produtoId);
    }

    /// <summary>RN-TD-04: quem fez precisa existir (desativado vale: o histórico dele permanece, RN-US-07).</summary>
    private async Task ExigirUsuarioAsync(Guid usuarioId, HashSet<Guid> conhecidos, CancellationToken cancellationToken)
    {
        if (conhecidos.Contains(usuarioId))
        {
            return;
        }

        Guard(usuarioId == Guid.Empty || await _usuarios.ObterPorIdAsync(usuarioId, cancellationToken) is null,
            $"Usuário não encontrado no servidor: {usuarioId}.");
        conhecidos.Add(usuarioId);
    }

    private static void Guard(bool condicao, string mensagem)
    {
        if (condicao)
        {
            throw new DomainException(mensagem);
        }
    }
}
