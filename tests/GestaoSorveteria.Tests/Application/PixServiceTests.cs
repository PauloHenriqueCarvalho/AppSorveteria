using GestaoSorveteria.Application.Abstractions;
using GestaoSorveteria.Application.Common;
using GestaoSorveteria.Application.Pix;
using GestaoSorveteria.Contracts.Pix;
using GestaoSorveteria.Domain.Common;
using GestaoSorveteria.Domain.Pix;
using GestaoSorveteria.Domain.Repositories;

namespace GestaoSorveteria.Tests.Application;

/// <summary>RN-PX-01…06: caso de uso do Pix integrado, com o banco em memória.</summary>
public class PixServiceTests
{
    private static readonly Guid Atendente = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class ProvedorPixFake : IProvedorPix
    {
        public Dictionary<string, CobrancaNoBanco> Banco { get; } = new();
        public int Criacoes { get; private set; }
        public bool ForaDoAr { get; set; }
        public bool Configurado { get; set; } = true;
        public string Nome => "Fake";
        public int ExpiracaoSegundos => 600;
        public NovaCobrancaPix? UltimaCriada { get; private set; }

        public Task<CobrancaNoBanco> CriarAsync(NovaCobrancaPix cobranca, CancellationToken cancellationToken = default)
        {
            Falhar();
            Criacoes++;
            UltimaCriada = cobranca;
            if (!Banco.TryGetValue(cobranca.Txid, out var existente))
            {
                existente = new CobrancaNoBanco(cobranca.Txid, cobranca.Valor, "000201copia" + cobranca.Txid, new SituacaoCobrancaPix(StatusCobrancaPix.Ativa));
                Banco[cobranca.Txid] = existente;
            }

            return Task.FromResult(existente);
        }

        public Task<CobrancaNoBanco> ConsultarAsync(string txid, CancellationToken cancellationToken = default)
        {
            Falhar();
            return Task.FromResult(Banco[txid]);
        }

        public Task<CobrancaNoBanco> CancelarAsync(string txid, CancellationToken cancellationToken = default)
        {
            Falhar();
            var atual = Banco[txid];
            if (atual.Situacao.Status == StatusCobrancaPix.Ativa)
            {
                atual = atual with { Situacao = new SituacaoCobrancaPix(StatusCobrancaPix.Removida) };
                Banco[txid] = atual;
            }

            return Task.FromResult(atual);
        }

        public void Pagar(string txid, decimal? valor = null) =>
            Banco[txid] = Banco[txid] with
            {
                Situacao = new SituacaoCobrancaPix(StatusCobrancaPix.Concluida, "E123", valor ?? Banco[txid].Valor, new DateTime(2026, 9, 17, 15, 1, 0, DateTimeKind.Utc)),
            };

        private void Falhar()
        {
            if (ForaDoAr)
            {
                throw new ProvedorPixException("O banco não respondeu.");
            }
        }
    }

    private sealed class CobrancaPixRepositoryFake : ICobrancaPixRepository
    {
        public List<CobrancaPix> Cobrancas { get; } = new();

        public Task<CobrancaPix?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Cobrancas.FirstOrDefault(c => c.Id == id));

        public Task<CobrancaPix?> ObterPorTxidAsync(string txid, CancellationToken cancellationToken = default) =>
            Task.FromResult(Cobrancas.FirstOrDefault(c => c.Txid == txid));

