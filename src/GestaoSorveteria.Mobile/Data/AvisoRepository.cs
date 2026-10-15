using System.Collections.Concurrent;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Model;

namespace SorveteriaMaui.Data;

/// <summary>
/// Avisos "Está acabando" no SQLite (Fase 2): os criados no tablet e os abertos que vieram do servidor.
/// Alterações (aviso novo, "Ainda falta", WhatsApp aberto) entram na fila de envio (<c>pendente_envio</c>) numa transação;
/// o <c>SyncService</c> manda o estado atual de cada um para <c>POST /api/sync/avisos</c> (RN-AV-09).
/// </summary>
public sealed class AvisoRepository(BancoLocal banco)
{
    /// <summary>
    /// Avisos do tablet que o servidor juntou a outro aberto do mesmo item (Id local → Id do servidor). Quem guardou o Id
    /// antigo (a tela no meio de um "Ainda falta", o WhatsApp que acabou de abrir) continua achando o aviso.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, Guid> _juntados = new();

    /// <summary>"Já avisados": abertos, os recusados primeiro (precisam de atenção), depois o mais grave e o mais recente.</summary>
    public async Task<List<AvisoLocal>> ListarAbertosAsync()
    {
        var db = await banco.ConexaoAsync();
        var abertos = await db.Table<AvisoLocal>().Where(a => a.Status == StatusAviso.Aberto).ToListAsync();
        return abertos
            .OrderByDescending(a => a.EhRecusado)
            .ThenByDescending(a => a.Prioridade)
            .ThenByDescending(a => a.AtualizadoEm)
            .ToList();
    }

