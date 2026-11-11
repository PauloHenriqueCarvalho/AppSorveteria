# 03 — Arquitetura e modelo de dados

> Atualizado em 29/09/2026: modelo **local-first** herdado do MVP (docs/06), painel da dona como **site estático** separado e hospedagem gratuita (docs/07).

## 1. Visão geral

```
 Celular Android (balcão)                      Navegador (PC ou celular da dona)
 ┌────────────────────────────────┐            ┌─────────────────────────────────┐
 │ GestaoSorveteria.Mobile (MAUI) │            │ Painel da dona (site estático)  │
 │ SQLite: produtos, caixa,       │            │ Cloudflare Pages                │
 │ comandas abertas, fila de envio│            └────────────────┬────────────────┘
 │ Regras: Domain (mesmo código)  │                             │ HTTPS + JWT (leitura/gestão)
 └───────────────┬────────────────┘                             │
                 │ HTTPS + JWT: envia vendas/caixas prontos,    │
                 │ baixa produtos (quando há internet)          │
                 ▼                                              ▼
 ┌──────────────────────────────────────────────────────────────────────────┐
 │ GestaoSorveteria.Server (ASP.NET Core 10) — Render Free (dorme 15 min)   │
 │  /api/*  controllers ──► Application (serviços) ──► Domain               │
 │                     Infrastructure (EF Core + Npgsql, hash, relógio)     │
 └───────────────────────────────────┬──────────────────────────────────────┘
                                     ▼
                         PostgreSQL — Neon Free (dorme 5 min)
```

- **Local-first:** a comanda nasce, recebe itens e fecha **no celular**. O servidor recebe a venda pronta. A venda nunca espera a internet.
- **Mesmas regras nos dois lados:** o app referencia `Domain` e `Contracts`; total, troco e fechamento de caixa são o mesmo código testado no celular e na API.
- **Painel separado:** site estático que só conversa com a API. Hospedagem grátis (docs/07).

## 2. Projetos e dependências

```
GestaoSorveteria.Domain        ◄── GestaoSorveteria.Application ◄── GestaoSorveteria.Infrastructure
        ▲                                   ▲                                   ▲
        │                                   │                                   │
GestaoSorveteria.Contracts ◄────────────────┘          GestaoSorveteria.Server ─┘ (também referencia Application)
        ▲
        └── GestaoSorveteria.Mobile (MAUI)          GestaoSorveteria.Tests ──► Domain, Application
```

| Projeto | Responsabilidade | Depende de | Pacotes NuGet |
|---|---|---|---|
| **Domain** | Entidades com regras, enums, exceções, interfaces de repositório | — | nenhum |
| **Contracts** | DTOs de request/response da API (compartilhados com o app) | — | nenhum |
| **Application** | Casos de uso (serviços), abstrações (`IClock`, `IPasswordHasher`, `ITokenService`), períodos/fuso | Domain, Contracts | nenhum |
| **Infrastructure** | `AppDbContext`, configurações EF, repositórios, hash PBKDF2, relógio, seed | Application | Npgsql.EntityFrameworkCore.PostgreSQL |
| **Server** | Controllers, JWT, Swagger, rate limit, ProblemDetails, CORS do painel, composição (DI) | Application, Infrastructure | JwtBearer, Swashbuckle, EF Design |
| **Mobile** | App do atendente (MVP em refatoração — docs/06): telas, SQLite, sincronização | Domain, Contracts (Etapa A) | MAUI, sqlite-net-pcl |
| **Painel** (Sprint 3) | Site estático da dona; consome a API | — (tipos TypeScript espelham os DTOs de Contracts) | React + Vite + TypeScript, npm (ADR 018) |
| **Tests** | xUnit v3: domínio e aplicação com fakes | Domain, Application | xunit.v3 |

**Regra de ouro (mantida):** Domain e Application nunca referenciam Infrastructure, Server ou Mobile. Dependências apontam para dentro.

### Estrutura de pastas

```
AppSorveteria/                   ← repositório único (GitHub)
├── GestaoSorveteria.slnx
├── Directory.Build.props        ← net10.0, nullable, warnings comuns a todos os projetos
├── docker-compose.yml           ← PostgreSQL local
├── Dockerfile                   ← imagem do Server para deploy
├── docs/                        ← 01–07 + contexto-mvp/ (documentos originais do MVP)
├── src/
│   ├── GestaoSorveteria.Domain/
│   │   ├── Common/              Entity, DomainException, Moeda, Guard
│   │   ├── Usuarios/            Usuario, PerfilUsuario, PoliticaSenha
│   │   ├── Produtos/            Produto
│   │   ├── Caixas/              Caixa, MovimentoCaixa, enums
│   │   ├── Comandas/            Comanda, ItemComanda, Pagamento, enums
│   │   └── Repositories/        IUsuarioRepository, IProdutoRepository, ICaixaRepository, IComandaRepository, IUnitOfWork
│   ├── GestaoSorveteria.Contracts/
│   │   ├── Auth/                LoginRequest, LoginResponse, UsuarioDto
│   │   └── Sync/                SyncCaixasRequest, SyncComandasRequest, SyncResponse (+ DTOs de caixa, movimento, comanda, item, pagamento)
│   ├── GestaoSorveteria.Application/
│   │   ├── Abstractions/        IClock, IPasswordHasher, ITokenService
│   │   ├── Auth/                AuthService
│   │   └── Common/              DiaComercial (fuso), exceções de aplicação
│   ├── GestaoSorveteria.Infrastructure/
│   │   ├── Persistence/         AppDbContext, Configurations/, Repositories/, Migrations/ (geradas)
│   │   ├── Security/            Pbkdf2PasswordHasher
│   │   ├── Seed/                DatabaseSeeder, SeedOptions
│   │   ├── Time/                SystemClock
│   │   └── DependencyInjection.cs
│   ├── GestaoSorveteria.Mobile/     app MAUI (MVP): View/, ViewModel/, Model/, Data/, Services/
│   └── GestaoSorveteria.Server/
│       ├── Controllers/         AuthController (Sprint 1: Sync, Produtos)
│       ├── Security/            JwtOptions, JwtTokenService
│       ├── Middleware/          DomainExceptionHandler
│       ├── Program.cs
│       └── appsettings*.json
└── tests/
    └── GestaoSorveteria.Tests/
        ├── Domain/
        └── Application/
```

