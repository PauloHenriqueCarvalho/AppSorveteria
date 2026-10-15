# CLAUDE.md — Gestão Sorveteria (AppSorveteria)

Guia para o Claude (e para qualquer dev) trabalhar neste repositório. Leia inteiro antes de mudar código.

## O que é

Sistema de gestão para uma sorveteria pequena, feito por Paulo (dev .NET) para a dona da loja.

| Parte | Onde | Tecnologia |
|---|---|---|
| App do atendente (balcão, Android, **funciona offline**) | `src/GestaoSorveteria.Mobile` | .NET MAUI + SQLite (`sqlite-net-pcl`) |
| API | `src/GestaoSorveteria.Server` + `Domain`, `Contracts`, `Application`, `Infrastructure` | ASP.NET Core 10, EF Core 10, PostgreSQL (Npgsql), JWT |
| Testes | `tests/GestaoSorveteria.Tests` | xUnit v3 |
| Painel da dona (site estático, Cloudflare Pages) | `src/GestaoSorveteria.Painel` | React + Vite + TypeScript, npm (ADR 018) — nunca calcula dinheiro, só mostra o que a API devolve |

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
| `06-analise-mvp-maui.md` | **Antes de mexer no app.** Bugs B1–B16 e plano das Etapas A/B/C |
| `07-hospedagem.md` | Deploy, limites dos planos gratuitos, variáveis de ambiente |
| `08-plano-do-dia-2026-10-07.md` | Plano para fechar a Fase 1: o que falta em cada frente e as 3 sessões em paralelo (A = API/painel, B = app Etapa C, C = deploy) |
| `09-guia-de-uso.md` | Guia de 1 página para a dona e o atendente, sem jargão. Mudou uma tela ou um fluxo do app/painel → atualize aqui |
| `10-fase2-estoque-e-pedidos.md` | **Antes da Fase 2.** Planilha da dona analisada, estoque de produto pronto, pedido sugerido, conferência da entrega, perguntas para a dona. Catálogo inicial em `fase2/catalogo-inicial.csv` |
| `contexto-mvp/` | Documentos originais do MVP (histórico, não é fonte de verdade) |

Quadro de tarefas: Trello "Sorveteria" (listas Backlog, A fazer, Em Andamento, Concluido, Dúvidas para a dona). Etiquetas: vermelha = bug crítico, laranja = bug importante, azul = app, roxa = API/deploy, verde = painel, amarela = proposta.

## Estado atual (atualize esta seção ao terminar um sprint)

