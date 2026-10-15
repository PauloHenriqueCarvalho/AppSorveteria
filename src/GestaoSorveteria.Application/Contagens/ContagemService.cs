using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Estoque;
using GestaoSorveteria.Contracts.Contagens;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Contagens;

/// <summary>
/// Contagem semanal do freezer (RN-ES-05/06, docs/10 §2.1): pelo painel (rascunho e conclusão) e pelo tablet (sincronização).
/// Só os grupos <c>Contagem</c> e <c>SaldoPelaVenda</c> entram; insumos (<c>SoAviso</c>) não.
/// </summary>
public sealed class ContagemService
{
    /// <summary>A lista do painel mostra as contagens dos últimos dias (e as que ainda não foram concluídas).</summary>
    public const int DiasNaLista = 60;

    private readonly SaldoEstoqueService _saldos;
    private readonly IContagemRepository _contagens;
    private readonly IEstoqueRepository _estoque;
    private readonly IUsuarioRepository _usuarios;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public ContagemService(
        IContagemRepository contagens,
        IEstoqueRepository estoque,
        IUsuarioRepository usuarios,
        SaldoEstoqueService saldos,
        IUnitOfWork uow,
        IClock clock)
    {
        _saldos = saldos;
        _contagens = contagens;
        _estoque = estoque;
        _usuarios = usuarios;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Contagens dos últimos 60 dias e as abertas, mais recentes primeiro.</summary>
    public async Task<IReadOnlyList<ContagemResumoDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var agora = _clock.UtcNow;
        var contagens = await _contagens.ListarAsync(agora.AddDays(-DiasNaLista), agora.AddMinutes(1), cancellationToken);
        var nomes = await NomesDosUsuariosAsync(cancellationToken);
        return contagens
            .OrderByDescending(c => c.Concluida ? c.ConcluidaEm : c.IniciadaEm)
            .Select(c => new ContagemResumoDto(
                c.Id,
                nomes.GetValueOrDefault(c.UsuarioId, "Usuário removido"),
                c.Origem.ToString(),
                c.IniciadaEm,
                c.ConcluidaEm,
                c.Itens.Count(i => i.ContadoUnidades is not null),
                c.Itens.Count(i => i.ContadoUnidades is null)))
            .ToList();
    }