## 3. Fluxo de uma requisição (padrão do projeto)

```
POST /api/sync/comandas   (lote enviado pelo app)
  │
  ├─ SyncController.Comandas(SyncComandasRequest)                ← valida formato, extrai usuário do token
  │     └─ SyncService.ReceberComandasAsync(request, usuarioId)  ← caso de uso (Application)
  │           ├─ IComandaRepository.ObterPorIdAsync(id)          ← já recebida? → "já recebida" (idempotência)
  │           ├─ ICaixaRepository / IProdutoRepository           ← Infrastructure/EF
  │           ├─ Comanda.Abrir(...), AdicionarProduto(...), Fechar(...)  ← REGRAS no Domain (DomainException)
  │           ├─ IUnitOfWork.SaveChangesAsync()
  │           └─ resultado por comanda: aceita / já recebida / rejeitada + motivo (Contracts)
  └─ erro inesperado → ProblemDetails (middleware)
```

Nomenclatura (do roadmap, mantida): classes `PascalCase`; interfaces `I*`; métodos assíncronos com sufixo `Async`; DTOs `NomeDto` / `NomeRequest`; rotas `kebab-case` no plural (`/api/comandas`).

## 4. Modelo de dados — Fase 1

Convenções: tabelas e colunas em `snake_case`; chaves `uuid`; dinheiro `numeric(12,2)`; datas `timestamptz` (UTC); enums como `varchar`.

### `usuarios`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | |
| nome | varchar(100) | |
| login | varchar(50) | **único** (minúsculo) |
| senha_hash | varchar(300) | PBKDF2 |
| perfil | varchar(20) | `Atendente` / `Admin` |
| ativo | boolean | |
| criado_em | timestamptz | |

### `produtos`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | |
| nome | varchar(80) | **único** (case-insensitive: índice em `lower(nome)`) |
| categoria | varchar(50) | |
| preco | numeric(12,2) | ≥ 0 |
| permite_valor_livre | boolean | self-service |
| ativo | boolean | |
| ordem | int | posição no app |
| criado_em / atualizado_em | timestamptz | |

### `caixas`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | |
| aberto_por_usuario_id | uuid FK usuarios | |
| aberto_em | timestamptz | |
| fundo_troco | numeric(12,2) | |
| status | varchar(20) | `Aberto` / `Fechado` — **índice único parcial: só um `Aberto`** |
| fechado_por_usuario_id | uuid FK usuarios, null | |
| fechado_em | timestamptz null | |
| total_vendas_dinheiro | numeric(12,2) null | calculado no fechamento |
| valor_esperado | numeric(12,2) null | RN-CX-06 |
| valor_contado | numeric(12,2) null | |
| diferenca | numeric(12,2) null | contado − esperado |
| total_vendas_dinheiro_app | numeric(12,2) null | RN-CX-10: valor do celular, só quando diverge |
| valor_esperado_app | numeric(12,2) null | RN-CX-10: valor do celular, só quando diverge |
| divergencia_sincronizacao | boolean | RN-CX-10 |
| fechamento_forcado | boolean | RN-CX-09: fechado pela dona no painel (contado 0); motivo em `observacao` |
| xmin (sistema) | xid | token de concorrência: dois fechamentos simultâneos → 409, nunca sobrescrita (RN-CX-07) |
| observacao | varchar(500) null | |

### `movimentos_caixa`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | |
| caixa_id | uuid FK caixas | |
| tipo | varchar(20) | `Sangria` / `Suprimento` |
| valor | numeric(12,2) | > 0 |
| motivo | varchar(200) | |
| usuario_id | uuid FK usuarios | |
| em | timestamptz | |

### `comandas`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | gerado no cliente (offline) |
| numero | int | sequencial no caixa; índice único (caixa_id, numero) |
| caixa_id | uuid FK caixas | |
| usuario_id | uuid FK usuarios | quem abriu |
| tipo | varchar(20) | `Balcao` / `Delivery` |
| status | varchar(20) | `Aberta` / `Fechada` / `Cancelada` / `Estornada` (RN-CM-09) |
| observacao | varchar(300) null | |
| total | numeric(12,2) | Σ itens (recalculado a cada alteração) |
| criada_em | timestamptz | hora do dispositivo |
| recebida_em | timestamptz | hora do servidor |
| fechada_em | timestamptz null | |
| cancelada_em | timestamptz null | |
| motivo_cancelamento | varchar(300) null | |
| estornada_em | timestamptz null | RN-CM-09 |
| estornada_por_usuario_id | uuid FK usuarios, null | Admin que estornou |
| motivo_estorno | varchar(300) null | obrigatório no estorno |
| recebida_apos_fechamento_caixa | boolean | RN-CX-08 |

### `itens_comanda`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | |
| comanda_id | uuid FK comandas | |
| produto_id | uuid FK produtos, **null** | null = item livre |
| descricao | varchar(120) | snapshot do nome ou texto livre |
| quantidade | int | ≥ 1 |
| preco_unitario | numeric(12,2) | snapshot |
| subtotal | numeric(12,2) | quantidade × preço |

