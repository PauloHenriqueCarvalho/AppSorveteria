using GestaoSorveteria.Domain.Caixas;
using Caixa = SorveteriaMaui.Model.Caixa;
using MovimentoCaixa = SorveteriaMaui.Model.MovimentoCaixa;
using Dominio = GestaoSorveteria.Domain.Caixas;

namespace SorveteriaMaui.Data;

/// <summary>
/// Caixas no SQLite (C3). Para alterar, devolve o <see cref="Dominio.Caixa"/> remontado (abertura + movimentos,
/// como a API faz na sincronização) e grava tudo de volta numa transação.
/// </summary>
public sealed class CaixaRepository(BancoLocal banco)
{
    /// <summary>RN-CX-01: no aparelho só existe um caixa aberto (o índice em <c>status</c> ajuda a busca).</summary>
    public async Task<Caixa?> ObterAbertoLinhaAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Caixa>().Where(c => c.Status == StatusCaixa.Aberto).FirstOrDefaultAsync();
    }

    public async Task<Caixa?> ObterLinhaAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        return await db.FindAsync<Caixa>(id);
    }

    /// <summary>Último caixa fechado, para mostrar a conferência na tela.</summary>
    public async Task<Caixa?> ObterUltimoFechadoAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Caixa>()
            .Where(c => c.Status == StatusCaixa.Fechado)
            .OrderByDescending(c => c.FechadoEm)
            .FirstOrDefaultAsync();
    }

    // ---------------------------------------------------------------- fila de envio (C4)

    /// <summary>Caixas com algo a enviar (abertura/movimento/fechamento) ou com comandas pendentes (<paramref name="comComandasPendentes"/>).</summary>
    public async Task<List<Caixa>> ListarComPendenciaAsync(IReadOnlyList<Guid> comComandasPendentes)
    {
        var db = await banco.ConexaoAsync();
        var pendentes = await db.Table<Caixa>().Where(c => c.PendenteEnvio == true).ToListAsync();
        foreach (var id in comComandasPendentes.Where(id => pendentes.All(c => c.Id != id)))
        {
            var caixa = await db.FindAsync<Caixa>(id);
            if (caixa is not null)
            {
                pendentes.Add(caixa);
            }
        }

        return pendentes;
    }

    public async Task<int> ContarPendentesEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Caixa>().Where(c => c.PendenteEnvio == true).CountAsync();
    }

    public async Task MarcarEnviadoAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE caixas SET pendente_envio = 0, enviado_em = ?, rejeitado_motivo = NULL, ultimo_erro_envio = NULL WHERE id = ?",
            DateTime.UtcNow, id);
    }

    /// <summary>Rejeitado pela API (ex.: fechado à força pelo painel, RN-CX-09): sai da fila, motivo visível. Nada é apagado.</summary>
    public async Task MarcarRejeitadoAsync(Guid id, string motivo)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync("UPDATE caixas SET pendente_envio = 0, rejeitado_motivo = ? WHERE id = ?", motivo, id);
    }

    public async Task<List<MovimentoCaixa>> ListarMovimentosAsync(Guid caixaId)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<MovimentoCaixa>().Where(m => m.CaixaId == caixaId).OrderBy(m => m.Em).ToListAsync();
    }

    /// <summary>Caixa aberto como entidade do Domain (abertura + movimentos reaplicados), ou <c>null</c>.</summary>
    public async Task<Dominio.Caixa?> ObterAbertoAsync()
    {
        var linha = await ObterAbertoLinhaAsync();
        if (linha is null)
        {
            return null;
        }

        var caixa = Dominio.Caixa.Abrir(linha.AbertoPorUsuarioId, linha.FundoTroco, linha.AbertoEm, linha.Id);
        foreach (var m in await ListarMovimentosAsync(linha.Id))
        {
            if (m.Tipo == TipoMovimentoCaixa.Sangria)
            {
                caixa.RegistrarSangria(m.Valor, m.Motivo, m.UsuarioId, m.Em, m.Id);
            }
            else
            {
                caixa.RegistrarSuprimento(m.Valor, m.Motivo, m.UsuarioId, m.Em, m.Id);
            }
        }

        return caixa;
    }

    /// <summary>
    /// Grava o caixa e os movimentos novos numa transação. Movimento nunca é apagado nem alterado (RN-TD-03);
    /// caixa fechado não muda (RN-CX-07). Marca <c>pendente_envio</c> para o C4.
    /// </summary>
    public async Task GravarAsync(Dominio.Caixa caixa, bool pendenteEnvio = true)
    {
        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(tx =>
        {
            var existente = tx.Find<Caixa>(caixa.Id);
            if (existente is not null && !existente.EstaAberto)
            {
                throw new GestaoSorveteria.Domain.Common.DomainException("Este caixa já está fechado (RN-CX-07).");
            }

            // RN-CX-05 dentro da transação: uma comanda aberta no meio do fechamento não pode ficar num caixa fechado.
            if (!caixa.EstaAberto
                && tx.Table<Model.Comanda>().Where(c => c.CaixaId == caixa.Id && c.Status == GestaoSorveteria.Domain.Comandas.StatusComanda.Aberta).Count() > 0)
            {
                throw new GestaoSorveteria.Domain.Common.DomainException("Ainda há comanda aberta neste caixa. Feche ou cancele antes de fechar o caixa (RN-CX-05).");
            }

            if (existente is null && caixa.EstaAberto
                && tx.Table<Caixa>().Where(c => c.Status == StatusCaixa.Aberto).Count() > 0)
            {
                throw new GestaoSorveteria.Domain.Common.DomainException("Já existe um caixa aberto. Feche-o antes de abrir outro (RN-CX-01).");
            }

            var linha = new Caixa
            {
                Id = caixa.Id,
                AbertoPorUsuarioId = caixa.AbertoPorUsuarioId,
                AbertoEm = caixa.AbertoEm,
                FundoTroco = caixa.FundoTroco,
                Status = caixa.Status,
                FechadoPorUsuarioId = caixa.FechadoPorUsuarioId,
                FechadoEm = caixa.FechadoEm,
                TotalVendasDinheiro = caixa.TotalVendasDinheiro,
                ValorEsperado = caixa.ValorEsperado,
                ValorContado = caixa.ValorContado,
                Diferenca = caixa.Diferenca,
                Observacao = caixa.Observacao,
                PendenteEnvio = pendenteEnvio,
            };

            if (existente is null)
            {
                tx.Insert(linha);
            }
            else
            {
                tx.Update(linha);
            }

            var gravados = tx.Table<MovimentoCaixa>().Where(m => m.CaixaId == caixa.Id).ToList().Select(m => m.Id).ToHashSet();
            foreach (var movimento in caixa.Movimentos.Where(m => !gravados.Contains(m.Id)))
            {
                tx.Insert(new MovimentoCaixa
                {
                    Id = movimento.Id,
                    CaixaId = caixa.Id,
                    Tipo = movimento.Tipo,
                    Valor = movimento.Valor,
                    Motivo = movimento.Motivo,
                    UsuarioId = movimento.UsuarioId,
                    Em = movimento.Em,
                });
            }
        });
    }
}
