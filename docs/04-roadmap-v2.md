# 04 — Roadmap v2

> Atualizado em 29/09/2026: repositório único, modelo local-first, app a partir do MVP (docs/06), painel estático e hospedagem gratuita (docs/07). O quadro do Trello "Sorveteria" espelha este roadmap.

**Proposta** fala em *Fases* (o que a dona recebe). **Roadmap** fala em *Sprints* (semanas de trabalho). A Fase 1 tem 5 sprints (Sprint 0 + 4). Cada sprint termina com algo **funcionando e demonstrável**.

```
Fase 1 — Sistema básico ............ Sprint 0 · 1 · 2 · 3 · 4   (5 semanas)
Fase 2 — Estoque inteligente ....... Sprint 5 · 6 · 7           (+3 semanas)
Fase 3 — Análise e relatórios ...... Sprint 8 · 9               (+2 semanas)
Fase 4 — Expansões ................. a combinar
```

## Fase 1 — Sistema básico

### Sprint 0 — Fundação ✅ (entregue nesta reestruturação)

- [x] Solução `GestaoSorveteria` em .NET 10 com 6 projetos (Domain, Contracts, Application, Infrastructure, Server, Tests) e referências corretas
- [x] Entidades da Fase 1 com regras de negócio dentro (Usuario, Produto, Caixa, MovimentoCaixa, Comanda, ItemComanda, Pagamento)
- [x] `AppDbContext` + configurações EF Core (snake_case, enums como texto, índices, precisão de dinheiro)
- [x] Hash de senha PBKDF2, `IClock`, seed do Admin
- [x] JWT + `POST /api/auth/login` + `GET /api/auth/me`, rate limit no login, Swagger com botão Authorize, ProblemDetails
- [x] Testes de domínio e aplicação (xUnit v3) — regras de comanda, pagamento/troco, caixa, política de senha, dia comercial
- [x] `docker-compose.yml` (PostgreSQL local), `Dockerfile`, CI no GitHub Actions, `.gitignore`, `.editorconfig`
- [x] Migração `InitialCreate` gerada e aplicada no PostgreSQL local (7 tabelas conferidas); login e `/api/auth/me` testados com a API rodando (29/09/2026)
- [x] Repositório único `AppSorveteria` com o MVP em `src/GestaoSorveteria.Mobile` (histórico preservado) + solução do Sprint 0 + docs — branch `develop`
- [x] `git push -u origin develop`
- [x] **Você:** proteger a `main` no GitHub (PR obrigatório + check "backend")

**Pronto quando:** `POST /api/auth/login` devolve token no Swagger e `dotnet test` está verde.

### Sprint 1 — API de sincronização (semana 1)

- [x] Contracts de sincronização: comanda completa (itens + pagamentos) e caixa (movimentos + fechamento) — `Contracts/Sync/SyncContracts.cs`
- [ ] `SyncService` + `SyncController`: `POST /api/sync/caixas` e `POST /api/sync/comandas` (lote, idempotente pelo `Id`, resultado por item: aceita / já recebida / rejeitada)
- [ ] Reconstruir os agregados com as regras do Domain; marcar vendas recebidas após o fechamento do caixa (RN-CX-08)
  - [x] Regras RN-CX-05/07/08/10 e RN-SY-01/03/04/05/06 no docs/02; Domain: `Comanda.Remontar` (preço da venda, caixa fechado), troco conferido, `Caixa.FecharSincronizado` + migração `CaixaDivergenciaSincronizacao`
  - [x] `POST /api/sync/caixas` (SyncService + SyncController): documento por documento, `DomainException` → `rejeitada`; falha de banco → erro HTTP e o app reenvia
  - [x] `POST /api/sync/comandas` (SyncService + SyncController): `Comanda.Remontar`, número/Ids/produto conferidos antes (rejeição em vez de 409/500), reenvio → `ja_recebida`
- [x] `ProdutoService` + `ProdutosController`: `GET /api/produtos?desde=` para o app (inclui inativos; `geradoEmUtc` vira o próximo `desde`)
- [x] `ProdutosController`: criar, editar, ativar/desativar (Admin)
- [ ] `POST /api/comandas/{id}/estornar` (Admin)
- [x] `GET /api/versao`
- [ ] Testes de aplicação com fakes, incluindo reenvio duplicado e lote com uma comanda inválida
- [x] Arquivo `.http` com o fluxo: login → produtos → sync de caixa → sync de comandas — `src/GestaoSorveteria.Server/Sync.http` (falta rodar contra o PostgreSQL local)

**Pronto quando:** um lote enviado duas vezes grava cada venda uma única vez e os totais do caixa batem no banco.

### Sprint 2 — App do atendente: refatoração do MVP (semana 2)

Plano detalhado em docs/06, seção 4.

- [ ] **Etapa A — base sólida:** referências a Domain/Contracts, `decimal`, UTC, enums, camada de dados nova, erros visíveis, numeração por caixa
- [ ] **Etapa B — vendas:** pagamento com troco correto e dividido, venda rápida, cancelar comanda, busca, delivery, aviso rápido em vez de alerta
- [ ] **Etapa C — caixa, login e sincronização:** abrir/sangria/fechar caixa, login com PIN, `SyncService`, indicador "aguardando envio"
- [ ] Teste no emulador (API em `10.0.2.2`) e no celular real com a internet desligada

