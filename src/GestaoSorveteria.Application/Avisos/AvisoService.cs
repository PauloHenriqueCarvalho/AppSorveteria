using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Contracts.Avisos;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Comandas;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Application.Avisos;

/// <summary>
/// Avisos "Está acabando" (docs/10 §2.2–2.3, RN-AV-01…08): criar sem duplicar, "ainda falta", fechar, a mensagem urgente
/// e o resumo do dia para o WhatsApp da dona, e a sincronização do tablet.
/// </summary>
public sealed class AvisoService
{
    /// <summary>A lista do painel mostra os fechados dos últimos dias junto com os abertos.</summary>
    public const int DiasDeFechadosNaLista = 30;

    /// <summary>Folga para o relógio do tablet adiantado; além disso a data é recusada (senão travaria as atualizações seguintes).</summary>
    public static readonly TimeSpan FolgaRelogio = TimeSpan.FromMinutes(10);

    private readonly IAvisoRepository _avisos;
    private readonly IEstoqueRepository _estoque;
    private readonly IUsuarioRepository _usuarios;
    private readonly IConfiguracaoLojaRepository _configuracao;
    private readonly IComandaRepository _comandas;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public AvisoService(
        IAvisoRepository avisos,
        IEstoqueRepository estoque,
        IUsuarioRepository usuarios,
        IConfiguracaoLojaRepository configuracao,
        IComandaRepository comandas,
        IUnitOfWork uow,
        IClock clock)
    {
        _avisos = avisos;
        _estoque = estoque;
        _usuarios = usuarios;
        _configuracao = configuracao;
        _comandas = comandas;
        _uow = uow;
        _clock = clock;
    }

    /// <summary>Abertos primeiro (urgente → média, mais recente primeiro); com <paramref name="somenteAbertos"/> = false, também os fechados dos últimos 30 dias.</summary>
    public async Task<IReadOnlyList<AvisoDto>> ListarAsync(bool somenteAbertos, CancellationToken cancellationToken = default)
    {
        var fechadosDesde = somenteAbertos ? (DateTime?)null : _clock.UtcNow.AddDays(-DiasDeFechadosNaLista);
        var avisos = await _avisos.ListarAsync(fechadosDesde, cancellationToken);
        var nomes = await NomesAsync(cancellationToken);
        return avisos
            .OrderByDescending(a => a.Aberto)
            .ThenByDescending(a => a.Aberto ? (int)a.Prioridade : 0)
            .ThenByDescending(a => a.Aberto ? a.AtualizadoEm : a.FechadoEm ?? a.AtualizadoEm)
            .Select(a => nomes.Dto(a))
            .ToList();
    }

    /// <summary>
    /// RN-AV-01/03. Item com aviso aberto não ganha outro: o aberto recebe a quantidade nova e fica com a maior prioridade
    /// (<see cref="AvisoSalvoResponse.JaExistia"/>). Reenviar o mesmo <see cref="CriarAvisoRequest.Id"/> devolve o aviso já gravado.
    /// </summary>
    public async Task<AvisoSalvoResponse> CriarAsync(CriarAvisoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var prioridade = TextoEnum.Converter<PrioridadeAviso>(request.Prioridade, "Prioridade");
        var agora = _clock.UtcNow;

        if (request.Id is { } idInformado && idInformado != Guid.Empty
            && await _avisos.ObterPorIdAsync(idInformado, cancellationToken) is { } repetido)
        {
            // Reenvio (a primeira resposta pode não ter chegado): o link volta enquanto ninguém abriu o WhatsApp.
            return await RespostaAsync(repetido, jaExistia: true, enviarAgora: AindaSemWhatsApp(repetido), cancellationToken);
        }

        Aviso aviso;
        bool jaExistia;
        bool enviarAgora;
        if (request.ItemEstoqueId is { } itemId)
        {
            await ExigirItemAtivoAsync(itemId, cancellationToken);
            var aberto = await _avisos.ObterAbertoDoItemAsync(itemId, cancellationToken);
            if (aberto is not null)
            {
                var virouUrgente = aberto.ReceberAtualizacao(
                    request.QuantidadeAtual, prioridade, request.Observacao, usuarioId, agora, aplicarMesmoSeMaisAntiga: true);
                enviarAgora = virouUrgente || AindaSemWhatsApp(aberto);
                aviso = aberto;
                jaExistia = true;
            }
            else
            {
                aviso = Aviso.DoItem(itemId, request.QuantidadeAtual, prioridade, request.Observacao, usuarioId, agora, request.Id);
                await _avisos.AdicionarAsync(aviso, cancellationToken);
                jaExistia = false;
                enviarAgora = prioridade == PrioridadeAviso.Urgente;
            }
        }
        else
        {
            aviso = Aviso.DeTextoLivre(request.TextoLivre ?? string.Empty, request.QuantidadeAtual, prioridade, request.Observacao, usuarioId, agora, request.Id);
            await _avisos.AdicionarAsync(aviso, cancellationToken);
            jaExistia = false;
            enviarAgora = prioridade == PrioridadeAviso.Urgente;
        }

        await _uow.SaveChangesAsync(cancellationToken);
        return await RespostaAsync(aviso, jaExistia, enviarAgora, cancellationToken);
    }