### `pagamentos`
| coluna | tipo | obs |
|---|---|---|
| id | uuid PK | |
| comanda_id | uuid FK comandas | |
| forma | varchar(20) | `Dinheiro` / `Pix` / `CartaoDebito` / `CartaoCredito` |
| valor | numeric(12,2) | abate da comanda |
| valor_recebido | numeric(12,2) | dinheiro entregue |
| troco | numeric(12,2) | |
| pix_txid | varchar(35)? | RN-PX-05: txid da cobrança do Pix integrado que pagou; nulo no Pix conferido à mão. Índice |

Índices além dos citados: `comandas(caixa_id)`, `comandas(status)`, `comandas(fechada_em)`, `itens_comanda(comanda_id)`, `pagamentos(comanda_id)`, `movimentos_caixa(caixa_id)`.

## 5. Extensão — Fase 2 (estoque, avisos e pedidos ao fornecedor)

Desenho em docs/10 (revisto em 14/10); regras RN-ES / RN-AV / RN-PF / RN-RC em docs/02, seção 6. Mesmas convenções da Fase 1 (uuid gerado no cliente/domínio, `numeric(12,2)` para dinheiro, `timestamptz`, enums como texto, nada apagado).

```
configuracao_loja   (id fixo, nome_loja varchar(60), whatsapp_dona varchar(13)?, atualizado_em)  -- 1 linha

fornecedores        (id, nome, nome_normalizado, whatsapp varchar(13)?, dias_pedido text[] -- {"Monday","Thursday"},
                     prazo_entrega_dias int, aceita_meia_caixa bool,
                     envio varchar(20) -- Impresso | WhatsApp | Ambos, mostrar_precos bool, ativo)
                                                     -- único (nome_normalizado)

grupos_estoque      (id, nome, nome_normalizado, modo_controle varchar(20) -- Contagem | SaldoPelaVenda | SoAviso, ordem)
                                                     -- único (nome_normalizado)

itens_estoque       (id, grupo_id FK, fornecedor_id FK, nome varchar(80),
                     unidade_compra varchar(10) -- Caixa | Balde | Unidade | Pacote,
                     unidades_por_embalagem int ≥ 1, custo_embalagem numeric(12,2),
                     estoque_alvo_embalagens numeric(6,1)?, minimo_unidades int?, ordem, ativo)
                                                     -- único (grupo_id, nome_normalizado)

movimentos_estoque  (id, item_estoque_id FK, tipo varchar(15) -- Entrada | BaixaVenda | EstornoVenda,
                     unidades int > 0, em, registrado_em, pedido_id FK?, comanda_id FK?, item_comanda_id FK?)
                     -- só grupos SaldoPelaVenda (potes); nunca apagado. em = quando aconteceu na loja (entrega chegou,
                     -- venda fechada; o estorno repete o em da venda); registrado_em = quando o servidor gravou.
                     -- único (tipo, item_comanda_id) e (tipo, pedido_id, item_estoque_id): a sincronização repetida não baixa duas vezes
                     -- saldo (RN-ES-08) = contado na última contagem + Σ movimentos com em depois da hora em que o item foi contado

contagens           (id, usuario_id, iniciada_em, concluida_em?, origem varchar(10) -- App | Painel)
itens_contagem      (id, contagem_id FK, item_estoque_id FK, contado_unidades int?,  -- null = não contado
                     saldo_sistema_unidades int?,  -- reservado: o saldo esperado é calculado na leitura (RN-ES-08)
                     contado_em timestamptz?)      -- quando o item foi contado (base do saldo); null = vale concluida_em
                                                   -- único (contagem_id, item_estoque_id)

avisos              (id, item_estoque_id?, texto_livre varchar(120)?, quantidade_atual int,
                     prioridade varchar(10) -- Urgente | Alta | Media, observacao?,
                     criado_por_usuario_id? -- null = Sistema, criado_em,
                     atualizado_em, status varchar(10) -- Aberto | Fechado,
                     fechado_em?, fechado_por?, motivo_fechamento?, pedido_id?,
                     whatsapp_aberto_em?)          -- urgente: quando o link foi aberto (RN-AV-08)
                     -- índice único parcial: 1 Aberto por item_estoque_id

pedidos_fornecedor  (id, fornecedor_id FK, numero int, status varchar(15), criado_em, enviado_em?,
                     recebido_em?  -- conferência no sistema, chegou_em?  -- quando a entrega chegou (conta para o estoque),
                     cancelado_em?, motivo_cancelamento?, total_pedido numeric(12,2),
                     total_recebido numeric(12,2)?, percentual_atendido numeric(5,2)?, usuario_id)
                     -- índice único parcial: 1 Rascunho por fornecedor

itens_pedido_fornecedor (id, pedido_id FK, item_estoque_id FK, sugerido_embalagens numeric(6,1),
                     pedido_embalagens numeric(6,1), recebido_embalagens numeric(6,1)?,
                     custo_embalagem numeric(12,2), alterado_pela_dona bool, origem varchar(20)
                     -- Sugestao | Aviso | FaltouAnterior, pedir_de_novo bool, nao_pedido bool)

produtos            + grupo_estoque_id FK uuid null, + unidades_por_venda int default 1   -- RN-PR-06 (Sprint 7)
produtos            + custo_informado numeric(12,2) null                                  -- RN-PR-07 (Sprint 9)
itens_comanda       + item_estoque_id FK uuid null        -- sabor do pote (RN-ES-09); só em item de produto
```

