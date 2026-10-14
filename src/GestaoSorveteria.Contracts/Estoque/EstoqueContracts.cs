namespace GestaoSorveteria.Contracts.Estoque;

// Fase 2 — catálogo de estoque (docs/10, docs/02 §6). Enums como texto: ModoControle (Contagem, SaldoPelaVenda, SoAviso),
// Unidade (Caixa, Balde, Unidade, Pacote), Envio (Impresso, WhatsApp, Ambos), dias da semana em inglês (Monday…), como o .NET.

/// <summary>RN-PF-01.</summary>
public sealed record FornecedorDto(
    Guid Id,
    string Nome,
    string? WhatsApp,
    IReadOnlyList<string> DiasPedido,
    int PrazoEntregaDias,
    bool AceitaMeiaCaixa,
    string Envio,
    bool MostrarPrecos,
    bool Ativo);

public sealed record SalvarFornecedorRequest(
    string Nome,
    string? WhatsApp,
    IReadOnlyList<string> DiasPedido,
    int PrazoEntregaDias,
    bool AceitaMeiaCaixa,
    string Envio,
    bool MostrarPrecos);

/// <summary>RN-ES-02.</summary>
public sealed record GrupoEstoqueDto(Guid Id, string Nome, string ModoControle, int Ordem);

public sealed record SalvarGrupoEstoqueRequest(string Nome, string ModoControle, int Ordem);

/// <summary>RN-ES-01. Alvo em embalagens (meia caixa vale); mínimo em unidades (potes).</summary>
public sealed record ItemEstoqueDto(
    Guid Id,
    Guid GrupoId,
    string GrupoNome,
    Guid FornecedorId,
    string FornecedorNome,
    string Nome,
    string Unidade,
    int UnidadesPorEmbalagem,
    decimal CustoEmbalagem,
    decimal? EstoqueAlvoEmbalagens,
    int? MinimoUnidades,
    int Ordem,
    bool Ativo);

public sealed record SalvarItemEstoqueRequest(
    Guid GrupoId,
    Guid FornecedorId,
    string Nome,
    string Unidade,
    int UnidadesPorEmbalagem,
    decimal CustoEmbalagem,
    int Ordem,
    decimal? EstoqueAlvoEmbalagens,
    int? MinimoUnidades);

/// <summary>GET /api/estoque/catalogo — tudo que o painel e o app precisam para montar as telas.</summary>
public sealed record CatalogoEstoqueResponse(
    IReadOnlyList<FornecedorDto> Fornecedores,
    IReadOnlyList<GrupoEstoqueDto> Grupos,
    IReadOnlyList<ItemEstoqueDto> Itens);

/// <summary>POST /api/estoque/itens/importar — conteúdo do CSV no formato de docs/fase2/catalogo-inicial.csv.</summary>
public sealed record ImportarCatalogoRequest(string Conteudo);

/// <summary>Resultado por linha: o que foi criado, atualizado e o que ficou de fora (com o motivo). Reimportar não duplica.</summary>
public sealed record ImportarCatalogoResponse(
    int FornecedoresCriados,
    int GruposCriados,
    int ItensCriados,
    int ItensAtualizados,
    int ItensSemMudanca,
    IReadOnlyList<ErroImportacaoDto> Erros);

/// <summary><paramref name="Linha"/> conta a partir de 1, com o cabeçalho na linha 1 (como no Excel).</summary>
public sealed record ErroImportacaoDto(int Linha, string Motivo);
