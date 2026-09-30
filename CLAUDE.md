# CLAUDE.md — Gestão Sorveteria (AppSorveteria)

Guia para o Claude (e para qualquer dev) trabalhar neste repositório. Leia inteiro antes de mudar código.

## O que é

Sistema de gestão para uma sorveteria pequena, feito por Paulo (dev .NET) para a dona da loja.

| Parte | Onde | Tecnologia |
|---|---|---|
| App do atendente (balcão, Android, **funciona offline**) | `src/GestaoSorveteria.Mobile` | .NET MAUI + SQLite (`sqlite-net-pcl`) |
| API | `src/GestaoSorveteria.Server` + `Domain`, `Contracts`, `Application`, `Infrastructure` | ASP.NET Core 10, EF Core 10, PostgreSQL (Npgsql), JWT |
| Testes | `tests/GestaoSorveteria.Tests` | xUnit v3 |
| Painel da dona | a criar (Sprint 3) | site estático — Blazor WebAssembly ou React + Vite (a decidir) |

Problemas que o sistema resolve: pedidos em papel, estoque "no olhômetro", não saber o que vende mais, **caixa que não bate**, reposição sem planejamento.

## Decisões que mandam (não contrariar sem conversar com o Paulo)

1. **Local-first.** A comanda nasce, recebe itens e fecha **no celular** (SQLite). A API recebe comandas e caixas **prontos** por `POST /api/sync/*`, idempotente pelo `Id`. A venda nunca espera internet.
2. **Mesmas regras no celular e na API.** Total, troco e fechamento de caixa ficam no `Domain` (sem dependências) e o app referencia `Domain` e `Contracts`.
3. **Continuar sobre o MVP MAUI**, refatorando em 3 etapas (docs/06). Não reescrever as telas do zero.
4. **Hospedagem gratuita** até o fim do piloto: API no Render Free (dorme após 15 min, acorda em ~1 min), PostgreSQL no Neon Free, painel no Cloudflare Pages. **Não usar Vercel Hobby** (proíbe uso comercial). Detalhes em docs/07.
5. **Melhor funcionamento acima da proposta comercial**: a proposta é ajustada depois ao que foi construído.

## Mapa dos documentos (`docs/`)

| Arquivo | Quando ler |
|---|---|
| `01-analise-e-decisoes.md` | Por que cada decisão foi tomada (ADRs; ver "Decisões revisadas em 29/09/2026") |
| `02-regras-de-negocio-v2.md` | **Antes de qualquer regra de negócio.** Regras numeradas `RN-XX-NN` |
| `03-arquitetura-e-modelo-de-dados.md` | Camadas, tabelas, endpoints, protocolo de sincronização |
| `04-roadmap-v2.md` | O que está em cada sprint e o critério de "pronto" |
| `05-correcoes-da-proposta.md` | Só para a parte comercial |
| `06-analise-mvp-maui.md` | **Antes de mexer no app.** Bugs B1–B15 e plano das Etapas A/B/C |
| `07-hospedagem.md` | Deploy, limites dos planos gratuitos, variáveis de ambiente |
| `contexto-mvp/` | Documentos originais do MVP (histórico, não é fonte de verdade) |

Quadro de tarefas: Trello "Sorveteria" (listas Backlog, A fazer, Em Andamento, Concluido, Dúvidas para a dona). Etiquetas: vermelha = bug crítico, laranja = bug importante, azul = app, roxa = API/deploy, verde = painel, amarela = proposta.

## Estado atual (atualize esta seção ao terminar um sprint)

- **Sprint 0 (fundação da API): concluído (29/09/2026).** Solução inteira compila sem avisos, 100 testes passam, migração `InitialCreate` aplicada no PostgreSQL local (porta 5433) e login/`/api/auth/me` testados. Pendente: proteger a `main` no GitHub.
- **App MAUI: MVP funcionando, com bugs de dinheiro** (docs/06 — B1 venda rápida quebrada, B2 pagamento grava valor entregue, B3 aceita dinheiro insuficiente, B4/B5 totais desatualizados, B6 `double`, B7 erros silenciosos).
- **Próximo:** Etapa A do app (base sólida) e Sprint 1 da API (sincronização).

## Comandos

```powershell
docker compose up -d                                   # PostgreSQL local (localhost:5433, sorveteria/postgres/postgres — 5433 para não brigar com PostgreSQL instalado no Windows)
dotnet build src/GestaoSorveteria.Server               # API
dotnet test  tests/GestaoSorveteria.Tests              # testes
dotnet run --project src/GestaoSorveteria.Server       # Swagger: http://localhost:5080/swagger (admin / admin12345 em Development)

# Migrações (sempre com -p Infrastructure -s Server)
dotnet ef migrations add <Nome> -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server -o Persistence/Migrations
dotnet ef database update      -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server
```

Os testes rodam no **Microsoft Testing Platform** (`"test": { "runner": "Microsoft.Testing.Platform" }` no `global.json`), exigido pelo xUnit v3 4.x no .NET 10 — não adicione `Microsoft.NET.Test.Sdk` nem `xunit.runner.visualstudio`. Filtro: `dotnet test --project tests/GestaoSorveteria.Tests --filter-class "<Namespace.Classe>"`.

O app MAUI compila só com o workload do .NET MAUI (Visual Studio no Windows). No emulador Android a API local fica em `http://10.0.2.2:5080`. O CI (GitHub Actions) compila e testa **só a API**.

## Arquitetura (regra de ouro)

```
Domain  ←  Application  ←  Infrastructure  ←  Server
   ↑            ↑
Contracts ──────┘            Mobile → Domain, Contracts        Tests → Domain, Application, Infrastructure
```