**Sincronização (local-first):**
- Comanda: `ItemComandaSyncDto` ganha `ItemEstoqueId` **opcional** (só potes). App antigo continua aceito; a baixa sai no servidor ao gravar a comanda fechada, na mesma transação.
- **`POST /api/sync/avisos`**: avisos criados ou atualizados no tablet, cada um com o **estado atual** (não o histórico de toques), idempotente pelo `Id`. Resultado por aviso: `aceita` / `ja_recebida` / `rejeitada` + `avisoId` (o que vale no servidor) + `aberto`.
  - Item que já tem aviso aberto no servidor (do painel ou de outro aparelho): o do tablet é **juntado** a ele e o app passa a usar o `avisoId` devolvido.
  - Prioridade fica com a **maior**; quantidade e observação só mudam se a informação for **mais nova** que a do servidor (reenvio fora de ordem não volta a quantidade).
  - Aviso que a dona (ou a entrega) **fechou**: reenvio sem novidade → `ja_recebida` com `aberto = false` (o app tira da lista); mudança feita no tablet **depois** do fechamento → `rejeitada` com "A dona fechou este aviso às HH:mm. Se ainda está faltando, avise de novo".
  - Aviso juntado a outro que depois foi fechado: se tudo o que o tablet sabe é anterior ao fechamento, não reabre (`ja_recebida`, `aberto = false`).
  - Datas além de agora + 10 min são recusadas (relógio do tablet adiantado travaria as atualizações seguintes).
- **`POST /api/sync/contagens`**: contagem do tablet com **todas as linhas registradas** (embalagens; nulo = não contado), parcial ou concluída (`concluidaEm`), idempotente pelo `Id`. Aberta no servidor recebe as linhas; reenvio igual → `ja_recebida`; mudança numa contagem já concluída → `rejeitada` ("faça uma nova contagem").
- O app sabe quando lembrar da contagem por `diasDeContagem` em `GET /api/estoque/itens` (véspera do dia de pedido de cada fornecedor com item na contagem).
- O app baixa grupos e itens **sem custo** em `GET /api/estoque/itens` e os avisos abertos em `GET /api/avisos?abertos=true`; o WhatsApp da dona vem de `GET /api/configuracoes`.
- O WhatsApp sai do aparelho (link `wa.me`), não do servidor. O texto do urgente é montado no próprio app (`MensagensWhatsApp.Urgente`, no Domain), para sair mesmo sem internet. `GET /api/avisos/resumo-do-dia` devolve o texto do resumo e o link; o app pede **depois de enviar a fila** (o total vendido conta só as vendas que já chegaram). Atendente só pede o de hoje; outros dias, só a dona.

**Endpoints (Admin, painel):**

| Método | Rota | Uso |
|---|---|---|
| GET/PUT | `/api/configuracoes` | WhatsApp da dona, nome da loja (GET para todos; PUT só Admin) |
| GET | `/api/configuracoes/primeiros-passos` | Admin: catálogo, cardápio, atendentes, WhatsApp da dona, primeira contagem e primeira venda do tablet — feito ou não (RN-LJ-01; lista do Início do painel) |
| GET/POST | `/api/avisos`, `/api/avisos/{id}/ainda-falta`, `/{id}/whatsapp-aberto` | listar e criar avisos, "ainda falta" (atendente e dona) |
| POST | `/api/avisos/{id}/fechar` | fechar com motivo (só Admin) |
| GET | `/api/avisos/resumo-do-dia?data=` | texto e link do resumo (RN-AV-07) |
| GET | `/api/estoque/catalogo` | fornecedores, grupos e itens (com custo) para o painel |
| POST/PUT | `/api/fornecedores`, `/api/estoque/grupos`, `/api/estoque/itens` (+ `/{id}/ativar`, `/{id}/desativar`) | catálogo (RN-ES-01/02/11, RN-PF-01/09) |
| POST | `/api/estoque/itens/importar` | CSV da planilha; `simular: true` devolve a prévia sem gravar (RN-ES-10) |
| GET | `/api/estoque/saldos` | saldo de cada pote (grupos `SaldoPelaVenda`): última contagem + entregas − vendas depois dela, mínimo, vendidos na semana (RN-ES-08) |
| GET/POST | `/api/estoque/contagens`, `/api/estoque/contagens/nova`, `/{id}` | contagem pelo painel (rascunho e concluir), histórico; a contagem anterior vem como referência (só Admin) |
| POST | `/api/pedidos-fornecedor/montar?fornecedorId=` | monta/atualiza o rascunho |
| PUT | `/api/pedidos-fornecedor/{id}/itens` | +/− nas linhas |
| POST | `/api/pedidos-fornecedor/{id}/enviar` | confirma; devolve texto do WhatsApp |
| GET | `/api/pedidos-fornecedor?fornecedorId=`, `/api/pedidos-fornecedor/{id}` | lista (90 dias + não recebidos) e o pedido completo — também os dados da folha impressa (o painel renderiza e chama a impressão do navegador) |
| POST | `/api/pedidos-fornecedor/{id}/receber` | conferência; diferenças e texto para o fornecedor |
| POST | `/api/pedidos-fornecedor/{id}/cancelar` | com motivo |
| GET | `/api/pedidos-fornecedor/{id}/mensagens` | WhatsApp para reabrir: o pedido (fornecedor que recebe por WhatsApp) e o que veio diferente na entrega |
| GET | `/api/fornecedores/{id}/historico` | % atendido, itens que mais faltam |

**Montagem automática do rascunho:** o painel chama `montar` ao abrir no dia do pedido (idempotente); um `BackgroundService` faz o mesmo quando a API estiver acordada. Não depende de agendador pago.

## 5b. Pix integrado (docs/02, seção 5a)

```
cobrancas_pix (id uuid PK — gerado pelo app por tentativa, txid varchar(35) único = id sem hífens, usuario_id uuid,
               valor numeric(12,2), status varchar(20) Ativa|Concluida|Removida|Expirada, pix_copia_ecola varchar(512),
               criada_em, expira_em, atualizada_em timestamptz, paga_em timestamptz?, end_to_end_id varchar(35)?,
               valor_pago numeric(12,2)?)                                                          -- RN-PX-02/03
pagamentos + pix_txid varchar(35)?                                                                    -- RN-PX-05
```