    /// <summary>Uma contagem com todas as linhas (itens contáveis), a referência da anterior e o que já foi contado.</summary>
    public async Task<ContagemDto> ObterAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var contagem = await _contagens.ObterPorIdAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Contagem", id);
        return await MontarAsync(contagem, cancellationToken);
    }

    /// <summary>
    /// Modelo para começar uma contagem no painel: Id novo, todos os itens contáveis sem quantidade e a contagem anterior como
    /// referência (RN-ES-06: o campo começa vazio). Nada é gravado até o primeiro <see cref="SalvarAsync"/>.
    /// </summary>
    public async Task<ContagemDto> ModeloAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var contagem = Contagem.Iniciar(usuarioId, _clock.UtcNow, OrigemContagem.Painel);
        return await MontarAsync(contagem, cancellationToken);
    }

    /// <summary>
    /// Painel: cria ou continua uma contagem aberta (parcial vale, RN-ES-05) e, se pedido, conclui.
    /// Linha com embalagens nulas volta a "não contado"; item ausente do pedido não muda.
    /// </summary>
    public async Task<ContagemDto> SalvarAsync(SalvarContagemRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var agora = _clock.UtcNow;
        var contagem = request.Id is { } id && id != Guid.Empty ? await _contagens.ObterPorIdAsync(id, cancellationToken) : null;
        var nova = contagem is null;
        contagem ??= Contagem.Iniciar(usuarioId, agora, OrigemContagem.Painel, request.Id);

        var catalogo = await CatalogoAsync(cancellationToken);
        foreach (var linha in request.Linhas ?? [])
        {
            var (item, grupo) = catalogo.Item(linha.ItemEstoqueId);
            contagem.Registrar(item, grupo, linha.Embalagens, contadoEmUtc: agora);
        }

        if (request.Concluir)
        {
            contagem.Concluir(agora);
        }

        if (nova)
        {
            await _contagens.AdicionarAsync(contagem, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return await MontarAsync(contagem, cancellationToken);
    }

    /// <summary>
    /// POST /api/sync/contagens. Cada contagem é gravada separada:
    /// <list type="bullet">
    /// <item>nova → criada com as linhas do tablet (e concluída, se veio concluída) — <c>aceita</c>;</item>
    /// <item>já existe e aberta → recebe as linhas (e a conclusão) — <c>aceita</c>, ou <c>ja_recebida</c> se nada mudou;</item>
    /// <item>já concluída → <c>ja_recebida</c> se o tablet não mudou nada depois; mudança numa contagem concluída é <c>rejeitada</c>.</item>
    /// </list>
    /// </summary>
    public async Task<SyncResponse> ReceberSyncAsync(SyncContagensRequest request, CancellationToken cancellationToken = default)
    {
        var resultados = new List<ResultadoSyncDto>(request.Contagens.Count);
        var catalogo = await CatalogoAsync(cancellationToken);
        var usuariosConhecidos = new HashSet<Guid>();

        foreach (var dto in request.Contagens)
        {
            if (dto is null)
            {
                resultados.Add(new ResultadoSyncDto(Guid.Empty, StatusSync.Rejeitada, "Contagem vazia no lote."));
                continue;
            }

            try
            {
                var status = await ReceberContagemAsync(dto, catalogo, usuariosConhecidos, cancellationToken);
                if (status == StatusSync.Aceita)
                {
                    await _uow.SaveChangesAsync(cancellationToken);
                }

                resultados.Add(new ResultadoSyncDto(dto.Id, status));
            }
            catch (DomainException ex)
            {
                _uow.DescartarAlteracoes();
                resultados.Add(new ResultadoSyncDto(dto.Id, StatusSync.Rejeitada, ex.Message));
            }
        }

        return new SyncResponse(resultados);
    }

    private async Task<string> ReceberContagemAsync(
        ContagemSyncDto dto, Catalogo catalogo, HashSet<Guid> usuariosConhecidos, CancellationToken cancellationToken)
    {
        Exigir.Que(dto.Id != Guid.Empty, "Contagem sem Id.");
        var linhas = dto.Linhas ?? [];
        Exigir.Que(linhas.All(l => l is not null), "Linha vazia na contagem.");
        Exigir.Que(linhas.Select(l => l.ItemEstoqueId).Distinct().Count() == linhas.Count, "A contagem tem o mesmo item duas vezes.");
        var limite = _clock.UtcNow.AddMinutes(10);
        Exigir.Que(dto.IniciadaEm <= limite && (dto.ConcluidaEm ?? dto.IniciadaEm) <= limite,
            "A data da contagem está no futuro: confira o relógio do tablet.");
        if (!usuariosConhecidos.Contains(dto.UsuarioId))
        {
            Exigir.Que(await _usuarios.ObterPorIdAsync(dto.UsuarioId, cancellationToken) is not null,
                "Usuário da contagem não encontrado no servidor.");
            usuariosConhecidos.Add(dto.UsuarioId);
        }

        var existente = await _contagens.ObterPorIdAsync(dto.Id, cancellationToken);
        if (existente is { Concluida: true })
        {
            Exigir.Que(MesmasLinhas(existente, linhas, catalogo),
                "Esta contagem já foi concluída e não muda mais. Para corrigir, faça uma nova contagem.");
            return StatusSync.JaRecebida;
        }

        var contagem = existente ?? Contagem.Iniciar(dto.UsuarioId, dto.IniciadaEm, OrigemContagem.App, dto.Id);
        var antes = Retrato(contagem);
        foreach (var linha in linhas)
        {
            var (item, grupo) = catalogo.Item(linha.ItemEstoqueId);
            contagem.Registrar(
                item, grupo, linha.Embalagens, linhaId: linha.Id == Guid.Empty ? null : linha.Id,
                contadoEmUtc: ContadoEmNoTablet(linha.ContadoEm, contagem.IniciadaEm, dto.ConcluidaEm ?? _clock.UtcNow));
        }

        if (dto.ConcluidaEm is { } concluidaEm)
        {
            contagem.Concluir(concluidaEm);
        }

        if (existente is null)
        {
            await _contagens.AdicionarAsync(contagem, cancellationToken);
            return StatusSync.Aceita;
        }

        return antes == Retrato(contagem) ? StatusSync.JaRecebida : StatusSync.Aceita;
    }

    /// <summary>Linhas e conclusão em texto, para saber se o reenvio mudou alguma coisa.</summary>
    /// <summary>
    /// Hora em que o tablet diz que contou o item, dentro da contagem (entre o início e a conclusão): relógio do tablet um
    /// pouco fora não rejeita a contagem inteira. Sem a hora, vale a conclusão (RN-ES-08).
    /// </summary>
    private static DateTime? ContadoEmNoTablet(DateTime? informado, DateTime iniciadaEm, DateTime limite)
    {
        if (informado is not { } quando)
        {
            return null;
        }

        var utc = quando.Kind == DateTimeKind.Utc ? quando : DateTime.SpecifyKind(quando, DateTimeKind.Utc);
        return utc < iniciadaEm ? iniciadaEm : utc > limite ? limite : utc;
    }

    private static string Retrato(Contagem contagem) =>
        $"{contagem.ConcluidaEm:O}|" + string.Join(";", contagem.Itens.OrderBy(i => i.ItemEstoqueId).Select(i => $"{i.ItemEstoqueId}={i.ContadoUnidades}"));

    private static bool MesmasLinhas(Contagem contagem, IReadOnlyList<LinhaContagemSyncDto> linhas, Catalogo catalogo)
    {
        foreach (var linha in linhas)
        {
            var gravada = contagem.Itens.FirstOrDefault(i => i.ItemEstoqueId == linha.ItemEstoqueId);
            int? unidades = linha.Embalagens is { } embalagens && catalogo.Itens.TryGetValue(linha.ItemEstoqueId, out var item)
                ? item.ParaUnidades(embalagens)
                : null;
            if (gravada?.ContadoUnidades != unidades)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<ContagemDto> MontarAsync(Contagem contagem, CancellationToken cancellationToken)
    {
        var catalogo = await CatalogoAsync(cancellationToken);
        // Referência e saldo esperado com o mesmo corte: as contagens concluídas antes desta (aberta: até agora).
        var anteriores = await _contagens.UltimaContagemPorItemAsync(contagem.ConcluidaEm ?? _clock.UtcNow, cancellationToken);
        var nomes = await NomesDosUsuariosAsync(cancellationToken);
        var registradas = contagem.Itens.ToDictionary(i => i.ItemEstoqueId);
        // Potes: o saldo que o sistema esperava, para conferir (RN-ES-08) — calculado agora, não guardado.
        var potes = catalogo.Itens.Values
            .Where(i => catalogo.Grupos.TryGetValue(i.GrupoId, out var g) && g.ModoControle == ModoControle.SaldoPelaVenda
                && (i.Ativo || registradas.ContainsKey(i.Id)))
            .Select(i => i.Id)
            .ToList();
        var esperados = await _saldos.SaldoEsperadoNaContagemAsync(contagem, potes, anteriores, cancellationToken);

        // Linhas: todo item ativo que entra na contagem + os que já estão na contagem (mesmo que desativados depois).
        var linhas = catalogo.Itens.Values
            .Where(i => registradas.ContainsKey(i.Id)
                || (i.Ativo && catalogo.Grupos.TryGetValue(i.GrupoId, out var g) && g.EntraNaContagem))
            .Select(i => (Item: i, Grupo: catalogo.Grupos[i.GrupoId]))
            .OrderBy(x => x.Grupo.Ordem).ThenBy(x => x.Grupo.Nome).ThenBy(x => x.Item.Ordem).ThenBy(x => x.Item.Nome)
            .Select(x =>
            {
                var registrada = registradas.GetValueOrDefault(x.Item.Id);
                int? anterior = anteriores.TryGetValue(x.Item.Id, out var a) ? a.Unidades : null;
                return new LinhaContagemDto(
                    x.Item.Id,
                    x.Grupo.Id,
                    x.Grupo.Nome,
                    x.Item.Nome,
                    x.Item.Unidade.ToString(),
                    x.Item.UnidadesPorEmbalagem,
                    x.Item.PassoEmbalagens,
                    registrada?.ContadoUnidades,
                    registrada?.ContadoUnidades is { } contado ? x.Item.EmEmbalagens(contado) : null,
                    anterior,
                    anterior is { } valor ? x.Item.EmEmbalagens(valor) : null,
                    esperados.TryGetValue(x.Item.Id, out var esperado) ? esperado : null);
            })
            .ToList();

        return new ContagemDto(
            contagem.Id,
            nomes.GetValueOrDefault(contagem.UsuarioId, "Usuário removido"),
            contagem.Origem.ToString(),
            contagem.IniciadaEm,
            contagem.ConcluidaEm,
            linhas);
    }

    private async Task<Catalogo> CatalogoAsync(CancellationToken cancellationToken)
    {
        var itens = await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken);
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        return new Catalogo(itens.ToDictionary(i => i.Id), grupos.ToDictionary(g => g.Id));
    }

    private async Task<Dictionary<Guid, string>> NomesDosUsuariosAsync(CancellationToken cancellationToken) =>
        (await _usuarios.ListarAsync(cancellationToken)).ToDictionary(u => u.Id, u => u.Nome);

    private sealed record Catalogo(Dictionary<Guid, ItemEstoque> Itens, Dictionary<Guid, GrupoEstoque> Grupos)
    {
        public (ItemEstoque Item, GrupoEstoque Grupo) Item(Guid itemId)
        {
            var item = Itens.GetValueOrDefault(itemId)
                ?? throw new DomainException("Item do catálogo não encontrado. Atualize o catálogo e conte de novo.");
            return (item, Grupos[item.GrupoId]);
        }
    }
}