- **Sprint 0 (fundação da API): concluído (29/09/2026).** Migração `InitialCreate` aplicada no PostgreSQL local (porta 5433), login/`/api/auth/me` testados.
- **Sprint 1 (API de sincronização): concluído (06/10/2026).** `POST /api/sync/caixas` e `/api/sync/comandas` idempotentes com remontagem pelo Domain, produtos (app e painel), estorno (RN-CM-09), `/api/versao`, consultas de caixas/vendas/resumo do dia para o painel, `Sync.http`. Em 07/10: usuários (`/api/usuarios`, RN-US-10) e fechamento forçado do caixa (`/api/caixas/{id}/fechar-forcado`, RN-CX-09). 300 testes passam, CI verde.
- **App MAUI: Etapas A, B e C na develop** (docs/06, docs/08). B1–B14 corrigidos e conferidos no tablet (30/09/2026); pagamento dividido, cancelar, busca, delivery e toast; Etapa C: login com PIN (C1), catálogo da API (C2, resolve B16), caixa de verdade (C3), `SyncService` (C4) e aba Turno (C5). Falta o teste no tablet com a internet desligada.
- **Painel (Sprint 3): concluído (07/10/2026).** Login, dashboard do dia, vendas com estorno, produtos, caixas (com fechamento forçado), usuários, "Conectando ao servidor…". Falta só publicar (Sprint 4).
- **Painel profissional (ADR 020, 14/10/2026): concluído.** Estrutura de ERP (menu lateral, trilha, indicadores, tabelas) e marca Verão Tropical. Componentes em `src/componentes/Ui.tsx` e `Icone.tsx`; tela nova usa esses componentes e as classes de `index.css`, sem biblioteca de UI. Vendas com filtro por atendente e forma de pagamento (#85); Produtos e Usuários em tabela (#87).
- **Deploy (Sprint 4): nada publicado ainda.** `Dockerfile`, `/health` e docs/07 prontos.
- **Fase 2 — estoque e pedidos (Sprints 5 e 6, 14/10/2026), na develop:** catálogo da planilha com fornecedores (importação com prévia), avisos "Está acabando" com urgente/resumo no WhatsApp da dona, contagem semanal (painel e `POST /api/sync/contagens`), pedido ao fornecedor (sugestão pela contagem estimada + entregas + a caminho + avisos + faltas, folha A4, WhatsApp, conferência da entrega, histórico) — #68–#75, #77, #79, #80. **App, abertos para o Paulo testar no tablet:** #67 (quantidade digitada) e #76 ("Está acabando", resumo do dia e a contagem semanal do #78, que já foi juntado nele).
- **Sprint 7 — potes com saldo pela venda (14/10/2026), na develop:** produto ligado ao grupo de estoque (RN-PR-06), sabor na venda, baixa na sincronização e estorno (RN-ES-09), entrada na conferência da entrega (RN-RC-03), aviso automático pelo saldo (RN-AV-04), saldos no painel (Potes) e conferência na contagem com diferenças no resumo do dia (RN-ES-08) — #81, #82, #84. **App aberto para o Paulo:** #83 (escolher o sabor na venda; juntar depois do #76). Antes de ligar os potes na loja: contar os potes uma vez (é a base do saldo).
- **Fase 3 — análise (Sprints 8 e 9, 15/10/2026), na develop:** relatório de período `GET /api/relatorios/periodo` e tela Análise → Relatórios — por dia/semana/mês, comparativo com o período anterior (RN-RL-09: semana anterior para menos de 7 dias, mesmos meses quando começa no dia 1, senão os N dias antes), horários e dias de pico, rankings de produtos, categorias e sabores (#86); custo estimado (RN-PR-07: catálogo do estoque ou informado, `GET /api/produtos/custos`, `PUT /api/produtos/{id}/custo`) e margem por produto, sabor e período (RN-RL-04) (#88). Depois de publicar: informar o custo do que não vem do catálogo (açaí, milk-shake…).
- **Próximo:** deploy (Sessão C do docs/08, espera as contas Neon/Render/Cloudflare), o teste dos PRs do app no tablet (#67, #76 → #83) e o piloto na loja. A Fase 4 é "a combinar" com a dona.

## Comandos

```powershell
docker compose up -d                                   # PostgreSQL local (localhost:5433, sorveteria/postgres/postgres — 5433 para não brigar com PostgreSQL instalado no Windows)
dotnet build src/GestaoSorveteria.Server               # API
dotnet test  tests/GestaoSorveteria.Tests              # testes (os de integração pulam sem banco)
$env:SORVETERIA_TESTES_PG="Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=postgres"
dotnet test  tests/GestaoSorveteria.Tests              # + integração: cria um banco temporário no PostgreSQL do docker compose, migra e apaga
dotnet run --project src/GestaoSorveteria.Server       # Swagger: http://localhost:5080/swagger (admin / admin12345 em Development)

# Migrações (sempre com -p Infrastructure -s Server)
dotnet ef migrations add <Nome> -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server -o Persistence/Migrations
dotnet ef database update      -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server

# Painel da dona (Node LTS) — rodar dentro de src/GestaoSorveteria.Painel
npm ci                                                 # instala dependências (package-lock.json)
npm run dev                                            # http://localhost:5173 (API em VITE_API_URL, padrão http://localhost:5080)
npm run lint                                           # ESLint
npm test                                               # Vitest (conversão de preço digitado, formatação em R$)
npm run build                                          # gera dist/ (o que vai para o Cloudflare Pages)
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
- **Migração sem `dotnet ef` à mão** (ex.: sessão na nuvem com NuGet bloqueado): commit numa branch `feature/**` com `[gerar-migracao: NomeEmPascalCase]` na mensagem; o workflow `Gerar migração EF` gera a migração + snapshot, confere que não sobrou mudança pendente, mostra o SQL no log e faz o commit na branch. Revise como manda a skill `migracao-ef`. O teste `Modelo_ComparadoComAsMigracoes_NaoTemMudancaPendente` reprova o CI quando o modelo muda sem migração.
- **CI:** `CI` (API + testes, Ubuntu, com um PostgreSQL de serviço para `tests/.../Integracao`) em todo push/PR; `App Android` (build do MAUI, Windows) quando o app, Domain ou Contracts mudam; `Painel` (npm ci, lint, testes, build) quando `src/GestaoSorveteria.Painel` muda. O `App Android` só dispara sozinho em PR para `main`/`develop`; num PR empilhado rode `gh api repos/PauloHenriqueCarvalho/AppSorveteria/actions/workflows/app.yml/dispatches -f ref=<branch>`.
- **Merge na `main`:** só o Paulo, por PR da `develop`.

## Como trabalhar aqui (para o Claude)

- Comece lendo o doc relevante (tabela acima) e o cartão do Trello da tarefa.
- Mudança de regra de negócio: atualize `docs/02` **e** escreva o teste antes/junto do código.
- Mudança de schema: gere migração (skill `migracao-ef`), nunca edite migração já aplicada.
- Ao terminar uma tarefa: use a skill `fechar-tarefa` (build/test, commit, docs, Trello).
- Agentes disponíveis em `.claude/agents/`: `dev-api`, `dev-mobile`, `revisor-dominio`, `testador`.
- Skills em `.claude/skills/`: `proximo-passo` (ciclo automático), `nova-funcionalidade-api`, `corrigir-bug-mvp`, `migracao-ef`, `sincronizacao-local-first`, `fechar-tarefa`.
- Linguagem com a dona (docs, guias, mensagens do app): sem jargão técnico.