Endpoints (atendente ou dona; banco fora do ar → **503** com a mensagem pronta):

| Método | Rota | O quê |
|---|---|---|
| GET | `/api/pix/situacao` | ligado?, provedor (`Inter`/`Simulado`/`Desligado`), validade do QR Code (RN-PX-01) |
| POST | `/api/pix/cobrancas` | `{ id, valor }` → cria no banco (ou devolve a mesma, se o Id já existe) com o copia e cola (RN-PX-02) |
| GET | `/api/pix/cobrancas/{id}` | situação atual; enquanto ativa pergunta ao banco; `paga` = concluída com o valor exato (RN-PX-03/04) |
| POST | `/api/pix/cobrancas/{id}/cancelar` | remove no banco; se já foi paga, volta paga (RN-PX-06) |

Banco: `IProvedorPix` (Application) → `InterProvedorPix` (Infrastructure: OAuth2 client_credentials com certificado mTLS, `PUT/GET/PATCH /pix/v2/cob/{txid}` do padrão do Banco Central, token em memória), `SimuladoProvedorPix` (fora de produção) ou `DesligadoProvedorPix`. Confirmação por **consulta** (o app pergunta a cada poucos segundos enquanto mostra o QR Code); webhook fica para depois, porque a API grátis dorme.

## 5a. Clima e calendário (base para estimar vendas)

Regras RN-CL em docs/02, seção 9b. O clima é guardado pelo **ponto** (latitude/longitude com 2 casas), não pela loja: uma segunda loja (ou a mudança de endereço) ganha o próprio histórico sem migração.

```
configuracao_loja + local_nome varchar(120)?, latitude numeric(7,4)?, longitude numeric(7,4)?   -- RN-CL-01

clima_dias          (id, data date, latitude numeric(5,2), longitude numeric(5,2),
                     temp_max numeric(4,1), temp_min numeric(4,1), temp_media numeric(4,1), sensacao_max numeric(4,1)?,
                     chuva_mm numeric(6,1)?, chuva_horas_pct numeric(4,1)?, nuvens_pct numeric(4,1)?, umidade_pct numeric(4,1)?,
                     icone varchar(40)?, condicao varchar(120)?, origem varchar(12) -- Observado | Combinado | Previsto,
                     definitivo bool, fonte varchar(20), coletado_em timestamptz)
                     -- único (latitude, longitude, data); definitivo nunca muda (RN-CL-02)

previsoes_clima     (id, feita_no_dia date, feita_em timestamptz, data date, latitude numeric(5,2), longitude numeric(5,2),
                     temp_max numeric(4,1), temp_min numeric(4,1), chuva_mm numeric(6,1)?, chance_chuva_pct numeric(4,1)?,
                     icone varchar(40)?, condicao varchar(120)?, fonte varchar(20))
                     -- único (latitude, longitude, feita_no_dia, data); nunca sobrescrita (RN-CL-03)
```

- **Feriados nacionais** (RN-CL-05) são calculados no Domain (`Feriados`), sem tabela.
- **Fonte do clima** atrás de `IFonteClima` (Application); a implementação `VisualCrossingFonteClima` (Infrastructure) usa a Timeline API — `GET timeline/{lat},{lon}/{de}/{ate}?unitGroup=metric&include=days&lang=pt&elements=…` — que devolve, na mesma resposta, dias passados (observados) e futuros (previsão), cada um com `source` (`obs`, `comb`, `fcst`). Procurar o local usa o mesmo endpoint com o texto no lugar das coordenadas (`resolvedAddress`, `latitude`, `longitude`).
- **Coleta** (`ColetaClimaService`, RN-CL-04): chamada por um `BackgroundService` do Server (1 min depois de ligar e a cada 3 h) e pelo botão "Atualizar agora". Uma chamada à fonte por janela de até 31 dias; a janela mais recente vai até hoje + 14 quando a previsão do dia ainda não foi guardada (uma chamada só traz os dois). Grava a cada janela (o que já veio fica, mesmo se a próxima falhar).

| Método | Rota | Uso |
|---|---|---|
| GET | `/api/clima/situacao` | Admin: chave configurada, local, última coleta, último erro, primeiro e último dia com clima, atribuição |
| GET | `/api/clima/procurar-local?texto=` | Admin: o lugar encontrado pelo serviço de clima (nome, latitude, longitude) para a dona confirmar |
| PUT | `/api/clima/local` | Admin: `{ nome, latitude, longitude }` grava o local da loja e já coleta; devolve a situação |
| POST | `/api/clima/atualizar` | Admin: coleta agora (RN-CL-04, sem esperar as 3 h) |
| GET | `/api/relatorios/clima-vendas?de=&ate=` | Admin: Clima × vendas por dia, médias por faixa de temperatura e chuva, próximos 7 dias (RN-CL-06) |

## 6. API — plano de endpoints (Sprint 1)

Modelo local-first: o app **não** chama a API a cada toque. Ele envia documentos prontos.

