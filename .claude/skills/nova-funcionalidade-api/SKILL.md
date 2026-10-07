---
name: nova-funcionalidade-api
description: Passo a passo para criar um endpoint ou caso de uso novo na API da sorveteria (Contracts → Domain → Application → Infrastructure → Server → testes → .http → docs). Use ao implementar qualquer funcionalidade de backend.
---

# Nova funcionalidade na API

Ordem de dentro para fora. Não pule etapas; cada uma compila sozinha.

## 1. Entender a regra
- Ache a regra em `docs/02-regras-de-negocio-v2.md` (`RN-XX-NN`) e o endpoint em `docs/03-arquitetura-e-modelo-de-dados.md` (seção 6).
- Se a regra não existir ou estiver ambígua: pare e pergunte ao Paulo; depois registre em `docs/02`.

## 2. Contracts (`src/GestaoSorveteria.Contracts/<Modulo>/`)
```csharp
public sealed record CriarProdutoRequest(
    [Required(ErrorMessage = "Informe o nome.")] string Nome,
    [Required(ErrorMessage = "Informe a categoria.")] string Categoria,
    [Range(0, 99999.99, ErrorMessage = "Preço inválido.")] decimal Preco,
    bool PermiteValorLivre,
    int Ordem);
public sealed record ProdutoDto(Guid Id, string Nome, decimal Preco, bool Ativo);
```

## 3. Domain (só se a regra for nova)
- Método na entidade que valida com `Guard`/`DomainException` e muda o estado. Sem `DateTime.Now` (receba `agoraUtc`).
- Teste de domínio em `tests/GestaoSorveteria.Tests/Domain/` antes de seguir.

## 4. Application (`src/GestaoSorveteria.Application/<Modulo>/<Nome>Service.cs`)
```csharp
public sealed class ProdutoService(IProdutoRepository produtos, IUnitOfWork uow, IClock clock)
{
    public async Task<ProdutoDto> CriarAsync(CriarProdutoRequest req, CancellationToken ct = default)
    {
        if (await produtos.ExisteComNomeAsync(Produto.NormalizarNome(req.Nome), null, ct))
            throw new DomainException("Já existe um produto com esse nome.");
        var produto = Produto.Criar(req.Nome, req.Categoria, req.Preco, req.PermiteValorLivre, req.Ordem, clock.UtcNow);
        await produtos.AdicionarAsync(produto, ct);
        await uow.SaveChangesAsync(ct);
        return produto.ToDto();
    }
}
```
- Mapeamento entidade → DTO num `<Modulo>Mapper` (extensão `ToDto`).
- Não encontrado → `EntidadeNaoEncontradaException`; sem permissão → `AcessoNegadoException`.

## 5. Infrastructure
- Método novo no repositório: `async`, `AsQueryable()` antes de operadores async direto no `DbSet`, `Include` quando carregar agregado.
- Coluna/tabela nova → configuração em `Persistence/Configurations/` + skill `migracao-ef`.

## 6. Server
- Controller em `Controllers/<Plural>Controller.cs`: `[ApiController]`, `[Route("api/<plural>")]`, `[Authorize]` (gestão: `[Authorize(Policy = Politicas.Admin)]`), `ProducesResponseType` para 200/400/401/403/404.
- Registre o serviço no `Program.cs` (`builder.Services.AddScoped<ProdutoService>();`).
- Adicione exemplos em `GestaoSorveteria.Server.http`.

## 7. Testes de aplicação
- Fakes em `tests/GestaoSorveteria.Tests/Application/Fakes.cs`. Cubra: sucesso, regra violada, não encontrado, idempotência (se houver `Id` do cliente).

## 8. Fechar
- `dotnet build src/GestaoSorveteria.Server` e `dotnet test tests/GestaoSorveteria.Tests` verdes.
- Atualize `docs/03` (endpoint) e `docs/02` (regra) se mudaram.
- Siga a skill `fechar-tarefa`.
