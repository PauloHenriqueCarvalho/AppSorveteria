# 04 — Roadmap v2

> Atualizado em 07/10/2026 (sincronizado com a `develop`): Sprint 1 inteiro, Etapas A e B do app e Sprint 3 menos usuários estão entregues. Plano para fechar a Fase 1 em docs/08. O quadro do Trello "Sorveteria" espelha este roadmap.

**Proposta** fala em *Fases* (o que a dona recebe). **Roadmap** fala em *Sprints* (semanas de trabalho). A Fase 1 tem 5 sprints (Sprint 0 + 4). Cada sprint termina com algo **funcionando e demonstrável**.

```
Fase 1 — Sistema básico ............ Sprint 0 · 1 · 2 · 3 · 4   (5 semanas)
Fase 2 — Estoque e pedidos ......... Sprint 5 · 6 · 7           (+3 semanas)
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

### Sprint 1 — API de sincronização ✅ (concluído em 06/10/2026)

- [x] Contracts de sincronização: comanda completa (itens + pagamentos) e caixa (movimentos + fechamento) — `Contracts/Sync/SyncContracts.cs`
- [x] `SyncService` + `SyncController`: `POST /api/sync/caixas` e `POST /api/sync/comandas` (lote, idempotente pelo `Id`, resultado por item: aceita / já recebida / rejeitada) (#21, #31)
- [x] Reconstruir os agregados com as regras do Domain; marcar vendas recebidas após o fechamento do caixa (RN-CX-08)
  - [x] Regras RN-CX-05/07/08/10 e RN-SY-01/03/04/05/06 no docs/02; Domain: `Comanda.Remontar` (preço da venda, caixa fechado), troco conferido, `Caixa.FecharSincronizado` + migração `CaixaDivergenciaSincronizacao`
  - [x] `POST /api/sync/caixas` (SyncService + SyncController): documento por documento, `DomainException` → `rejeitada`; falha de banco → erro HTTP e o app reenvia
  - [x] `POST /api/sync/comandas` (SyncService + SyncController): `Comanda.Remontar`, número/Ids/produto conferidos antes (rejeição em vez de 409/500), reenvio → `ja_recebida`
- [x] `ProdutoService` + `ProdutosController`: `GET /api/produtos?desde=` para o app (inclui inativos; `geradoEmUtc` vira o próximo `desde`)
- [x] `ProdutosController`: criar, editar, ativar/desativar (Admin)
- [x] `POST /api/comandas/{id}/estornar` (Admin) — status `Estornada`, não altera o caixa (RN-CM-09) (#37)
- [x] `GET /api/versao`
- [x] Testes de aplicação com fakes, incluindo reenvio duplicado e lote com uma comanda inválida (`SyncCaixasTests`, `SyncComandasTests`)
- [x] Arquivo `.http` com o fluxo: login → produtos → sync de caixa → sync de comandas → fechamento — `src/GestaoSorveteria.Server/Sync.http` (#35)
- [x] Consultas para o painel (adiantadas do Sprint 3): `GET /api/caixas*` (#27), `GET /api/comandas*` (#30), `GET /api/relatorios/resumo-dia` (#34)

**Pronto quando:** um lote enviado duas vezes grava cada venda uma única vez e os totais do caixa batem no banco. ✅ (`FluxoCompleto_CaixaComandasEFechamento_TotaisDoCaixaBatem`)

### Sprint 2 — App do atendente: refatoração do MVP (semana 2)

Plano detalhado em docs/06, seção 4.

- [x] **Etapa A — base sólida:** referências a Domain/Contracts, `decimal`, UTC, enums, camada de dados nova, erros visíveis, numeração por caixa — testada no tablet em 30/09/2026, modo avião incluído (docs/06, seção 2.1)
- [x] **Etapa B — vendas:** pagamento com troco correto e dividido (#23), venda rápida (#15), cancelar comanda (#24), busca (#28), delivery (#29), toast em vez de alerta (#26)
- [ ] **Etapa C — caixa, login e sincronização** (sequência C1–C5 em docs/08, Sessão B):
  - [x] C1 — Login com PIN + cliente HTTP (token em `SecureStorage`, URL da API em configuração) — PR aberto para teste no tablet
  - [x] C2 — Catálogo vindo da API (`GET /api/produtos?desde=`, cache no SQLite; resolve B16) — PR aberto para teste no tablet
  - [x] C3 — Caixa de verdade (abrir, sangria/suprimento, fechar com conferência); remove o `CaixaProvisorio` — PR aberto para teste no tablet
  - [x] C4 — `SyncService` + indicador "X vendas aguardando envio" (o ciclo de 1 min também atualiza o catálogo) — PR aberto para teste no tablet
  - [x] C5 — Histórico do turno com status de envio (aba **Turno**) — PR aberto para teste no tablet
- [ ] Teste no emulador (API em `10.0.2.2`) e no celular real com a internet desligada

**Pronto quando:** venda completa no celular real sem internet, sincronizada ao religar, com o caixa batendo.

### Sprint 3 — Painel da dona (site estático) ✅ (concluído em 07/10/2026)

- [x] Decidir a tecnologia: **React + Vite + TypeScript** em `src/GestaoSorveteria.Painel` (ADR 018, docs/01)
- [x] Projeto do painel no repositório (Vite + React + TS, React Router, TanStack Query, ESLint, CI `Painel`); configuração do Cloudflare Pages documentada (docs/07)
- [x] CORS na API com a origem do painel vinda de configuração (`Cors:PainelOrigem`)
- [x] Login da dona pela API (JWT, perfil Admin; sai sozinho quando o token vence ou a API responde 401)
- [x] Dashboard do dia: total vendido, nº de comandas, ticket médio, por forma de pagamento, caixa atual (esperado × contado)
- [x] Vendas por data com itens e pagamentos; estorno (status `Estornada` — RN-CM-09)
- [x] Produtos: cadastrar, editar, ativar/desativar, ordem dos botões (tela do painel)
- [x] Caixas: histórico de fechamentos com diferenças (`GET /api/caixas*` + tela do painel)
- [x] Usuários: criar atendente, redefinir PIN (RN-US-01…08)
  - [x] API: `GET/POST /api/usuarios`, `POST /api/usuarios/{id}/redefinir-senha`, `POST /api/usuarios/{id}/ativar|desativar` (Admin; RN-US-10: não desativa a si mesmo) (#47)
  - [x] Painel: tela "Usuários" — lista, novo usuário (atendente com PIN ou Admin com senha), redefinir PIN/senha, ativar/desativar (#50)
- [x] Tela "Conectando ao servidor…" enquanto a API acorda (novas tentativas automáticas)

**Pronto quando:** a dona vê, do celular dela, o que foi vendido hoje e se o caixa bateu. ✅ (falta só publicar no Cloudflare — Sprint 4)

### Painel profissional (cara de ERP, ADR 020) — 14/10/2026
- [x] Estrutura: menu lateral agrupado, barra superior com trilha e data, gaveta + barra de atalhos no celular, marca Verão Tropical, login com a marca
- [x] Componentes: cabeçalho de página, indicador, seção, selo, barra, esqueleto, tabela; folha de estilo nova (as telas antigas já herdam)
- [x] Início: 4 indicadores, formas de pagamento, "Precisa da sua atenção", últimas vendas em tabela, caixa atual
- [x] API do dashboard: vendas por hora, comparativo com o mesmo dia da semana anterior, mais vendidos (RN-RL-03/06/07); gráfico no Início
- [x] Vendas e Caixas em tabela com filtro de período (atalhos Hoje, Ontem, 7 dias, 30 dias, Este mês) e situação, exportar Excel (CSV brasileiro) e imprimir; relatório de fechamento imprimível no detalhe do caixa
- [ ] Filtro de vendas por atendente e por forma de pagamento (precisa de parâmetro novo na API)
- [ ] Produtos e Usuários no mesmo padrão

### Sprint 4 — Piloto na loja com hospedagem gratuita (semana 4)

Detalhes em docs/07.

- [ ] API no Render Free (Docker) + PostgreSQL no Neon Free; variáveis de ambiente; `/health`
- [ ] Painel no Cloudflare Pages apontando para a API
- [ ] `pg_dump` semanal guardado fora da nuvem; testar a restauração — script `scripts/backup.ps1` e passo a passo de restauração prontos (docs/07, 07/10); falta agendar contra o Neon
- [ ] APK assinado + link de instalação; `/api/versao` e aviso de atualização no app — script `scripts/publicar-apk.ps1` e passo a passo (keystore fora do Git, link fixo, instalação) prontos (docs/07, 07/10); falta gerar o APK oficial e publicar o link
- [x] Fechamento forçado do caixa pelo painel (RN-CX-09 — adiado do Sprint 3; entregue em 07/10/2026)
  - [x] API: `POST /api/caixas/{id}/fechar-forcado` com motivo, contado = 0, diferença registrada; caixa marcado `fechamento_forcado` (migração `CaixaFechamentoForcado`) (#51)
  - [x] Painel: botão "Fechar este caixa pelo painel" no detalhe do caixa aberto, com motivo e confirmação; etiqueta "Fechado pelo painel"
- [ ] Instalar no celular da sorveteria; cadastrar produtos reais com a dona; simular um turno inteiro
- [ ] Corrigir o que aparecer no teste real
- [ ] App: quantidade digitada no item (tocar no número abre teclado numérico; "50 picolés" sem 49 toques) — RN-ES-04, pode adiantar da Fase 2
- [ ] Guia de uso de 1 página (com prints): abrir caixa, vender, fechar caixa, ver o painel — texto pronto em `docs/09-guia-de-uso.md` (07/10); faltam os prints e os links, depois do deploy e da Etapa C

**Pronto quando:** a dona opera um dia inteiro sem ajuda. Fim do piloto → decidir se sai do gratuito (docs/07).

## Fase 2 — Estoque, avisos e pedidos ao fornecedor

Redesenhada em 13/10 e revista com o Paulo em 14/10 (docs/10): picolé e massa por **contagem semanal**, potes com **sabor na venda** (teste), botão **"Está acabando"** com prioridade, **urgente na hora e resumo do dia** pelo WhatsApp da dona, **fornecedores separados** e **pedido impresso** para a fábrica.

### Sprint 5 — Catálogo, contagem e avisos
- [x] Domain: `Fornecedor`, `GrupoEstoque` (modo de controle), `ItemEstoque`, `Contagem`, `Aviso`, `CalculoEstoque` + 24 testes (RN-ES-01…07, RN-AV-01/03/05/08, RN-PF-01) — 14/10
- [x] Application: catálogo — fornecedores, grupos, itens e importação da planilha com prévia (RN-ES-10/11, RN-PF-09) + testes — 14/10
- [x] Infrastructure + API do catálogo: tabelas da Fase 2 (migração `Fase2Estoque`), `GET /api/estoque/catalogo` e alterações (Admin), `GET /api/estoque/itens` (app, sem custo) — 14/10
- [x] API: avisos (criar sem duplicar, "ainda falta", fechar, WhatsApp aberto), resumo do dia, configuração (WhatsApp da dona), `POST /api/sync/avisos` (RN-AV-01…10) — 14/10
- [x] API: contagens pelo painel (modelo, rascunho, concluir) e `POST /api/sync/contagens`; `diasDeContagem` para o lembrete do app (RN-ES-05/06) — 14/10
- [x] Saída da semana (RN-ES-07): média por semana entre contagens, somando o que foi recebido no intervalo — entra no pedido sugerido (Sprint 6) — 14/10
- [x] Diferenças da contagem dos potes no resumo do dia (sistema × contado, mais de 1) — Sprint 7
- [ ] App: quantidade digitada na venda (RN-ES-04); botão **"Está acabando"** (busca, quantidade, prioridade, sem duplicar); mensagem **urgente** pelo WhatsApp; **resumo do dia** no fechamento do caixa
- [ ] App: tela de **contagem semanal** na ordem da planilha (meia caixa, "não tem nenhum", salvar parcial); lembrete na véspera do pedido
- [x] Painel: catálogo de estoque (por grupo, filtros, exportar), importação da planilha com prévia, item, grupo (modo de controle) e fornecedores — 14/10
- [x] Painel: avisos (abertos/fechados, contador no menu, novo, "ainda falta", fechar com motivo), resumo do dia, configurações (WhatsApp da dona) — 14/10
- [x] Painel: contagem semanal (lista, nova com a anterior de referência, rascunho, concluir, ver concluída) — 14/10

**Pronto quando:** o atendente avisa "acabou Coco preto" e a dona recebe no WhatsApp na hora; no fim do dia chega o resumo; a contagem semanal substitui a planilha.

### Sprint 6 — Pedido, impressão e entrega
- [x] Domain: `PedidoFornecedor` + linhas, status, diferenças (RN-PF-05/06/08, RN-RC-02/03) + testes — 14/10
- [x] API: montar sugerido por fornecedor (saída semanal, alvo, avisos abertos, faltou, entregas depois da contagem e o que está a caminho — RN-PF-02…04), +/−, enviar (mensagem do WhatsApp), cancelar, receber (fecha avisos, custo novo, mensagem das diferenças), histórico; migração `PedidosFornecedor` — 14/10
- [x] Painel: pedido por fornecedor (rascunho montado sozinho no dia do pedido, +/−, acrescentar item, refazer a sugestão) com **Imprimir** (folha A4 com colunas Veio/Obs., RN-PF-07) e **WhatsApp**; conferência da entrega ("chegou tudo certo" ou só as diferenças, sabor trocado, preço novo) com mensagem para o fornecedor; histórico de 30 dias no fornecedor; `GET /api/pedidos-fornecedor/{id}/mensagens` — 14/10

**Pronto quando:** a dona imprime o pedido da semana para os pais, confere a entrega pela folha preenchida em menos de 2 minutos e o que faltou volta no pedido seguinte.

### Sprint 7 — Potes automáticos (teste)
- [x] Domain: produto ligado a grupo (RN-PR-06), sabor na linha da venda, movimentos de estoque e saldo com a contagem como base; hora em que a entrega chegou e em que cada item foi contado (RN-ES-08/09) — #81
- [x] API: `ItemEstoqueId` opcional no sync com baixa no servidor, estorno, entrada na conferência, aviso automático de mínimo e zerado (RN-AV-04), `GET /api/estoque/saldos`, `PUT /api/produtos/{id}/estoque`, pedido dos potes pelo saldo e pelas vendas
- [ ] Painel: produto ligado ao grupo, saldo dos potes, diferença da contagem (saldo × contado) e no resumo do dia
- [ ] Virada: **fazer uma contagem dos potes antes de ligar os produtos de pote** (o saldo parte dela; entregas conferidas antes não têm entrada)
- [ ] App: grade de sabores **só nos potes** (produto com `pedeSabor`), `itemEstoqueId` na venda, hora de cada item na contagem
- [ ] Avaliar com a dona depois de 2 semanas: se as diferenças dos potes forem grandes, o grupo volta para `Contagem`

## Fase 3 — Análise e relatórios

### Sprint 8 — Gráficos e ranking
- [ ] Vendas por dia/semana/mês, comparativo com o período anterior, horários de pico
- [ ] Ranking de produtos (quantidade e valor), por categoria

### Sprint 9 — Lucro e exportação
- [ ] Custo estimado por produto (RN-PR-07, pelo custo da embalagem do estoque), margem por produto, por sabor e por período
- [ ] Exportação CSV/Excel por período; relatório de fechamentos de caixa

## Fase 4 — Expansões (a combinar)
Ficha técnica de insumos (se a dona quiser controlar insumo além do produto pronto) · fidelidade por pontos · impressora de cupom · integração iFood · relatório financeiro com custo de produção · multi-loja.

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