| Método | Rota | Perfil | Observação |
|---|---|---|---|
| POST | `/api/auth/login` | público | rate limit; devolve JWT + usuário (pronto no Sprint 0) |
| GET | `/api/auth/me` | qualquer | valida token (pronto no Sprint 0) |
| GET | `/api/produtos?desde=` | qualquer | o app baixa o catálogo. Sem `desde`: tudo. Com `desde` (data/hora com fuso): só os criados/alterados a partir dali, **inclusive desativados** (o app esconde o botão, RN-PR-03). A resposta traz `geradoEmUtc`, que o app guarda e manda como próximo `desde`. A API recua `desde` em 5 min (margem contra gravação concorrente); repetir produto é inofensivo |
| GET | `/api/produtos/{id}` | qualquer | um produto (tela de edição do painel) |
| POST/PUT | `/api/produtos`, `/api/produtos/{id}` | Admin | cadastro pelo painel (`SalvarProdutoRequest`); nome repetido → 400 (RN-PR-01); POST devolve 201 |
| POST | `/api/produtos/{id}/ativar` · `/desativar` | Admin | nunca apaga (RN-PR-03); devolve o produto atualizado |
| GET | `/api/produtos/custos` | Admin | custo estimado de uma unidade vendida (pelo estoque ou informado), a origem e a margem sobre o preço de cada produto (RN-PR-07, RN-RL-04) — só o painel; o catálogo do app não traz custo |
| PUT | `/api/produtos/{id}/custo` | Admin | `{ custo }` informa o custo de uma unidade vendida (nulo apaga) — para produto sem vínculo de estoque ou grupo sem custo |
| PUT | `/api/produtos/{id}/estoque` | Admin | liga o produto a um grupo de estoque (`grupoEstoqueId` nulo = desliga) e quantas unidades saem por venda (RN-PR-06). Grupo de potes → `pedeSabor` no produto: o app mostra a grade de sabores e manda `itemEstoqueId` na linha da venda (RN-ES-09) |
| GET | `/api/usuarios`, `/api/usuarios/{id}` | Admin | todos os usuários, inclusive desativados (`UsuarioDto`) |
| POST | `/api/usuarios` | Admin | cria atendente (PIN) ou Admin (senha) — `CriarUsuarioRequest` (`perfil` = `Atendente`/`Admin`); login repetido → 400 (RN-US-02); senha fora da política → 400 (RN-US-03); 201 |
| POST | `/api/usuarios/{id}/redefinir-senha` | Admin | `RedefinirSenhaRequest { novaSenha }`; sem pedir a antiga (RN-US-08); vale a política do perfil do usuário |
| POST | `/api/usuarios/{id}/ativar` · `/desativar` | Admin | nunca apaga (RN-US-07); desativar a si mesmo → 400 (RN-US-10) |
| POST | `/api/sync/caixas` | qualquer | lote de caixas (abertos/fechados) com movimentos; idempotente pelo `id` |
| POST | `/api/sync/comandas` | qualquer | lote de comandas **fechadas ou canceladas** com itens e pagamentos; idempotente pelo `id`; a API reconstrói o agregado com as regras do Domain e devolve, por comanda, `aceita`/`já recebida`/`rejeitada` + motivo |
| POST | `/api/comandas/{id}/estornar` | Admin | estorno pelo painel (RN-CM-09) |
| GET | `/api/relatorios/dia?data=` | Admin | resumo do dia para o painel |
| GET | `/api/relatorios/periodo?de=&ate=&agrupar=` | Admin | relatório de período (Sprint 8) |
| GET | `/api/caixas?de=&ate=` | Admin | histórico de fechamentos |
| POST | `/api/caixas/{id}/fechar-forcado` | Admin | RN-CX-09: `{ "motivo": "..." }`; contado = 0, vendas em dinheiro = as já recebidas, diferença gravada; caixa já fechado → 400; corrida com a sincronização → 409 (xmin) |
| GET | `/api/versao` | público | `versaoApi`, `versaoMinimaApp`, `versaoAtualApp`, `linkApk` (configuração `App:*`); abaixo da mínima o app exige atualizar, abaixo da atual só avisa |

Erros seguem **RFC 9457 ProblemDetails**: 400 regra de negócio (`DomainException`), 401/403 acesso, 404 não encontrado, 409 conflito (violação de unicidade), 429 rate limit. No sync em lote, rejeição de uma comanda **não** derruba o lote: vem no resultado daquela comanda.

### 6.1 Endpoints do painel — proposta (Sprint 3, a validar pelo Paulo)

O painel **não calcula dinheiro** (ADR 018): totais, ticket médio, esperado × contado e somas por forma de pagamento vêm prontos da API, calculados com o Domain. "Hoje/de/até" são **datas do dia comercial** (`yyyy-MM-dd`, America/Sao_Paulo — `DiaComercial`); a API converte para o intervalo UTC. Todos `[Authorize(Roles = "Admin")]`.

| Método | Rota | Tela | Resposta |
|---|---|---|---|
| GET | `/api/relatorios/dia?data=` | Dashboard — **pronto** (sem data = hoje; vendas estornadas à parte; vendas por hora, mesmo dia da semana anterior e mais vendidos desde 14/10 — RN-RL-03/06/07) | `ResumoDiaDto` |
| GET | `/api/relatorios/periodo?de=&ate=&agrupar=` | Relatório de período — **pronto** (sem datas = este mês até hoje; até 366 dias; `agrupar` = dia, semana ou mes; faturamento por trecho, período anterior, por hora, por dia da semana, rankings de produtos, categorias e sabores e a margem estimada por produto, sabor e período — RN-RL-03/04/08/09/10) | `RelatorioPeriodoDto` |
| GET | `/api/comandas?de=&ate=&status=&atendente=&forma=&pagina=&tamanho=` | Vendas (lista) — **pronto** (dia da venda = fechamento, senão cancelamento, senão criação; sem datas = hoje; `atendente` = Id de quem vendeu; `forma` = Dinheiro, Pix, CartaoDebito ou CartaoCredito — venda com pelo menos um pagamento nela, então o pagamento dividido aparece nas duas; tamanho 1–100, padrão 50) | `PaginaDto<ComandaResumoDto>` |
| GET | `/api/comandas/{id}` | Vendas (detalhe) — **pronto** | `ComandaDetalheDto` |
| POST | `/api/comandas/{id}/estornar` | Vendas (estorno — RN-CM-09) — **pronto** (corpo `{ "motivo": "..." }`; não altera o caixa) | `ComandaDetalheDto` |
| GET | `/api/caixas?de=&ate=` | Caixas (histórico) — **pronto** (sem datas: 30 dias; máx. 93; hoje faz 1 consulta por caixa — agregar no repositório se pesar) | `IReadOnlyList<CaixaResumoDto>` |
| GET | `/api/caixas/atual` | Dashboard (caixa aberto) — **pronto** | `CaixaResumoDto` ou 204 sem caixa aberto |
| GET | `/api/caixas/{id}` | Caixas (detalhe) — **pronto** | `CaixaDetalheDto` |
| GET/POST | `/api/usuarios`, `/api/usuarios/{id}/redefinir-senha`, `/ativar`, `/desativar` | Usuários — **pronto** (RN-US-01…08, RN-US-10) | `UsuarioDto` / `IReadOnlyList<UsuarioDto>` |
| POST | `/api/caixas/{id}/fechar-forcado` | Caixas (detalhe do caixa aberto — RN-CX-09) — **pronto** (corpo `FecharCaixaForcadoRequest { motivo }`) | `CaixaDetalheDto` (resumo com `fechamentoForcado = true`) |