- `Domain` e `Contracts`: **zero pacotes NuGet**, zero referência a outros projetos (além de Contracts ser referenciado).
- `Domain` e `Application` **nunca** referenciam `Infrastructure`, `Server` ou `Mobile`.
- **Regra de negócio mora na entidade** (método que valida e lança `DomainException`). `Application` orquestra (carrega → chama o domínio → salva → mapeia para DTO). Controller só recebe e devolve.
- Endpoint nunca expõe entidade: sempre DTO de `Contracts`.
- Todo acesso a banco é `async`. Repositórios só marcam alterações; quem confirma é `IUnitOfWork.SaveChangesAsync()` no fim do caso de uso.
- Erros HTTP: `DomainException` → 400, `EntidadeNaoEncontradaException` → 404, `AcessoNegadoException` → 403, violação de unicidade no PostgreSQL → 409 (ProblemDetails, `DomainExceptionHandler`).

## Invariantes que não podem quebrar

- **Dinheiro é `decimal`**, 2 casas, `MidpointRounding.AwayFromZero` (`Moeda.Arredondar`). Nunca `double`/`float`. No banco: `numeric(12,2)`.
- **Datas em UTC** (`DateTime.Kind == Utc`, `timestamptz`). "Hoje/semana/mês" usa `DiaComercial` (America/Sao_Paulo).
- **Ids são `Guid` gerados no cliente/domínio**, nunca pelo banco (`ValueGenerated.Never` no `AppDbContext`). Isso sustenta a idempotência da sincronização.
- **Nada de venda, pagamento, caixa ou movimento é apagado.** Correção = novo registro com motivo (cancelar, estornar, ajuste).
- Pagamento: `Valor` abate da comanda; em dinheiro `ValorRecebido ≥ Valor` e `Troco = ValorRecebido − Valor` (calculado, nunca digitado). Soma dos pagamentos = total **exato**.
- Caixa: `esperado = fundo + vendas em dinheiro + suprimentos − sangrias`; `diferença = contado − esperado`. Só um caixa aberto.
- Item livre (self-service / venda avulsa) tem `ProdutoId = null` — nunca um Id inventado.
- Enums gravados como texto; tabelas e colunas em `snake_case`.
- Segredos só em variáveis de ambiente. `appsettings.Development.json` contém apenas valores locais de desenvolvimento.

## Convenções de código

- C# com `Nullable` e `ImplicitUsings` (via `Directory.Build.props`), namespaces com escopo de arquivo, chaves sempre.
- Nomes em **português** para o domínio (`Comanda`, `Caixa`, `FecharAsync`); sufixo `Async` em métodos assíncronos; interfaces com `I`; DTOs `NomeDto` / `NomeRequest` / `NomeResponse`; rotas `/api/<plural-kebab-case>`.
- Mensagens de erro para o usuário em português, prontas para aparecer na tela.
- Cite o ID da regra (`RN-CM-07`) ou do bug (`B3`) em comentários e testes quando a linha existir por causa dela.
- Testes: `Metodo_Cenario_Resultado` (ex.: `Fechar_DinheiroRecebidoMenorQueValor_Lanca`). Domínio sem mocks; aplicação com fakes em memória (`tests/.../Application/Fakes.cs`).

## Git

- `main`: só código testado (merge vindo da `develop`). `develop`: dia a dia. `feature/<nome>`, `fix/<nome>` a partir da `develop`.
- Commit: `tipo(escopo): descrição` em português — tipos `feat`, `fix`, `refactor`, `test`, `docs`, `chore`; escopos `app`, `api`, `domain`, `infra`, `painel`, `repo`, `docs`. Ex.: `fix(app): recusa dinheiro insuficiente no pagamento (B3)`.
- Antes de commitar: `dotnet build` + `dotnet test` verdes; nenhum segredo no diff.

## Desenvolvimento automático (Claude Code)

- **Ciclo:** a skill `proximo-passo` pega a próxima tarefa do `docs/04`, cria a branch, implementa com os agentes, roda build/testes, passa pelo `revisor-dominio`, abre PR para a `develop`, acompanha o CI e faz o merge quando verde. PR de app que precisa de teste no celular fica aberto para o Paulo.
- **Permissões:** `.claude/settings.json` libera build/test/ef/docker/git/gh sem perguntar e **bloqueia** push na `main`, `--force`, `reset --hard`, `rm -rf` e apagar o banco.
- **Duas frentes em paralelo:** use `git worktree` (ex.: `../AppSorveteria-app` na branch do app) e uma sessão do Claude Code em cada pasta, para API e app não se atropelarem.
- **CI:** `CI` (API + testes, Ubuntu) em todo push/PR; `App Android` (build do MAUI, Windows) quando o app, Domain ou Contracts mudam.
- **Merge na `main`:** só o Paulo, por PR da `develop`.

## Como trabalhar aqui (para o Claude)

- Comece lendo o doc relevante (tabela acima) e o cartão do Trello da tarefa.
- Mudança de regra de negócio: atualize `docs/02` **e** escreva o teste antes/junto do código.
- Mudança de schema: gere migração (skill `migracao-ef`), nunca edite migração já aplicada.
- Ao terminar uma tarefa: use a skill `fechar-tarefa` (build/test, commit, docs, Trello).
- Agentes disponíveis em `.claude/agents/`: `dev-api`, `dev-mobile`, `revisor-dominio`, `testador`.
- Skills em `.claude/skills/`: `proximo-passo` (ciclo automático), `nova-funcionalidade-api`, `corrigir-bug-mvp`, `migracao-ef`, `sincronizacao-local-first`, `fechar-tarefa`.
- Linguagem com a dona (docs, guias, mensagens do app): sem jargão técnico.