    /// <summary>RN-AV-03 "Ainda falta": atualiza o aviso aberto; baixar a prioridade é recusado.</summary>
    public async Task<AvisoSalvoResponse> AindaFaltaAsync(Guid id, AindaFaltaRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var aviso = await CarregarAsync(id, cancellationToken);
        var prioridade = TextoEnum.Converter<PrioridadeAviso>(request.Prioridade, "Prioridade");
        var virouUrgente = aviso.AindaFalta(request.QuantidadeAtual, prioridade, request.Observacao, usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return await RespostaAsync(aviso, jaExistia: true, enviarAgora: virouUrgente, cancellationToken);
    }

    /// <summary>RN-AV-05b: a dona fecha com motivo ("comprei no mercado").</summary>
    public async Task<AvisoDto> FecharAsync(Guid id, FecharAvisoRequest request, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var aviso = await CarregarAsync(id, cancellationToken);
        aviso.Fechar(request.Motivo, usuarioId, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return (await NomesAsync(cancellationToken)).Dto(aviso);
    }

    /// <summary>RN-AV-08: o link do WhatsApp foi aberto (não quer dizer que a mensagem foi enviada). Guarda só a primeira vez.</summary>
    public async Task<AvisoDto> MarcarWhatsAppAbertoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var aviso = await CarregarAsync(id, cancellationToken);
        if (aviso.WhatsAppAbertoEm is null)
        {
            aviso.MarcarWhatsAppAberto(_clock.UtcNow);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return (await NomesAsync(cancellationToken)).Dto(aviso);
    }

    /// <summary>
    /// RN-AV-07: urgentes do dia (e os urgentes ainda abertos de antes), altas e médias abertas, e o total vendido no dia.
    /// Sem <paramref name="dia"/> = hoje no horário da loja.
    /// </summary>
    public async Task<ResumoDoDiaResponse> ResumoDoDiaAsync(DateOnly? dia, CancellationToken cancellationToken = default)
    {
        var data = dia ?? DiaComercial.DataDe(_clock.UtcNow);
        PeriodoConsulta.Validar(data, data, _clock.UtcNow, maximoDias: 1);
        var (inicioUtc, fimUtc) = DiaComercial.IntervaloUtc(data);

        var abertos = await _avisos.ListarAsync(fechadosDesdeUtc: null, cancellationToken);
        var doDia = await _avisos.ListarAtualizadosEntreAsync(inicioUtc, fimUtc, cancellationToken);
        var nomes = await NomesAsync(cancellationToken);

        // Abertos criados depois do dia consultado não entram no resumo de um dia passado.
        // Urgentes: os do dia (criados, atualizados ou fechados nele — fechados aparecem como resolvidos) e os ainda abertos.
        var abertosAteODia = abertos.Where(a => a.CriadoEm < fimUtc).ToList();
        var urgentes = abertosAteODia.Concat(doDia)
            .Where(a => a.Prioridade == PrioridadeAviso.Urgente)
            .DistinctBy(a => a.Id)
            .OrderBy(a => a.CriadoEm)
            .ToList();
        var altas = abertosAteODia.Where(a => a.Prioridade == PrioridadeAviso.Alta).OrderBy(a => a.CriadoEm).ToList();
        var medias = abertosAteODia.Where(a => a.Prioridade == PrioridadeAviso.Media).OrderBy(a => a.CriadoEm).ToList();

        // RN-RL-01: faturamento = comandas fechadas no dia (estornadas ficam de fora).
        var vendas = await _comandas.ListarFechadasAsync(inicioUtc, fimUtc, cancellationToken);
        var total = Moeda.Arredondar(vendas.Where(c => c.Status == StatusComanda.Fechada).Sum(c => c.Total));

        var configuracao = await ConfiguracaoAsync(cancellationToken);
        var texto = MensagensWhatsApp.ResumoDoDia(
            configuracao.NomeLoja,
            data,
            urgentes.Select(a => nomes.Linha(a) with { Resolvido = !a.Aberto }).ToList(),
            altas.Select(nomes.Linha).ToList(),
            medias.Select(nomes.Linha).ToList(),
            total);

        return new ResumoDoDiaResponse(
            data,
            texto,
            MensagensWhatsApp.Link(configuracao.WhatsAppDona, texto),
            urgentes.Count,
            altas.Count,
            medias.Count);
    }

    // ---------------------------------------------------------------- configuração da loja

    public async Task<ConfiguracaoLojaDto> ObterConfiguracaoAsync(CancellationToken cancellationToken = default)
    {
        var configuracao = await ConfiguracaoAsync(cancellationToken);
        return new ConfiguracaoLojaDto(configuracao.NomeLoja, configuracao.WhatsAppDona);
    }

    /// <summary>Só Admin (controller). Cria a linha na primeira vez.</summary>
    public async Task<ConfiguracaoLojaDto> SalvarConfiguracaoAsync(SalvarConfiguracaoLojaRequest request, CancellationToken cancellationToken = default)
    {
        var configuracao = await _configuracao.ObterAsync(cancellationToken);
        if (configuracao is null)
        {
            configuracao = ConfiguracaoLoja.Padrao(_clock.UtcNow);
            await _configuracao.AdicionarAsync(configuracao, cancellationToken);
        }

        configuracao.Atualizar(request.NomeLoja, request.WhatsAppDona, _clock.UtcNow);
        await _uow.SaveChangesAsync(cancellationToken);
        return new ConfiguracaoLojaDto(configuracao.NomeLoja, configuracao.WhatsAppDona);
    }

    // ---------------------------------------------------------------- sincronização do tablet

    /// <summary>
    /// POST /api/sync/avisos. Cada aviso é gravado separado (um rejeitado não derruba os outros):
    /// <list type="bullet">
    /// <item>Id novo e o item sem aviso aberto → cria (<c>aceita</c>);</item>
    /// <item>Id novo e o item já com aviso aberto (do painel ou de outro aparelho) → junta no aberto (<c>aceita</c>, <c>AvisoId</c> = o aberto);</item>
    /// <item>Id conhecido e aberto → recebe a atualização se for mais nova, prioridade nunca baixa (<c>aceita</c> ou <c>ja_recebida</c>);</item>
    /// <item>Id conhecido e já fechado pela dona → <c>ja_recebida</c> com <c>Aberto</c> = false.</item>
    /// </list>
    /// </summary>
    public async Task<SyncAvisosResponse> ReceberSyncAsync(SyncAvisosRequest request, CancellationToken cancellationToken = default)
    {
        var resultados = new List<ResultadoSyncAvisoDto>(request.Avisos.Count);
        var usuariosConhecidos = new HashSet<Guid>();

        foreach (var dto in request.Avisos)
        {
            if (dto is null)
            {
                resultados.Add(new ResultadoSyncAvisoDto(Guid.Empty, StatusSync.Rejeitada, "Aviso vazio no lote.", null, null));
                continue;
            }

            try
            {
                var (status, aviso) = await ReceberAvisoAsync(dto, usuariosConhecidos, cancellationToken);
                if (status == StatusSync.Aceita)
                {
                    await _uow.SaveChangesAsync(cancellationToken);
                }

                resultados.Add(new ResultadoSyncAvisoDto(dto.Id, status, null, aviso.Id, aviso.Aberto));
            }
            catch (DomainException ex)
            {
                _uow.DescartarAlteracoes();
                resultados.Add(new ResultadoSyncAvisoDto(dto.Id, StatusSync.Rejeitada, ex.Message, null, null));
            }
        }

        return new SyncAvisosResponse(resultados);
    }

    private async Task<(string Status, Aviso Aviso)> ReceberAvisoAsync(
        AvisoSyncDto dto, HashSet<Guid> usuariosConhecidos, CancellationToken cancellationToken)
    {
        Exigir.Que(dto.Id != Guid.Empty, "Aviso sem Id.");
        var prioridade = TextoEnum.Converter<PrioridadeAviso>(dto.Prioridade, "Prioridade");
        await ExigirUsuarioAsync(dto.CriadoPorUsuarioId, usuariosConhecidos, cancellationToken);
        var atualizadoPor = dto.AtualizadoPorUsuarioId ?? dto.CriadoPorUsuarioId;
        await ExigirUsuarioAsync(atualizadoPor, usuariosConhecidos, cancellationToken);
        Exigir.Que(dto.AtualizadoEm >= dto.CriadoEm, "A atualização do aviso não pode ser anterior à criação.");
        var limite = _clock.UtcNow + FolgaRelogio;
        Exigir.Que(dto.AtualizadoEm <= limite && (dto.WhatsAppAbertoEm ?? dto.CriadoEm) <= limite,
            "A data do aviso está no futuro: confira o relógio do tablet.");

        var existente = await _avisos.ObterPorIdAsync(dto.Id, cancellationToken);
        if (existente is not null)
        {
            Exigir.Que(existente.ItemEstoqueId == dto.ItemEstoqueId
                    && (dto.ItemEstoqueId is not null || existente.TextoLivre == dto.TextoLivre?.Trim()),
                "Já existe um aviso com este Id para outro item: nada foi alterado.");

            if (!existente.Aberto)
            {
                // O tablet mexeu no aviso (offline) depois que a dona fechou: a informação nova não pode sumir em silêncio.
                Exigir.Que(dto.AtualizadoEm <= existente.AtualizadoEm || dto.AtualizadoEm <= existente.FechadoEm,
                    $"A dona fechou este aviso às {HoraNaLoja(existente.FechadoEm!.Value)}. Se ainda está faltando, avise de novo.");
                return (StatusSync.JaRecebida, existente);
            }

            var mudou = AplicarAtualizacao(existente, dto, prioridade, atualizadoPor);
            return (mudou ? StatusSync.Aceita : StatusSync.JaRecebida, existente);
        }

        if (dto.ItemEstoqueId is { } itemId)
        {
            Exigir.Que(await _estoque.ObterItemAsync(itemId, cancellationToken) is not null,
                "Item do catálogo não encontrado. Atualize o catálogo do tablet e avise de novo.");
            if (await _avisos.ObterAbertoDoItemAsync(itemId, cancellationToken) is { } aberto)
            {
                AplicarAtualizacao(aberto, dto, prioridade, atualizadoPor);
                return (StatusSync.Aceita, aberto);
            }

            // O aviso do tablet pode ter sido juntado a outro que depois foi fechado (pela dona ou pela entrega), e a resposta
            // não chegou ao tablet. Se tudo o que o tablet sabe é anterior ao fechamento, ele já foi resolvido: não reabre.
            if (await _avisos.ObterUltimoFechadoDoItemAsync(itemId, cancellationToken) is { } fechado
                && fechado.FechadoEm >= dto.AtualizadoEm)
            {
                return (StatusSync.JaRecebida, fechado);
            }
        }

        var aviso = dto.ItemEstoqueId is { } item
            ? Aviso.DoItem(item, dto.QuantidadeAtual, prioridade, dto.Observacao, dto.CriadoPorUsuarioId, dto.CriadoEm, dto.Id)
            : Aviso.DeTextoLivre(dto.TextoLivre ?? string.Empty, dto.QuantidadeAtual, prioridade, dto.Observacao, dto.CriadoPorUsuarioId, dto.CriadoEm, dto.Id);
        if (dto.AtualizadoEm > dto.CriadoEm)
        {
            aviso.ReceberAtualizacao(dto.QuantidadeAtual, prioridade, dto.Observacao, atualizadoPor, dto.AtualizadoEm);
        }

        if (dto.WhatsAppAbertoEm is { } aberturaWhatsApp)
        {
            aviso.MarcarWhatsAppAberto(aberturaWhatsApp);
        }

        await _avisos.AdicionarAsync(aviso, cancellationToken);
        return (StatusSync.Aceita, aviso);
    }

    /// <summary>Aplica o estado do tablet; devolve se alguma coisa mudou (para responder <c>ja_recebida</c> num reenvio igual).</summary>
    private static bool AplicarAtualizacao(Aviso aviso, AvisoSyncDto dto, PrioridadeAviso prioridade, Guid atualizadoPor)
    {
        var antes = (aviso.QuantidadeAtual, aviso.Prioridade, aviso.Observacao, aviso.AtualizadoEm, aviso.WhatsAppAbertoEm);
        aviso.ReceberAtualizacao(dto.QuantidadeAtual, prioridade, dto.Observacao, atualizadoPor, dto.AtualizadoEm);
        if (dto.WhatsAppAbertoEm is { } aberturaWhatsApp)
        {
            aviso.MarcarWhatsAppAberto(aberturaWhatsApp);
        }

        return antes != (aviso.QuantidadeAtual, aviso.Prioridade, aviso.Observacao, aviso.AtualizadoEm, aviso.WhatsAppAbertoEm);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<AvisoSalvoResponse> RespostaAsync(Aviso aviso, bool jaExistia, bool enviarAgora, CancellationToken cancellationToken)
    {
        var nomes = await NomesAsync(cancellationToken);
        var dto = nomes.Dto(aviso);
        if (!enviarAgora)
        {
            return new AvisoSalvoResponse(dto, jaExistia, EnviarAgora: false, MensagemWhatsApp: null, LinkWhatsApp: null);
        }

        var configuracao = await ConfiguracaoAsync(cancellationToken);
        var mensagem = MensagensWhatsApp.Urgente(
            configuracao.NomeLoja,
            dto.Descricao,
            aviso.QuantidadeAtual,
            aviso.Observacao,
            dto.AtualizadoPorNome ?? dto.CriadoPorNome,
            TimeZoneInfo.ConvertTimeFromUtc(aviso.AtualizadoEm, DiaComercial.Fuso));
        return new AvisoSalvoResponse(dto, jaExistia, EnviarAgora: true, mensagem, MensagensWhatsApp.Link(configuracao.WhatsAppDona, mensagem));
    }

    /// <summary>Urgente cujo WhatsApp ninguém abriu ainda: quem chamou deve abrir o link (RN-AV-06).</summary>
    private static bool AindaSemWhatsApp(Aviso aviso) =>
        aviso.Aberto && aviso.Prioridade == PrioridadeAviso.Urgente && aviso.WhatsAppAbertoEm is null;

    private static string HoraNaLoja(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, DiaComercial.Fuso).ToString("dd/MM HH:mm", System.Globalization.CultureInfo.InvariantCulture);

    private async Task<ConfiguracaoLoja> ConfiguracaoAsync(CancellationToken cancellationToken) =>
        await _configuracao.ObterAsync(cancellationToken) ?? ConfiguracaoLoja.Padrao(_clock.UtcNow);

    private async Task<Aviso> CarregarAsync(Guid id, CancellationToken cancellationToken) =>
        await _avisos.ObterPorIdAsync(id, cancellationToken) ?? throw new EntidadeNaoEncontradaException("Aviso", id);

    private async Task ExigirItemAtivoAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = await _estoque.ObterItemAsync(itemId, cancellationToken)
            ?? throw new DomainException("Item do catálogo não encontrado. Escolha um item da lista.");
        Exigir.Que(item.Ativo, $"{item.Nome} está desativado no catálogo.");
    }

    private async Task ExigirUsuarioAsync(Guid usuarioId, HashSet<Guid> conhecidos, CancellationToken cancellationToken)
    {
        if (conhecidos.Contains(usuarioId))
        {
            return;
        }

        Exigir.Que(await _usuarios.ObterPorIdAsync(usuarioId, cancellationToken) is not null,
            "Usuário do aviso não encontrado no servidor.");
        conhecidos.Add(usuarioId);
    }

    /// <summary>Nomes de itens, grupos, fornecedores e usuários para montar os DTOs sem uma consulta por aviso.</summary>
    private async Task<Nomes> NomesAsync(CancellationToken cancellationToken)
    {
        var itens = await _estoque.ListarItensAsync(somenteAtivos: false, cancellationToken);
        var grupos = await _estoque.ListarGruposAsync(cancellationToken);
        var fornecedores = await _estoque.ListarFornecedoresAsync(cancellationToken);
        var usuarios = await _usuarios.ListarAsync(cancellationToken);
        return new Nomes(
            itens.ToDictionary(i => i.Id),
            grupos.ToDictionary(g => g.Id, g => g.Nome),
            fornecedores.ToDictionary(f => f.Id, f => f.Nome),
            usuarios.ToDictionary(u => u.Id, u => u.Nome));
    }

    private sealed record Nomes(
        Dictionary<Guid, ItemEstoque> Itens,
        Dictionary<Guid, string> Grupos,
        Dictionary<Guid, string> Fornecedores,
        Dictionary<Guid, string> Usuarios)
    {
        public const string Sistema = "Sistema";

        public string Descricao(Aviso aviso)
        {
            if (aviso.ItemEstoqueId is { } itemId && Itens.TryGetValue(itemId, out var item))
            {
                return MensagensWhatsApp.DescricaoDoItem(Grupos.GetValueOrDefault(item.GrupoId), item.Nome);
            }

            return aviso.TextoLivre ?? "Item removido";
        }

        public MensagensWhatsApp.LinhaAviso Linha(Aviso aviso) => new(Descricao(aviso), aviso.QuantidadeAtual);

        public AvisoDto Dto(Aviso aviso)
        {
            var item = aviso.ItemEstoqueId is { } itemId ? Itens.GetValueOrDefault(itemId) : null;
            return new AvisoDto(
                aviso.Id,
                aviso.ItemEstoqueId,
                Descricao(aviso),
                item is null ? null : Grupos.GetValueOrDefault(item.GrupoId),
                item is null ? null : Fornecedores.GetValueOrDefault(item.FornecedorId),
                aviso.QuantidadeAtual,
                aviso.Prioridade.ToString(),
                aviso.Observacao,
                Nome(aviso.CriadoPorUsuarioId),
                aviso.CriadoEm,
                aviso.AtualizadoPorUsuarioId is null && aviso.AtualizadoEm == aviso.CriadoEm ? null : Nome(aviso.AtualizadoPorUsuarioId),
                aviso.AtualizadoEm,
                aviso.Status.ToString(),
                aviso.FechadoEm,
                aviso.FechadoPorUsuarioId is null ? null : Nome(aviso.FechadoPorUsuarioId),
                aviso.MotivoFechamento,
                aviso.WhatsAppAbertoEm);
        }

        private string Nome(Guid? usuarioId) =>
            usuarioId is { } id ? Usuarios.GetValueOrDefault(id) ?? "Usuário removido" : Sistema;
    }
}
