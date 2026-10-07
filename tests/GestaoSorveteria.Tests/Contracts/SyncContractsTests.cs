using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using GestaoSorveteria.Contracts.Sync;
using GestaoSorveteria.Domain.Caixas;
using GestaoSorveteria.Domain.Comandas;

namespace GestaoSorveteria.Tests.Contracts;

public class SyncContractsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SyncComandasRequest_IdaEVoltaEmJson_PreservaDinheiroDatasEItemLivre()
    {
        var criadaEm = new DateTime(2026, 9, 30, 15, 0, 0, DateTimeKind.Utc);
        var request = new SyncComandasRequest(
        [
            new ComandaSyncDto(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "Balcao", "Fechada", 30.10m, criadaEm,
                [new ItemComandaSyncDto(Guid.NewGuid(), null, "Self-service", 1, 30.10m, 30.10m)],
                [new PagamentoSyncDto(Guid.NewGuid(), "Dinheiro", 30.10m, 50m, 19.90m)],
                FechadaEm: criadaEm.AddMinutes(3)),
        ]);

        var json = JsonSerializer.Serialize(request, Web);
        var volta = JsonSerializer.Deserialize<SyncComandasRequest>(json, Web)!;

        var comanda = Assert.Single(volta.Comandas);
        Assert.Equal(30.10m, comanda.Total);
        Assert.Equal(DateTimeKind.Utc, comanda.CriadaEm.Kind);
        Assert.Null(Assert.Single(comanda.Itens).ProdutoId); // item livre (RN-CM-03)
        Assert.Equal(19.90m, Assert.Single(comanda.Pagamentos).Troco);
        Assert.Contains("\"comandas\":", json);
    }

    [Fact]
    public void SyncResponse_Json_UsaStatusEmTexto()
    {
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new SyncResponse([new ResultadoSyncDto(id, StatusSync.JaRecebida)]), Web);

        Assert.Contains("\"status\":\"ja_recebida\"", json);
        Assert.Contains($"\"id\":\"{id}\"", json);
    }

    [Fact]
    public void SyncComandasRequest_LoteVazioOuGrandeDemais_Invalido()
    {
        Assert.False(Valido(new SyncComandasRequest([])));
        Assert.False(Valido(new SyncComandasRequest(Enumerable.Repeat(ComandaQualquer(), LimitesSync.MaximoPorLote + 1).ToList())));
        Assert.True(Valido(new SyncComandasRequest([ComandaQualquer()])));
    }

    [Fact]
    public void SyncCaixasRequest_LoteVazioOuGrandeDemais_Invalido()
    {
        var caixa = new CaixaSyncDto(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, 50m, []);
        Assert.False(Valido(new SyncCaixasRequest([])));
        Assert.False(Valido(new SyncCaixasRequest(Enumerable.Repeat(caixa, LimitesSync.MaximoPorLote + 1).ToList())));
        Assert.True(Valido(new SyncCaixasRequest([caixa])));
    }

    // Um item inválido vira "rejeitada" no resultado; não pode derrubar o lote inteiro com 400.
    [Theory]
    [InlineData(typeof(CaixaSyncDto))]
    [InlineData(typeof(MovimentoCaixaSyncDto))]
    [InlineData(typeof(FechamentoCaixaSyncDto))]
    [InlineData(typeof(ComandaSyncDto))]
    [InlineData(typeof(ItemComandaSyncDto))]
    [InlineData(typeof(PagamentoSyncDto))]
    public void DtoDeItemDoLote_NaoTemValidacaoPorAtributo(Type tipo)
    {
        var parametros = tipo.GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(parametros, p => p.GetCustomAttributes<ValidationAttribute>().Any());
        Assert.DoesNotContain(tipo.GetProperties(), p => p.GetCustomAttributes<ValidationAttribute>().Any());
    }

    // Com Nullable ligado, o [ApiController] trata referência não anulável como [Required] implícito:
    // um campo nulo derrubaria o lote inteiro com 400 e travaria a fila do celular.
    [Theory]
    [InlineData(typeof(CaixaSyncDto))]
    [InlineData(typeof(MovimentoCaixaSyncDto))]
    [InlineData(typeof(FechamentoCaixaSyncDto))]
    [InlineData(typeof(ComandaSyncDto))]
    [InlineData(typeof(ItemComandaSyncDto))]
    [InlineData(typeof(PagamentoSyncDto))]
    public void DtoDeItemDoLote_ReferenciasSaoAnulaveis(Type tipo)
    {
        var nulabilidade = new NullabilityInfoContext();
        var obrigatorias = tipo.GetProperties()
            .Where(p => !p.PropertyType.IsValueType && p.Name != "EqualityContract")
            .Where(p => nulabilidade.Create(p).ReadState != NullabilityState.Nullable)
            .Select(p => p.Name);

        Assert.Empty(obrigatorias);
    }

    // Os textos documentados nos Contracts precisam existir no Domain (o app e a API convertem por nome).
    [Theory]
    [InlineData(typeof(FormaPagamento), new[] { "Dinheiro", "Pix", "CartaoDebito", "CartaoCredito" })]
    [InlineData(typeof(TipoComanda), new[] { "Balcao", "Delivery" })]
    [InlineData(typeof(StatusComanda), new[] { "Fechada", "Cancelada" })]
    [InlineData(typeof(TipoMovimentoCaixa), new[] { "Sangria", "Suprimento" })]
    public void TextosDeEnumDosContracts_ExistemNoDomain(Type enumDoDomain, string[] textos)
    {
        Assert.All(textos, texto => Assert.True(Enum.IsDefined(enumDoDomain, Enum.Parse(enumDoDomain, texto))));
    }

    private static ComandaSyncDto ComandaQualquer() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "Balcao", "Cancelada", 0m, DateTime.UtcNow, [], []);

    // Como o MVC faz com records: os atributos ficam nos parâmetros do construtor primário.
    private static bool Valido(object request)
    {
        var tipo = request.GetType();
        return tipo.GetConstructors().Single().GetParameters().All(parametro =>
        {
            var valor = tipo.GetProperty(parametro.Name!)!.GetValue(request);
            return parametro.GetCustomAttributes<ValidationAttribute>().All(atributo => atributo.IsValid(valor));
        });
    }
}