DTOs (namespace `Contracts.Relatorios`, `Contracts.Comandas`, `Contracts.Caixas`; enums como texto, dinheiro `decimal` 2 casas, datas UTC):

```csharp
// Dashboard — RN-RL-01: faturamento = comandas fechadas; estornos à parte.
record ResumoDiaDto(
    DateOnly Data,                              // dia comercial consultado
    decimal TotalVendido, int QuantidadeComandas, decimal TicketMedio,   // ticket médio calculado na API (0 sem vendas)
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento,
    decimal TotalEstornado, int QuantidadeEstornos,                      // vendas do dia estornadas depois (RN-RL-01)
    int QuantidadeCanceladas,
    int VendasRecebidasAposFechamento,          // RN-CX-08: para conferência
    CaixaResumoDto? CaixaAtual);                // caixa aberto agora (ou o último do dia)
record TotalPorFormaDto(string Forma, decimal Total, int Quantidade);

// Vendas
record PaginaDto<T>(IReadOnlyList<T> Itens, int Pagina, int Tamanho, int TotalItens);
record ComandaResumoDto(Guid Id, int Numero, Guid CaixaId, string Tipo, string Status, decimal Total,
    DateTime CriadaEm, DateTime? FechadaEm, string AtendenteNome, IReadOnlyList<string> FormasPagamento,
    bool RecebidaAposFechamentoCaixa);
record ComandaDetalheDto(Guid Id, int Numero, Guid CaixaId, string Tipo, string Status, decimal Total,
    string? Observacao, DateTime CriadaEm, DateTime RecebidaEm, DateTime? FechadaEm,
    DateTime? CanceladaEm, string? MotivoCancelamento, DateTime? EstornadaEm, string? EstornadaPorNome, string? MotivoEstorno,
    bool RecebidaAposFechamentoCaixa, string AtendenteNome,
    IReadOnlyList<ItemComandaDto> Itens, IReadOnlyList<PagamentoDto> Pagamentos);
record ItemComandaDto(Guid Id, Guid? ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
record PagamentoDto(Guid Id, string Forma, decimal Valor, decimal ValorRecebido, decimal Troco);
record EstornarComandaRequest([Required, StringLength(300, MinimumLength = 3)] string Motivo);

// Caixas — RN-CX-06: fechado → valores gravados no fechamento; aberto → vendas em dinheiro e esperado "até agora", contado/diferença null.
record CaixaResumoDto(Guid Id, string Status, DateTime AbertoEm, string AbertoPorNome, decimal FundoTroco,
    DateTime? FechadoEm, string? FechadoPorNome,
    decimal TotalVendas, int QuantidadeComandas,            // todas as formas, calculado na API
    decimal? TotalVendasDinheiro, decimal? ValorEsperado, decimal? ValorContado, decimal? Diferenca,
    bool DivergenciaSincronizacao, int VendasRecebidasAposFechamento, decimal DinheiroRecebidoAposFechamento,   // RN-CX-10 e RN-CX-08: pontos para a dona conferir
    bool FechamentoForcado);                    // RN-CX-09: fechado pela dona no painel, contado 0
record FecharCaixaForcadoRequest([Required, StringLength(300, MinimumLength = 3)] string Motivo);
record CaixaDetalheDto(CaixaResumoDto Resumo, IReadOnlyList<MovimentoCaixaDto> Movimentos,
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento, string? Observacao);
record MovimentoCaixaDto(Guid Id, string Tipo, decimal Valor, string Motivo, string UsuarioNome, DateTime Em);
```

Decidido em 30/09/2026 (Paulo):
1. **Estorno × cancelamento:** status próprio `Estornada` + colunas `estornada_em`, `estornada_por_usuario_id`, `motivo_estorno` (migração). `Cancelada` fica só para a comanda aberta cancelada (RN-CM-08).
2. **Estorno depois do caixa fechado:** só no relatório — o caixa não muda (RN-CX-07). Dinheiro devolvido ao cliente = sangria no caixa aberto, pelo app.
3. **Fechamento forçado pelo painel** (RN-CX-09): Sprint 4 — **pronto** em 07/10/2026 (`POST /api/caixas/{id}/fechar-forcado`).
4. `CaixaAtual` no dashboard mostra o último estado recebido do app (depende de `POST /api/sync/caixas`).
5. Os endpoints de leitura são implementados na frente do painel, com uma consulta própria (`Application/Painel`, implementada com projeções do EF em `Infrastructure`), sem mexer nos repositórios usados pela sincronização.

