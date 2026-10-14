using GestaoSorveteria.Domain.Common;
using SorveteriaMaui.Model;
using Dominio = GestaoSorveteria.Domain.Comandas;

namespace SorveteriaMaui.Data;

/// <summary>Vendas de um caixa para a tela e para o fechamento (C3). Só comandas fechadas contam; abertas só são contadas.</summary>
public sealed record ResumoVendasCaixa(decimal TotalDinheiro, decimal TotalVendido, int ComandasFechadas, int ComandasAbertas);

/// <summary>
/// Comandas no SQLite. Leitura para a tela devolve as linhas; para alterar, devolve a comanda do Domain
/// (<see cref="Dominio.Comanda.Restaurar"/>) e grava tudo de volta numa transação só.
/// </summary>
public sealed class ComandaRepository(BancoLocal banco)
{
    public async Task<List<Comanda>> ListarAbertasAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Comanda>()
            .Where(c => c.Status == Dominio.StatusComanda.Aberta)
            .OrderBy(c => c.Numero)
            .ToListAsync();
    }

    public async Task<Comanda?> ObterLinhaAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        return await db.FindAsync<Comanda>(id);
    }

    public async Task<List<ItemComanda>> ListarItensAsync(Guid comandaId)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<ItemComanda>().Where(i => i.ComandaId == comandaId).ToListAsync();
    }

    /// <summary>
    /// RN-CX-05/06 / RN-PG-05: comandas abertas (impedem fechar), fechadas, total vendido e só o **dinheiro**
    /// (campo <c>Valor</c>, o que abateu a comanda — o troco não fica na gaveta) para o esperado do caixa.
    /// </summary>
    public async Task<ResumoVendasCaixa> ResumoVendasNoCaixaAsync(Guid caixaId)
    {
        var db = await banco.ConexaoAsync();
        var abertas = await db.Table<Comanda>()
            .Where(c => c.CaixaId == caixaId && c.Status == Dominio.StatusComanda.Aberta).CountAsync();
        var fechadas = await db.Table<Comanda>()
            .Where(c => c.CaixaId == caixaId && c.Status == Dominio.StatusComanda.Fechada).ToListAsync();
        var dinheiroCentavos = await db.ExecuteScalarAsync<long>(
            "SELECT COALESCE(SUM(p.valor_centavos), 0) FROM pagamentos p " +
            "JOIN comandas c ON c.id = p.comanda_id " +
            "WHERE c.caixa_id = ? AND c.status = ? AND p.forma = ?",
            caixaId, (int)Dominio.StatusComanda.Fechada, (int)Dominio.FormaPagamento.Dinheiro);

        return new ResumoVendasCaixa(
            Conversoes.ParaReais(dinheiroCentavos),
            Conversoes.ParaReais(fechadas.Sum(c => c.TotalCentavos)),
            fechadas.Count,
            abertas);
    }

    // ---------------------------------------------------------------- fila de envio (C4)

    public async Task<List<Pagamento>> ListarPagamentosAsync(Guid comandaId)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Pagamento>().Where(p => p.ComandaId == comandaId).ToListAsync();
    }

    /// <summary>Comandas fechadas/canceladas do caixa ainda não confirmadas pela API, mais antigas primeiro (até <paramref name="maximo"/>).</summary>
    public async Task<List<Comanda>> ListarPendentesEnvioAsync(Guid caixaId, int maximo)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Comanda>()
            .Where(c => c.CaixaId == caixaId && c.PendenteEnvio == true)
            .OrderBy(c => c.Numero)
            .Take(maximo)
            .ToListAsync();
    }

    public async Task<List<Guid>> ListarCaixasComPendenteEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        var linhas = await db.Table<Comanda>().Where(c => c.PendenteEnvio == true).ToListAsync();
        return linhas.Select(c => c.CaixaId).Distinct().ToList();
    }

    public async Task<int> ContarPendentesEnvioAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Comanda>().Where(c => c.PendenteEnvio == true).CountAsync();
    }

    public async Task<int> ContarRejeitadasAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Comanda>().Where(c => c.RejeitadaMotivo != null).CountAsync();
    }

    /// <summary>C5 / RN-SY-03: comandas rejeitadas pelo servidor, de qualquer caixa, mais recentes primeiro.</summary>
    public async Task<List<Comanda>> ListarRejeitadasAsync()
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Comanda>()
            .Where(c => c.RejeitadaMotivo != null)
            .OrderByDescending(c => c.CriadaEm)
            .ToListAsync();
    }

    /// <summary>C5: comandas fechadas/canceladas do caixa, mais recentes primeiro, com a situação do envio.</summary>
    public async Task<List<Comanda>> ListarEncerradasDoCaixaAsync(Guid caixaId)
    {
        var db = await banco.ConexaoAsync();
        return await db.Table<Comanda>()
            .Where(c => c.CaixaId == caixaId && c.Status != Dominio.StatusComanda.Aberta)
            .OrderByDescending(c => c.Numero)
            .ToListAsync();
    }

    /// <summary>aceita / ja_recebida: sai da fila. Nada mais muda na comanda (RN-CM-07).</summary>
    public async Task MarcarEnviadaAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE comandas SET pendente_envio = 0, enviada_em = ?, rejeitada_motivo = NULL, ultimo_erro_envio = NULL WHERE id = ?",
            DateTime.UtcNow, id);
    }

    /// <summary>rejeitada: sai da fila, mas o motivo fica visível para o atendente (RN-SY-03). A venda continua gravada.</summary>
    public async Task MarcarRejeitadaAsync(Guid id, string motivo)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync("UPDATE comandas SET pendente_envio = 0, rejeitada_motivo = ? WHERE id = ?", motivo, id);
    }

    /// <summary>Falha de rede/servidor: continua pendente, só anota.</summary>
    public async Task RegistrarFalhaEnvioAsync(Guid id, string erro)
    {
        var db = await banco.ConexaoAsync();
        await db.ExecuteAsync(
            "UPDATE comandas SET tentativas_envio = tentativas_envio + 1, ultimo_erro_envio = ? WHERE id = ?", erro, id);
    }

    /// <summary>RN-CM-02 / B8: sequencial dentro do caixa, contando também as fechadas.</summary>
    public async Task<int> ProximoNumeroAsync(Guid caixaId)
    {
        var db = await banco.ConexaoAsync();
        var ultima = await db.Table<Comanda>()
            .Where(c => c.CaixaId == caixaId)
            .OrderByDescending(c => c.Numero)
            .FirstOrDefaultAsync();
        return (ultima?.Numero ?? 0) + 1;
    }

    public async Task<Dominio.Comanda?> ObterAsync(Guid id)
    {
        var db = await banco.ConexaoAsync();
        var linha = await db.FindAsync<Comanda>(id);
        if (linha is null)
        {
            return null;
        }

        var itens = await db.Table<ItemComanda>().Where(i => i.ComandaId == id).ToListAsync();
        var pagamentos = await db.Table<Pagamento>().Where(p => p.ComandaId == id).ToListAsync();

        // RecebidaEm é a hora do servidor (RN-CM-12); no aparelho vale a de criação até a sincronização.
        return Dominio.Comanda.Restaurar(
            linha.Id, linha.CaixaId, linha.UsuarioId, linha.Numero, linha.Tipo, linha.Status,
            linha.CriadaEm, linha.CriadaEm, linha.Observacao,
            itens.Select(i => new Dominio.DadosItem(i.Id, i.ProdutoId, i.Descricao, i.Quantidade, i.PrecoUnitario, null)), // sabor dos potes: Sprint 7 (app)
            pagamentos.Select(p => new Dominio.DadosPagamento(p.Forma, p.Valor, p.ValorRecebido, p.Id)),
            linha.FechadaEm, linha.CanceladaEm, linha.MotivoCancelamento);
    }

    /// <summary>
    /// Grava comanda, itens e pagamentos numa única transação (B1, B4, B5): ou tudo entra, ou nada.
    /// <paramref name="nomeClienteNovo"/> só é usado quando a comanda ainda não existe no banco.
    /// </summary>
    public async Task GravarAsync(Dominio.Comanda comanda, string? nomeClienteNovo = null)
    {
        var db = await banco.ConexaoAsync();
        await db.RunInTransactionAsync(tx =>
        {
            var existente = tx.Find<Comanda>(comanda.Id);

            // Defesa extra: comanda fechada/cancelada é imutável (RN-CM-07); desfaz a transação.
            if (existente is not null && existente.Status != Dominio.StatusComanda.Aberta)
            {
                throw new DomainException("Esta comanda já foi fechada ou cancelada.");
            }

            // RN-CX-03 dentro da transação: o caixa pode ter sido fechado entre a conferência do serviço e a gravação.
            if (existente is null && tx.Find<Caixa>(comanda.CaixaId)?.EstaAberto != true)
            {
                throw new DomainException("O caixa foi fechado. Abra o caixa na aba Caixa antes de vender (RN-CX-03).");
            }
            var linha = new Comanda
            {
                Id = comanda.Id,
                CaixaId = comanda.CaixaId,
                UsuarioId = comanda.UsuarioId,
                Numero = comanda.Numero,
                NomeCliente = existente is null ? nomeClienteNovo : existente.NomeCliente,
                Tipo = comanda.Tipo,
                Status = comanda.Status,
                Observacao = comanda.Observacao,
                Total = comanda.Total,
                CriadaEm = comanda.CriadaEm,
                FechadaEm = comanda.FechadaEm,
                CanceladaEm = comanda.CanceladaEm,
                MotivoCancelamento = comanda.MotivoCancelamento,
                PendenteEnvio = !comanda.EstaAberta, // docs/03 §8: fechada/cancelada entra na fila de envio
            };

            if (existente is null)
            {
                tx.Insert(linha);
            }
            else
            {
                tx.Update(linha);
            }

            var itensGravados = tx.Table<ItemComanda>().Where(i => i.ComandaId == comanda.Id).ToList();
            var idsAtuais = comanda.Itens.Select(i => i.Id).ToHashSet();

            // RN-CM-06: comanda aberta pode perder itens
            foreach (var removido in itensGravados.Where(i => !idsAtuais.Contains(i.Id)))
            {
                tx.Delete(removido);
            }

            var idsGravados = itensGravados.Select(i => i.Id).ToHashSet();
            foreach (var item in comanda.Itens)
            {
                var linhaItem = new ItemComanda
                {
                    Id = item.Id,
                    ComandaId = comanda.Id,
                    ProdutoId = item.ProdutoId,
                    Descricao = item.Descricao,
                    Quantidade = item.Quantidade,
                    PrecoUnitario = item.PrecoUnitario,
                    Subtotal = item.Subtotal,
                };

                if (idsGravados.Contains(item.Id))
                {
                    tx.Update(linhaItem);
                }
                else
                {
                    tx.Insert(linhaItem);
                }
            }

            // Pagamento nunca é apagado nem alterado (RN-TD-03): só entram os novos.
            var pagamentosGravados = tx.Table<Pagamento>().Where(p => p.ComandaId == comanda.Id).ToList()
                .Select(p => p.Id).ToHashSet();
            foreach (var pagamento in comanda.Pagamentos.Where(p => !pagamentosGravados.Contains(p.Id)))
            {
                tx.Insert(new Pagamento
                {
                    Id = pagamento.Id,
                    ComandaId = comanda.Id,
                    Forma = pagamento.Forma,
                    Valor = pagamento.Valor,
                    ValorRecebido = pagamento.ValorRecebido,
                    Troco = pagamento.Troco,
                    PagoEm = comanda.FechadaEm ?? DateTime.UtcNow,
                });
            }
        });
    }

    public async Task AlterarNomeClienteAsync(Guid comandaId, string? nomeCliente)
    {
        var db = await banco.ConexaoAsync();
        var alteradas = await db.ExecuteAsync(
            "UPDATE comandas SET nome_cliente = ? WHERE id = ? AND status = ?",
            nomeCliente, comandaId, (int)Dominio.StatusComanda.Aberta);
        if (alteradas == 0)
        {
            throw new DomainException("Só dá para mudar o nome de uma comanda aberta.");
        }
    }
}