    public async Task<int> ContarAbertosAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<AvisoLocal>().Where(a => a.Status == StatusAviso.Aberto).CountAsync();
    }

    public async Task<AvisoLocal?> ObterAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        return await db.FindAsync<AvisoLocal>(Resolver(id));
    }

    /// <summary>RN-AV-03: aviso aberto (não recusado) do item, se houver — o mais recente.</summary>
    public async Task<AvisoLocal?> ObterAbertoDoItemAsync(Guid itemEstoqueId)
    {
        var abertos = await ListarAbertosAsync();
        return abertos
            .Where(a => a.ItemEstoqueId == itemEstoqueId && !a.EhRecusado)
            .OrderByDescending(a => a.AtualizadoEm)
            .FirstOrDefault();
    }

    /// <summary>
    /// Aviso novo, já na fila. O aviso recusado do mesmo item (ou o que o atendente escolheu "Avisar de novo") sai da lista
    /// na mesma transação: o aviso novo é a resposta a ele.
    /// </summary>
    public async Task InserirAsync(AvisoLocal aviso, Guid? substituiRecusadoId)
    {
        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(tx =>
        {
            tx.Insert(aviso);
            if (aviso.ItemEstoqueId is { } itemId)
            {
                tx.Execute(
                    "UPDATE avisos SET status = ? WHERE item_estoque_id = ? AND rejeitado_motivo IS NOT NULL AND id <> ?",
                    (int)StatusAviso.Fechado, itemId, aviso.Id);
            }

            if (substituiRecusadoId is { } recusado)
            {
                tx.Execute("UPDATE avisos SET status = ? WHERE id = ? AND rejeitado_motivo IS NOT NULL", (int)StatusAviso.Fechado, recusado);
            }
        });
    }

    /// <summary>
    /// Mudança feita no aparelho ("Ainda falta", WhatsApp aberto): lê, aplica <paramref name="alteracao"/> (que pode recusar
    /// com <see cref="DomainException"/>, e aí nada é gravado), sobe a versão e volta para a fila — tudo numa transação.
    /// <paramref name="substituiRecusadoId"/>: o recusado que o atendente escolheu "Avisar de novo" sai da lista junto.
    /// </summary>
    public async Task<AvisoLocal> AlterarAsync(Guid id, Action<AvisoLocal> alteracao, Guid? substituiRecusadoId = null)
    {
        var db = await banco.ConexaoAsync();
        AvisoLocal? alterado = null;
        await db.RunInTransactionAsync(tx =>
        {
            var linha = tx.Find<AvisoLocal>(Resolver(id))
                ?? throw new DomainException("Este aviso não está mais no tablet. Volte e abra a tela de novo.");
            alteracao(linha);
            linha.Versao++;
            linha.PendenteEnvio = true;
            linha.UltimoErroEnvio = null;
            tx.Update(linha);
            if (substituiRecusadoId is { } recusado && recusado != linha.Id)
            {
                tx.Execute("UPDATE avisos SET status = ? WHERE id = ? AND rejeitado_motivo IS NOT NULL", (int)StatusAviso.Fechado, recusado);
            }

            alterado = linha;
        });

        return alterado!;
    }

    /// <summary>Recusado que o atendente já viu e tirou da lista. Só no tablet: nada vai para o servidor.</summary>
    public async Task DispensarRecusadoAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync("UPDATE avisos SET status = ? WHERE id = ? AND rejeitado_motivo IS NOT NULL", (int)StatusAviso.Fechado, Resolver(id));
    }

    // ---------------------------------------------------------------- fila de envio (RN-AV-09)

    public async Task<List<AvisoLocal>> ListarPendentesEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<AvisoLocal>()
            .Where(a => a.PendenteEnvio == true)
            .OrderBy(a => a.AtualizadoEm)
            .ToListAsync();
    }

    public async Task<int> ContarPendentesEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<AvisoLocal>().Where(a => a.PendenteEnvio == true).CountAsync();
    }

    /// <summary>
    /// <c>aceita</c> / <c>ja_recebida</c>. Sai da fila se não mudou desde o envio (<paramref name="versaoEnviada"/>).
    /// <paramref name="idNoServidor"/> diferente: o servidor juntou este aviso a outro aberto do mesmo item e o tablet passa
    /// a usar o Id de lá. <paramref name="aberto"/> = false: a dona (ou a entrega) já fechou — sai da lista.
    /// </summary>
    public async Task MarcarEnviadoAsync(Guid id, int versaoEnviada, Guid? idNoServidor, bool aberto)
    {
        var db = await banco.ConexaoAsync();
        var juntadoA = idNoServidor is { } outro && outro != Guid.Empty && outro != id ? outro : (Guid?)null;
        var achou = false;
        await db.RunInTransactionAsync(tx =>
        {
            var linha = tx.Find<AvisoLocal>(id);
            if (linha is null)
            {
                return;
            }

            achou = true;
            if (linha.Versao == versaoEnviada)
            {
                linha.PendenteEnvio = false;
                linha.EnviadoEm = DateTime.UtcNow;
                linha.UltimoErroEnvio = null;
                linha.RejeitadoMotivo = null;
                if (!aberto)
                {
                    linha.Status = StatusAviso.Fechado;
                }
            }

            if (juntadoA is { } novoId)
            {
                // Chave primária não muda no lugar: sai a linha antiga, entra a mesma com o Id do servidor
                // (se o aviso de lá já estava no cache, fica com o estado do tablet até a próxima atualização da lista).
                tx.Delete<AvisoLocal>(id);
                linha.Id = novoId;
                tx.InsertOrReplace(linha);
            }
            else
            {
                tx.Update(linha);
            }
        });

        if (achou && juntadoA is { } juntado)
        {
            _juntados[id] = juntado;
        }
    }

    /// <summary><c>rejeitada</c>: sai da fila com o motivo visível (laranja na lista). Se mudou durante o envio, tenta de novo.</summary>
    public async Task MarcarRecusadoAsync(Guid id, int versaoEnviada, string motivo)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE avisos SET pendente_envio = 0, rejeitado_motivo = ? WHERE id = ? AND versao = ?", motivo, id, versaoEnviada);
    }

    /// <summary>O servidor não devolveu resultado para o aviso: continua pendente, só anota.</summary>
    public async Task RegistrarFalhaEnvioAsync(Guid id, string erro)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync("UPDATE avisos SET ultimo_erro_envio = ? WHERE id = ?", erro, id);
    }

    // ---------------------------------------------------------------- avisos abertos do servidor

    /// <summary>
    /// Troca os abertos do cache pelos que o servidor devolveu (<c>GET /api/avisos?abertos=true</c>), sem perder nada do tablet:
    /// aviso <b>pendente de envio</b>, <b>recusado</b> ainda na lista, ou confirmado depois de <paramref name="consultadoEmUtc"/>
    /// (a resposta pode não ter visto o envio) fica como está. Aberto no tablet que sumiu do servidor foi fechado: sai da lista.
    /// </summary>
    public async Task AplicarAbertosDoServidorAsync(IReadOnlyList<AvisoLocal> abertosNoServidor, DateTime consultadoEmUtc)
    {
        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(tx =>
        {
            var servidor = abertosNoServidor.GroupBy(a => a.Id).ToDictionary(g => g.Key, g => g.First());

            foreach (var local in tx.Table<AvisoLocal>().Where(a => a.Status == StatusAviso.Aberto).ToList())
            {
                if (!servidor.ContainsKey(local.Id) && !Protegido(local, consultadoEmUtc))
                {
                    local.Status = StatusAviso.Fechado;
                    tx.Update(local);
                }
            }

            foreach (var aviso in servidor.Values)
            {
                var existente = tx.Find<AvisoLocal>(aviso.Id);
                if (existente is not null && Protegido(existente, consultadoEmUtc))
                {
                    continue;
                }

                aviso.Versao = existente?.Versao ?? 0;
                aviso.CriadoPorUsuarioId ??= existente?.CriadoPorUsuarioId;
                aviso.EnviadoEm = existente?.EnviadoEm;
                tx.InsertOrReplace(aviso);
            }
        });
    }

    private static bool Protegido(AvisoLocal aviso, DateTime consultadoEmUtc) =>
        aviso.PendenteEnvio
        || (aviso.EhRecusado && aviso.Status == StatusAviso.Aberto)
        || aviso.EnviadoEm >= consultadoEmUtc;

    private Guid Resolver(Guid id)
    {
        for (var i = 0; i < 10 && _juntados.TryGetValue(id, out var novo); i++)
        {
            id = novo;
        }

        return id;
    }
}