## 7. Autenticação

- **App e painel** usam o mesmo `POST /api/auth/login` → JWT (claims `sub`, `name`, `role`), validade 12 h, assinado com `Jwt:Key` (mín. 32 caracteres, variável de ambiente em produção). Enviado em `Authorization: Bearer`.
- **App:** o primeiro login com PIN exige internet; o token fica no `SecureStorage`. Se expirar sem internet, o app continua vendendo e pede o PIN de novo quando a conexão voltar (as vendas pendentes esperam na fila).
- **Painel:** token guardado no navegador (`localStorage`) até o `ExpiraEmUtc` do login; vencido ou recusado pela API (401) → volta para o login. Perfil Admin obrigatório (conferido no login e no `GET /api/auth/me` ao abrir). Enquanto a API acorda, o painel mostra "Conectando ao servidor…" e tenta de novo (até 5 tentativas de 70 s).
- **CORS:** a API libera apenas a origem do painel (`Cors:PainelOrigem`).
- Autorização por perfil: `[Authorize(Roles = "Admin")]` nos endpoints de gestão.

## 8. Protocolo local-first (app ↔ API)

1. **Ao abrir o app (com internet):** baixa produtos alterados; tenta enviar pendências.
2. **Caixa:** aberto no celular (fundo de troco), com sangrias/suprimentos locais. Fechado no celular com a conferência do Domain (`Caixa.Fechar`).
3. **Comanda:** criada, alterada e fechada só no SQLite. `Id` (UUID) e `Numero` (sequencial no caixa) gerados no celular.
4. **Ao fechar ou cancelar** uma comanda, ela entra na fila de envio (`pendente_envio = true`). O `SyncService` tenta enviar na hora e depois a cada 1 min.
5. **Resposta da API por comanda:** `aceita` ou `já recebida` → marca como enviada; `rejeitada` → fica visível para o atendente com o motivo (nunca some em silêncio); erro de rede ou API dormindo → tenta de novo (timeout de 90 s — docs/07).
6. **Fechar o caixa** exige só que não haja comanda aberta (RN-CX-05); pode ser sem internet (RN-SY-04). O caixa fechado sobe depois das comandas dele; o servidor grava os próprios valores e marca divergência com os do celular (RN-CX-10). Venda que chega depois do fechamento é aceita e marcada, sem mudar os valores do caixa (RN-CX-07/08).
7. **Servidor:** mesmo `Id` recebido de novo → responde `já recebida` sem duplicar (idempotência).
8. **Formato (`Contracts/Sync`):** lote de 1 a 100 itens; resposta `{ resultados: [ { id, status: "aceita" | "ja_recebida" | "rejeitada", motivo? } ] }` na ordem do lote. Só o lote é validado por atributo (400 se vazio ou grande demais); os itens não, para que um item inválido vire `rejeitada` sem derrubar os outros. Enums como texto com os nomes do Domain; datas UTC. O app manda também os valores que calculou (total, subtotal, troco, esperado, diferença) para a API conferir com as mesmas regras do Domain. Ordem: caixa aberto → comandas → caixa com fechamento (RN-SY-03). Reenvio de caixa só acrescenta: movimento/fechamento novo = `aceita`, nada novo = `ja_recebida`, nada é apagado, e caixa já fechado no servidor não muda (`rejeitada`, RN-CX-07).

## 9. Configuração e ambientes

| Chave | Dev (appsettings.Development.json) | Produção (variáveis de ambiente) |
|---|---|---|
| `ConnectionStrings:Default` | `Host=localhost;Port=5433;Database=sorveteria;Username=postgres;Password=postgres` | `ConnectionStrings__Default` |
| `Jwt:Key` | chave de desenvolvimento (no arquivo) | `Jwt__Key` (segredo, ≥ 32 chars) |
| `Jwt:Issuer` / `Jwt:Audience` | `GestaoSorveteria` | idem |
| `Jwt:ExpiracaoHoras` | 12 | 12 |
| `Seed:AdminLogin` / `Seed:AdminSenha` | `admin` / `admin12345` | `Seed__AdminLogin` / `Seed__AdminSenha` (trocar após o 1º login) |
| `Database:MigrateOnStartup` | true | `true` no piloto (um serviço só, Render Free); `false` quando houver deploy pago/réplicas |
| `Cors:PainelOrigem` | `http://localhost:5173` (painel local) | URL do painel no Cloudflare Pages (Sprint 3) |
| `Clima:ChaveApi` | vazio (coleta desligada; pôr a sua chave em `dotnet user-secrets` ou variável para testar) | `Clima__ChaveApi` (chave do Visual Crossing, RN-CL-07) |
| `Clima:IntervaloHoras` | 3 | 3 (de quanto em quanto tempo a API acordada coleta; fora de 1–24 volta a 3 com aviso no log). A recoleta de um dia ainda aberto espera 3 h, fixo |

Nunca commitar segredo: `appsettings.Development.json` só contém valores locais; produção usa variáveis de ambiente (Render → Environment). Ver docs/07.

## 10. Deploy — gratuito no desenvolvimento e no piloto

Detalhes, limites e variáveis em **docs/07-hospedagem.md**.

- **API:** Render (Web Service Free, a partir do `Dockerfile` da raiz). Dorme após 15 min sem tráfego; acorda em ~1 min. Health check `/health`.
- **Banco:** Neon Free (PostgreSQL, 0,5 GB). `SSL Mode=Require` na string de conexão.
- **Painel:** Cloudflare Pages (site estático). A Vercel Hobby não permite uso comercial.
- **App:** APK assinado por link fixo; `/api/versao` avisa sobre atualização.
- **Backup:** `pg_dump` semanal guardado fora da nuvem; o celular mantém as vendas até a API confirmar.
