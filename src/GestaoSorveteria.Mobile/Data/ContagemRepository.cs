using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Estoque;
using SorveteriaMaui.Model;

namespace SorveteriaMaui.Data;

/// <summary>Última quantidade contada de um item numa contagem concluída do tablet (só referência na tela, RN-ES-06).</summary>
public sealed record ContagemAnterior(decimal Embalagens, DateTime ConcluidaEm);

/// <summary>
/// Contagem semanal no SQLite (Fase 2, RN-ES-05/06). Cada item contado grava a linha e põe a contagem na fila numa
/// transação; quem decide o valor (e recusa com <see cref="DomainException"/>) é o <c>ContagemAppService</c>, com as regras
/// do Domain. O <c>SyncService</c> manda a contagem inteira para <c>POST /api/sync/contagens</c>.
/// </summary>
public sealed class ContagemRepository(BancoLocal banco)
{
    /// <summary>Referência: as últimas contagens concluídas bastam (cada item costuma estar na mais recente).</summary>
    private const int ContagensParaReferencia = 8;

    /// <summary>A contagem em andamento no tablet (salva parcial), se houver. Só existe uma por vez.</summary>
    public async Task<ContagemLocal?> ObterEmAndamentoAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ContagemLocal>()
            .Where(c => c.ConcluidaEm == null && c.DescartadaEm == null)
            .OrderByDescending(c => c.IniciadaEm)
            .FirstOrDefaultAsync();
    }

    public async Task<ContagemLocal?> ObterAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        return await db.FindAsync<ContagemLocal>(id);
    }

    /// <summary>A última concluída no tablet (para "Última contagem: 07/10 por Ana" e para mostrar se foi recusada).</summary>
    public async Task<ContagemLocal?> ObterUltimaConcluidaAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ContagemLocal>()
            .Where(c => c.ConcluidaEm != null && c.DescartadaEm == null)
            .OrderByDescending(c => c.ConcluidaEm)
            .FirstOrDefaultAsync();
    }

    /// <summary>Lembrete (RN-ES-05): há contagem concluída a partir de <paramref name="desdeUtc"/> que não foi recusada?</summary>
    public async Task<bool> ExisteConcluidaDesdeAsync(DateTime desdeUtc)
    {
        var db = await banco.ConexaoAsync();
        var concluidas = await db.Table<ContagemLocal>()
            .Where(c => c.ConcluidaEm != null && c.DescartadaEm == null && c.ConcluidaEm >= desdeUtc)
            .ToListAsync();
        return concluidas.Any(c => !c.EhRecusada);
    }

    public async Task<List<ItemContagemLocal>> ListarLinhasAsync(Guid contagemId)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ItemContagemLocal>().Where(i => i.ContagemId == contagemId).ToListAsync();
    }

    /// <summary>
    /// RN-ES-06: por item, o último valor contado nas contagens concluídas do tablet (a mais recente primeiro).
    /// A contagem em andamento não entra (o campo dela começa vazio; a anterior é só referência).
    /// </summary>
    public async Task<Dictionary<Guid, ContagemAnterior>> ListarAnterioresPorItemAsync()
    {
        var db = await banco.ConexaoAsync();
        var concluidas = await db.Table<ContagemLocal>()
            .Where(c => c.ConcluidaEm != null && c.DescartadaEm == null)
            .OrderByDescending(c => c.ConcluidaEm)
            .Take(ContagensParaReferencia)
            .ToListAsync();

        var anteriores = new Dictionary<Guid, ContagemAnterior>();
        foreach (var contagem in concluidas)
        {
            var contagemId = contagem.Id;
            var linhas = await db.Table<ItemContagemLocal>().Where(i => i.ContagemId == contagemId).ToListAsync();
            foreach (var linha in linhas)
            {
                if (linha.Embalagens is { } embalagens && !anteriores.ContainsKey(linha.ItemEstoqueId))
                {
                    anteriores[linha.ItemEstoqueId] = new ContagemAnterior(embalagens, contagem.ConcluidaEm!.Value);
                }
            }
        }

        return anteriores;
    }

    /// <summary>
    /// Registra (ou apaga) a quantidade de um item na contagem em andamento, numa transação: lê o valor atual, chama
    /// <paramref name="calcular"/> (valor atual → novo; pode recusar com <see cref="DomainException"/>, e aí nada é gravado),
    /// grava a linha, sobe a versão e põe na fila. Sem contagem em andamento, a criada por <paramref name="nova"/> nasce aqui.
    /// <paramref name="contagemEsperadaId"/>: a que a tela está mostrando — se ela não está mais em andamento (concluída ou
    /// trocada em outro lugar), recusa em vez de gravar em outra.
    /// </summary>
    public async Task<(ContagemLocal Contagem, ItemContagemLocal Linha)> RegistrarAsync(
        Guid? contagemEsperadaId, Func<ContagemLocal> nova, Guid itemEstoqueId, Func<decimal?, decimal?> calcular)
    {
        var db = await banco.ConexaoAsync();
        ContagemLocal? contagem = null;
        ItemContagemLocal? linha = null;
        await db.RunInTransactionAsync(tx =>
        {
            var agora = DateTime.UtcNow;
            var emAndamento = tx.Table<ContagemLocal>()
                .Where(c => c.ConcluidaEm == null && c.DescartadaEm == null)
                .OrderByDescending(c => c.IniciadaEm)
                .FirstOrDefault();

            if (contagemEsperadaId is { } esperada && emAndamento?.Id != esperada)
            {
                throw new DomainException("Esta contagem já foi concluída ou trocada. Volte e abra a contagem de novo.");
            }

            var criada = emAndamento is null;
            contagem = emAndamento ?? nova();
            var contagemId = contagem.Id;
            linha = tx.Table<ItemContagemLocal>().FirstOrDefault(i => i.ContagemId == contagemId && i.ItemEstoqueId == itemEstoqueId);

            var valorAnterior = linha?.Embalagens;
            var novoValor = calcular(valorAnterior);

            // RN-ES-06/08: a hora do item é a de quando o número mudou (nunca antes do início da contagem, que pode ter
            // nascido agora mesmo, um instante depois de "agora"); apagado volta a "sem hora".
            var contadoAgora = agora < contagem.IniciadaEm ? contagem.IniciadaEm : agora;
            if (linha is null)
            {
                linha = new ItemContagemLocal { ContagemId = contagemId, ItemEstoqueId = itemEstoqueId };
                linha.Embalagens = novoValor;
                linha.AlteradoEm = agora;
                linha.ContadoEm = novoValor is null ? null : contadoAgora;
                tx.Insert(linha);
            }
            else
            {
                // Mesmo número de novo: fica a hora que já tinha (linha de antes desta versão: a do último toque).
                linha.ContadoEm = novoValor is null ? null
                    : novoValor != valorAnterior ? contadoAgora
                    : linha.ContadoEm ?? linha.AlteradoEm;
                linha.Embalagens = novoValor;
                linha.AlteradoEm = agora;
                tx.Update(linha);
            }

            contagem.AlteradaEm = agora;
            contagem.Versao++;
            contagem.PendenteEnvio = true;
            contagem.UltimoErroEnvio = null;
            contagem.RejeitadoMotivo = null; // mudou depois da recusa: tenta de novo (se recusar outra vez, o motivo volta)
            if (criada)
            {
                tx.Insert(contagem);
            }
            else
            {
                tx.Update(contagem);
            }
        });

        return (contagem!, linha!);
    }

    /// <summary>
    /// Conclui numa transação: <paramref name="concluir"/> recebe a contagem e as linhas, aplica as regras do Domain
    /// (pelo menos um item contado…) e devolve a hora da conclusão; recusa com <see cref="DomainException"/>.
    /// </summary>
    public async Task<ContagemLocal> ConcluirAsync(Guid contagemId, Func<ContagemLocal, IReadOnlyList<ItemContagemLocal>, DateTime> concluir)
    {
        var db = await banco.ConexaoAsync();
        ContagemLocal? contagem = null;
        await db.RunInTransactionAsync(tx =>
        {
            contagem = tx.Find<ContagemLocal>(contagemId)
                ?? throw new DomainException("Esta contagem não está mais no tablet. Volte e abra a contagem de novo.");
            if (!contagem.EstaEmAndamento)
            {
                throw new DomainException("Esta contagem já foi concluída e não muda mais. Faça uma nova contagem.");
            }

            var linhas = tx.Table<ItemContagemLocal>().Where(i => i.ContagemId == contagemId).ToList();
            contagem.ConcluidaEm = concluir(contagem, linhas);
            contagem.AlteradaEm = DateTime.UtcNow;
            contagem.Versao++;
            contagem.PendenteEnvio = true;
            contagem.UltimoErroEnvio = null;
            contagem.RejeitadoMotivo = null;
            tx.Update(contagem);
        });

        return contagem!;
    }

    /// <summary>
    /// Deixa de lado a contagem em andamento (recusada pelo servidor ou esquecida de outro dia) para começar outra.
    /// Nada é apagado: ela sai da tela e da fila.
    /// </summary>
    public async Task DescartarAsync(Guid contagemId)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE contagens SET descartada_em = ?, pendente_envio = 0 WHERE id = ? AND concluida_em IS NULL AND descartada_em IS NULL",
            DateTime.UtcNow, contagemId);
    }

    // ---------------------------------------------------------------- fila de envio

    public async Task<List<ContagemLocal>> ListarPendentesEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ContagemLocal>()
            .Where(c => c.PendenteEnvio == true)
            .OrderBy(c => c.IniciadaEm)
            .ToListAsync();
    }

    public async Task<int> ContarPendentesEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ContagemLocal>().Where(c => c.PendenteEnvio == true).CountAsync();
    }

    /// <summary>
    /// Linhas que vão no envio: as registradas cujo item ainda entra na contagem. Item que a dona passou para um grupo
    /// "só aviso" (copos, insumos) depois de contado sai do envio — o servidor recusaria a contagem inteira por ele.
    /// </summary>
    public async Task<List<ItemContagemLocal>> ListarLinhasParaEnvioAsync(Guid contagemId)
    {
        var db = await banco.ConexaoAsync();
        var linhas = await db.Table<ItemContagemLocal>().Where(i => i.ContagemId == contagemId).ToListAsync();
        var foraDaContagem = (await db.Table<ItemEstoqueLocal>().Where(i => i.ModoControleGrupo == ModoControle.SoAviso).ToListAsync())
            .Select(i => i.Id)
            .ToHashSet();
        return linhas.Where(l => !foraDaContagem.Contains(l.ItemEstoqueId)).ToList();
    }

    /// <summary><c>aceita</c> / <c>ja_recebida</c>: sai da fila se não mudou desde o envio (<paramref name="versaoEnviada"/>).</summary>
    public async Task MarcarEnviadoAsync(Guid id, int versaoEnviada)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE contagens SET pendente_envio = 0, enviado_em = ?, ultimo_erro_envio = NULL, rejeitado_motivo = NULL WHERE id = ? AND versao = ?",
            DateTime.UtcNow, id, versaoEnviada);
    }

    /// <summary><c>rejeitada</c>: sai da fila com o motivo visível (laranja na tela). Se mudou durante o envio, tenta de novo.</summary>
    public async Task MarcarRecusadoAsync(Guid id, int versaoEnviada, string motivo)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE contagens SET pendente_envio = 0, rejeitado_motivo = ? WHERE id = ? AND versao = ?", motivo, id, versaoEnviada);
    }

    /// <summary>O servidor não devolveu resultado para a contagem: continua pendente, só anota.</summary>
    public async Task RegistrarFalhaEnvioAsync(Guid id, string erro)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync("UPDATE contagens SET ultimo_erro_envio = ? WHERE id = ?", erro, id);
    }
}
