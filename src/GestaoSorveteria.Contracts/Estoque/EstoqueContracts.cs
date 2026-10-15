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

/// <summary>
/// POST /api/estoque/itens/importar — conteúdo do CSV no formato de docs/fase2/catalogo-inicial.csv (RN-ES-10).
/// <paramref name="Simular"/> = true devolve o mesmo resumo sem gravar nada: o painel mostra a prévia e a dona confirma.
/// </summary>
public sealed record ImportarCatalogoRequest(string Conteudo, bool Simular = false);

/// <summary>
/// O que a importação criou (ou criaria, na simulação). Item que já está no catálogo é mantido como está;
/// se a planilha traz outro custo, embalagem ou fornecedor, a diferença aparece em <paramref name="Avisos"/>.
/// Linha com problema fica de fora e aparece em <paramref name="Erros"/>; as outras entram.
/// </summary>
public sealed record ImportarCatalogoResponse(
    bool Simulacao,
    IReadOnlyList<string> FornecedoresNovos,
    IReadOnlyList<string> GruposNovos,
    int ItensNovos,
    int ItensJaCadastrados,
    IReadOnlyList<LinhaImportacaoDto> Avisos,
    IReadOnlyList<LinhaImportacaoDto> Erros);

/// <summary><paramref name="Linha"/> é a que o Excel mostra, com o cabeçalho na linha 1.</summary>
public sealed record LinhaImportacaoDto(int Linha, string Mensagem);

/// <summary>
/// GET /api/estoque/itens — o que o app do atendente precisa para "Está acabando" e para a contagem: grupos e itens
/// sem custo nem fornecedor (o atendente não vê preço de compra).
/// </summary>
/// <param name="DiasDeContagem">
/// RN-ES-05: dias da semana ("Sunday"…) em que o app mostra "Hoje é dia de contagem" — a véspera do dia de pedido de cada
/// fornecedor ativo que tem item na contagem.
/// </param>
public sealed record CatalogoEstoqueAppResponse(
    IReadOnlyList<GrupoEstoqueDto> Grupos,
    IReadOnlyList<ItemEstoqueAppDto> Itens,
    IReadOnlyList<string> DiasDeContagem);

public sealed record ItemEstoqueAppDto(
    Guid Id,
    Guid GrupoId,
    string Nome,
    string Unidade,
    int UnidadesPorEmbalagem,
    int Ordem,
    bool Ativo);

/// <summary>
/// GET /api/estoque/saldos (Admin) — RN-ES-08: saldo de cada pote (grupo <c>SaldoPelaVenda</c>) = última contagem + o que
/// chegou − o que foi vendido depois dela. <paramref name="SaldoUnidades"/> pode ser negativo (a venda nunca é bloqueada:
/// "conferir"). <paramref name="VendidosNaSemana"/> = vendas dos últimos 7 dias menos estornos.
/// </summary>
public sealed record SaldoPoteDto(
    Guid ItemEstoqueId,
    Guid GrupoId,
    string GrupoNome,
    string ItemNome,
    int SaldoUnidades,
    int? MinimoUnidades,
    int? UltimaContagemUnidades,
    DateTime? UltimaContagemEm,
    int VendidosNaSemana,
    bool Ativo);