**Pronto quando:** venda completa no celular real sem internet, sincronizada ao religar, com o caixa batendo.

### Sprint 3 — Painel da dona (site estático) (semana 3)

- [x] Decidir a tecnologia: **React + Vite + TypeScript** em `src/GestaoSorveteria.Painel` (ADR 018, docs/01)
- [x] Projeto do painel no repositório (Vite + React + TS, React Router, TanStack Query, ESLint, CI `Painel`); configuração do Cloudflare Pages documentada (docs/07)
- [x] CORS na API com a origem do painel vinda de configuração (`Cors:PainelOrigem`)
- [x] Login da dona pela API (JWT, perfil Admin; sai sozinho quando o token vence ou a API responde 401)
- [ ] Dashboard do dia: total vendido, nº de comandas, ticket médio, por forma de pagamento, caixa atual (esperado × contado)
- [x] Vendas por data com itens e pagamentos; estorno (status `Estornada` — RN-CM-09)
- [x] Produtos: cadastrar, editar, ativar/desativar, ordem dos botões (tela do painel)
- [x] Caixas: histórico de fechamentos com diferenças (`GET /api/caixas*` + tela do painel)
- [ ] Usuários: criar atendente, redefinir PIN
- [x] Tela "Conectando ao servidor…" enquanto a API acorda (novas tentativas automáticas)

**Pronto quando:** a dona vê, do celular dela, o que foi vendido hoje e se o caixa bateu.

### Sprint 4 — Piloto na loja com hospedagem gratuita (semana 4)

Detalhes em docs/07.

- [ ] API no Render Free (Docker) + PostgreSQL no Neon Free; variáveis de ambiente; `/health`
- [ ] Painel no Cloudflare Pages apontando para a API
- [ ] `pg_dump` semanal guardado fora da nuvem; testar a restauração
- [ ] APK assinado + link de instalação; `/api/versao` e aviso de atualização no app
- [ ] Fechamento forçado do caixa pelo painel (RN-CX-09 — adiado do Sprint 3)
- [ ] Instalar no celular da sorveteria; cadastrar produtos reais com a dona; simular um turno inteiro
- [ ] Corrigir o que aparecer no teste real
- [ ] Guia de uso de 1 página (com prints): abrir caixa, vender, fechar caixa, ver o painel

**Pronto quando:** a dona opera um dia inteiro sem ajuda. Fim do piloto → decidir se sai do gratuito (docs/07).

## Fase 2 — Estoque inteligente

### Sprint 5 — Insumos e ficha técnica
- [ ] Entidades `Insumo`, `FichaTecnica`, `MovimentoEstoque` + migração
- [ ] Painel: cadastro de insumos (unidade, mínimo), ficha técnica por produto, entrada de reposição (atualiza custo médio)

### Sprint 6 — Baixa automática e alertas
- [ ] Ao fechar comanda: baixa por ficha técnica (RN-ES-02); estorno devolve (RN-ES-03)
- [ ] Ajuste/perda com motivo; histórico de movimentos por insumo
- [ ] Alertas de mínimo no dashboard e lista "repor hoje"

### Sprint 7 — Previsão de ruptura
- [ ] Consumo médio diário (14 dias) e dias restantes (RN-ES-07); lista de compras sugerida (quanto comprar para X dias)
- [ ] Teste com dados reais de 2 semanas; ajuste de mínimos com a dona

## Fase 3 — Análise e relatórios

### Sprint 8 — Gráficos e ranking
- [ ] Vendas por dia/semana/mês, comparativo com o período anterior, horários de pico
- [ ] Ranking de produtos (quantidade e valor), por categoria

### Sprint 9 — Lucro e exportação
- [ ] Custo estimado por produto (RN-PR-07), margem por produto e por período
- [ ] Exportação CSV/Excel por período; relatório de fechamentos de caixa

## Fase 4 — Expansões (a combinar)
Fidelidade por pontos · impressora de cupom · integração iFood · relatório financeiro com custo de produção · multi-loja.

## Regras de trabalho (mantidas do roadmap original)

- `main` só recebe código testado; trabalho diário em `develop`; `feature/<nome>` e `fix/<nome>`.
- Commit no fim de cada sessão: `tipo(escopo): descrição` — ex.: `feat(api): fechar comanda com múltiplos pagamentos`, `test(domain): troco em dinheiro`.
- Nunca expor entidade no endpoint (sempre DTO de `Contracts`). Toda regra no Domain/Application; controller só recebe e devolve.
- Nada de segredo no Git (`appsettings.Development.json` só com valores locais; produção via variáveis de ambiente).
- Antes de qualquer tela: endpoint testado no Swagger.
- Mostrar algo funcionando para a dona toda semana; mudança de escopo entra na fila da próxima fase.

## Checklist de entrega de fase

- [ ] Endpoints da fase testados no Swagger com dados reais
- [ ] `dotnet test` verde e CI verde
- [ ] App funciona no celular físico; painel funciona no celular da dona
- [ ] Nenhuma senha/chave no repositório
- [ ] `main` atualizada e taggeada (`v1.0.0`, `v2.0.0`…)
- [ ] A dona consegue usar sem explicação a cada clique