        public Task AdicionarAsync(CobrancaPix cobranca, CancellationToken cancellationToken = default)
        {
            Cobrancas.Add(cobranca);
            return Task.CompletedTask;
        }
    }

    private sealed record Ambiente(PixService Servico, ProvedorPixFake Banco, CobrancaPixRepositoryFake Repositorio, UnitOfWorkFake Uow, ClockFake Relogio);

    private static Ambiente Criar()
    {
        var banco = new ProvedorPixFake();
        var repositorio = new CobrancaPixRepositoryFake();
        var uow = new UnitOfWorkFake();
        var relogio = new ClockFake();
        return new Ambiente(new PixService(banco, repositorio, new ConfiguracaoLojaRepositoryFake(), uow, relogio), banco, repositorio, uow, relogio);
    }

    [Fact]
    public async Task CriarAsync_Ligado_CriaNoBancoEGrava()
    {
        var a = Criar();
        var id = Guid.NewGuid();

        var dto = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(id, 23.50m), Atendente, Ct);

        Assert.Equal(id, dto.Id);
        Assert.Equal(id.ToString("N"), dto.Txid);
        Assert.Equal("Ativa", dto.Status);
        Assert.False(dto.Paga);
        Assert.StartsWith("000201", dto.PixCopiaECola);
        Assert.Single(a.Repositorio.Cobrancas);
        Assert.Equal(1, a.Uow.Confirmacoes);
        Assert.Equal(23.50m, a.Banco.UltimaCriada!.Valor);
        Assert.Contains("balcão", a.Banco.UltimaCriada.Descricao);
    }

    [Fact]
    public async Task CriarAsync_Desligado_LancaSemChamarOBanco()
    {
        var a = Criar();
        a.Banco.Configurado = false;

        var ex = await Assert.ThrowsAsync<DomainException>(() => a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 10m), Atendente, Ct));

        Assert.Contains("RN-PX-01", ex.Message);
        Assert.Equal(0, a.Banco.Criacoes);
    }

    [Fact]
    public async Task CriarAsync_MesmoIdDeNovo_DevolveAMesmaSemChamarOBanco()
    {
        var a = Criar();
        var pedido = new CriarCobrancaPixRequest(Guid.NewGuid(), 15m);
        var primeira = await a.Servico.CriarAsync(pedido, Atendente, Ct);

        var segunda = await a.Servico.CriarAsync(pedido, Atendente, Ct);

        Assert.Equal(primeira.Txid, segunda.Txid);
        Assert.Equal(1, a.Banco.Criacoes);
        Assert.Single(a.Repositorio.Cobrancas);
    }

    [Fact]
    public async Task CriarAsync_MesmoIdOutroValor_Lanca()
    {
        var a = Criar();
        var id = Guid.NewGuid();
        await a.Servico.CriarAsync(new CriarCobrancaPixRequest(id, 15m), Atendente, Ct);

        await Assert.ThrowsAsync<DomainException>(() => a.Servico.CriarAsync(new CriarCobrancaPixRequest(id, 16m), Atendente, Ct));
    }

    [Fact]
    public async Task CriarAsync_BancoForaDoAr_PropagaENaoGrava()
    {
        var a = Criar();
        a.Banco.ForaDoAr = true;

        await Assert.ThrowsAsync<ProvedorPixException>(() => a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 10m), Atendente, Ct));

        Assert.Empty(a.Repositorio.Cobrancas);
        Assert.Equal(0, a.Uow.Confirmacoes);
    }

    [Fact]
    public async Task ConsultarAsync_PagaNoBanco_DevolvePagaEGrava()
    {
        var a = Criar();
        var criada = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 23.50m), Atendente, Ct);
        a.Banco.Pagar(criada.Txid);

        var dto = await a.Servico.ConsultarAsync(criada.Id, Ct);

        Assert.True(dto.Paga);
        Assert.Equal("Concluida", dto.Status);
        Assert.Equal("E123", dto.EndToEndId);
        Assert.Equal(2, a.Uow.Confirmacoes);
    }

    [Fact]
    public async Task ConsultarAsync_PagoComOutroValor_NaoEstaPaga()
    {
        var a = Criar();
        var criada = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 23.50m), Atendente, Ct);
        a.Banco.Pagar(criada.Txid, 2.35m);

        var dto = await a.Servico.ConsultarAsync(criada.Id, Ct);

        Assert.Equal("Concluida", dto.Status);
        Assert.False(dto.Paga);
    }

    [Fact]
    public async Task ConsultarAsync_AtivaSemMudanca_NaoGrava()
    {
        var a = Criar();
        var criada = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 10m), Atendente, Ct);

        var dto = await a.Servico.ConsultarAsync(criada.Id, Ct);

        Assert.Equal("Ativa", dto.Status);
        Assert.Equal(1, a.Uow.Confirmacoes);
    }

    [Fact]
    public async Task ConsultarAsync_VencidaNoRelogio_Expira()
    {
        var a = Criar();
        var criada = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 10m), Atendente, Ct);
        a.Relogio.UtcNow = a.Relogio.UtcNow.AddMinutes(12);

        var dto = await a.Servico.ConsultarAsync(criada.Id, Ct);

        Assert.Equal("Expirada", dto.Status);
    }

    [Fact]
    public async Task ConsultarAsync_Desconhecida_Lanca404()
    {
        var a = Criar();

        await Assert.ThrowsAsync<EntidadeNaoEncontradaException>(() => a.Servico.ConsultarAsync(Guid.NewGuid(), Ct));
    }

    [Fact]
    public async Task CancelarAsync_Ativa_FicaRemovida()
    {
        var a = Criar();
        var criada = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 10m), Atendente, Ct);

        var dto = await a.Servico.CancelarAsync(criada.Id, Ct);

        Assert.Equal("Removida", dto.Status);
        Assert.Equal(StatusCobrancaPix.Removida, a.Banco.Banco[criada.Txid].Situacao.Status);
    }

    [Fact]
    public async Task CancelarAsync_PagaNoUltimoSegundo_VoltaPaga()
    {
        var a = Criar();
        var criada = await a.Servico.CriarAsync(new CriarCobrancaPixRequest(Guid.NewGuid(), 10m), Atendente, Ct);
        a.Banco.Pagar(criada.Txid);

        var dto = await a.Servico.CancelarAsync(criada.Id, Ct);

        Assert.True(dto.Paga);
    }
}
