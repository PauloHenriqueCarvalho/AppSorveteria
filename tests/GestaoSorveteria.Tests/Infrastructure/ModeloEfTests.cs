using GestaoSorveteria.Domain.Estoque;
using GestaoSorveteria.Domain.Loja;
using GestaoSorveteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GestaoSorveteria.Tests.Infrastructure;

/// <summary>Modelo do EF Core sem banco: nada de conexão, só o mapeamento e a comparação com as migrações.</summary>
public class ModeloEfTests
{
    private static AppDbContext Contexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql("Host=localhost;Database=modelo").Options);

    private static IModel ModeloDeDesign(AppDbContext db) => db.GetService<IDesignTimeModel>().Model;

    /// <summary>
    /// A API aplica as migrações ao subir e o EF recusa subir com o modelo diferente do último snapshot.
    /// Mexeu em entidade ou configuração? Gere a migração (skill migracao-ef ou o workflow "Gerar migração EF").
    /// </summary>
    [Fact]
    public void Modelo_ComparadoComAsMigracoes_NaoTemMudancaPendente()
    {
        using var db = Contexto();

        Assert.False(db.Database.HasPendingModelChanges(),
            "O modelo do EF mudou sem migração: gere com `dotnet ef migrations add` ou com [gerar-migracao: Nome] no commit.");
    }

    [Fact]
    public void Estoque_TabelasEColunas_EmSnakeCaseComoNoDocs03()
    {
        using var db = Contexto();
        var modelo = ModeloDeDesign(db);

        Assert.Equal("fornecedores", modelo.FindEntityType(typeof(Fornecedor))!.GetTableName());
        Assert.Equal("grupos_estoque", modelo.FindEntityType(typeof(GrupoEstoque))!.GetTableName());
        Assert.Equal("itens_estoque", modelo.FindEntityType(typeof(ItemEstoque))!.GetTableName());
        Assert.Equal("contagens", modelo.FindEntityType(typeof(Contagem))!.GetTableName());
        Assert.Equal("itens_contagem", modelo.FindEntityType(typeof(ItemContagem))!.GetTableName());
        Assert.Equal("avisos", modelo.FindEntityType(typeof(Aviso))!.GetTableName());

        var fornecedor = modelo.FindEntityType(typeof(Fornecedor))!;
        Assert.Equal("dias_pedido", fornecedor.FindProperty("_diasPedido")!.GetColumnName());
        Assert.Equal("whatsapp", fornecedor.FindProperty(nameof(Fornecedor.WhatsApp))!.GetColumnName());
        Assert.Equal("unidade_compra",
            modelo.FindEntityType(typeof(ItemEstoque))!.FindProperty(nameof(ItemEstoque.Unidade))!.GetColumnName());
        Assert.Equal("whatsapp_aberto_em",
            modelo.FindEntityType(typeof(Aviso))!.FindProperty(nameof(Aviso.WhatsAppAbertoEm))!.GetColumnName());
    }

    [Fact]
    public void ConfiguracaoLoja_UmaTabelaComOWhatsAppDaDona()
    {
        using var db = Contexto();
        var configuracao = ModeloDeDesign(db).FindEntityType(typeof(ConfiguracaoLoja))!;

        Assert.Equal("configuracao_loja", configuracao.GetTableName());
        Assert.Equal("whatsapp_dona", configuracao.FindProperty(nameof(ConfiguracaoLoja.WhatsAppDona))!.GetColumnName());
    }

    [Fact]
    public void Estoque_IndicesUnicos_SeguramNomesRepetidosEAvisoAbertoDuplicado()
    {
        using var db = Contexto();
        var modelo = ModeloDeDesign(db);

        // RN-ES-01: item único por grupo + nome; RN-ES-11: grupo e fornecedor por nome.
        Assert.True(Indice(modelo, typeof(ItemEstoque), nameof(ItemEstoque.GrupoId), nameof(ItemEstoque.NomeNormalizado)).IsUnique);
        Assert.True(Indice(modelo, typeof(GrupoEstoque), nameof(GrupoEstoque.NomeNormalizado)).IsUnique);
        Assert.True(Indice(modelo, typeof(Fornecedor), nameof(Fornecedor.NomeNormalizado)).IsUnique);
        Assert.True(Indice(modelo, typeof(ItemContagem), nameof(ItemContagem.ContagemId), nameof(ItemContagem.ItemEstoqueId)).IsUnique);

        // RN-AV-03: um aviso aberto por item.
        var avisoAberto = Indice(modelo, typeof(Aviso), nameof(Aviso.ItemEstoqueId));
        Assert.True(avisoAberto.IsUnique);
        Assert.Equal("status = 'Aberto' AND item_estoque_id IS NOT NULL", avisoAberto.GetFilter());
    }

    [Fact]
    public void Pedido_UmRascunhoPorFornecedorENumeroUnico()
    {
        using var db = Contexto();
        var modelo = ModeloDeDesign(db);

        Assert.Equal("pedidos_fornecedor", modelo.FindEntityType(typeof(PedidoFornecedor))!.GetTableName());
        Assert.Equal("itens_pedido_fornecedor", modelo.FindEntityType(typeof(ItemPedidoFornecedor))!.GetTableName());
        var rascunho = Indice(modelo, typeof(PedidoFornecedor), nameof(PedidoFornecedor.FornecedorId));
        Assert.True(rascunho.IsUnique);
        Assert.Equal("status = 'Rascunho'", rascunho.GetFilter());
        Assert.True(Indice(modelo, typeof(PedidoFornecedor), nameof(PedidoFornecedor.Numero)).IsUnique);
        Assert.True(Indice(modelo, typeof(ItemPedidoFornecedor), nameof(ItemPedidoFornecedor.PedidoId), nameof(ItemPedidoFornecedor.ItemEstoqueId)).IsUnique);
    }

    private static IIndex Indice(IModel modelo, Type entidade, params string[] propriedades) =>
        modelo.FindEntityType(entidade)!.GetIndexes()
            .Single(i => i.Properties.Select(p => p.Name).SequenceEqual(propriedades));
}
