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
| status | varchar(20) | `Aberta` / `Fechada` / `Cancelada` |
| observacao | varchar(300) null | |
| total | numeric(12,2) | Σ itens (recalculado a cada alteração) |
| criada_em | timestamptz | hora do dispositivo |
| recebida_em | timestamptz | hora do servidor |
| fechada_em | timestamptz null | |
| cancelada_em | timestamptz null | |
| motivo_cancelamento | varchar(300) null | |
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

Índices além dos citados: `comandas(caixa_id)`, `comandas(status)`, `comandas(fechada_em)`, `itens_comanda(comanda_id)`, `pagamentos(comanda_id)`, `movimentos_caixa(caixa_id)`.

## 5. Extensão — Fase 2 (estoque) e Fase 3 (custos)

```
insumos            (id, nome, unidade, quantidade_atual, quantidade_minima, custo_medio, ativo)
fichas_tecnicas    (id, produto_id, insumo_id, quantidade)            -- único (produto_id, insumo_id)
movimentos_estoque (id, insumo_id, tipo, quantidade, custo_total?, comanda_id?, usuario_id, em, observacao)
                    tipo: Entrada | BaixaVenda | EstornoVenda | Ajuste | Perda
produtos           + custo_manual numeric(12,2) null                   -- Fase 3, para produto sem ficha técnica
```

`quantidade_atual` é sempre o saldo dos movimentos (recalculável); a coluna existe só para leitura rápida e alertas.

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
| POST | `/api/sync/caixas` | qualquer | lote de caixas (abertos/fechados) com movimentos; idempotente pelo `id` |
| POST | `/api/sync/comandas` | qualquer | lote de comandas **fechadas ou canceladas** com itens e pagamentos; idempotente pelo `id`; a API reconstrói o agregado com as regras do Domain e devolve, por comanda, `aceita`/`já recebida`/`rejeitada` + motivo |
| POST | `/api/comandas/{id}/estornar` | Admin | estorno pelo painel (RN-CM-09) |
| GET | `/api/relatorios/dia?data=` | Admin | resumo do dia para o painel |
| GET | `/api/caixas?de=&ate=` | Admin | histórico de fechamentos |
| GET | `/api/versao` | público | versão mínima do app |

Erros seguem **RFC 9457 ProblemDetails**: 400 regra de negócio (`DomainException`), 401/403 acesso, 404 não encontrado, 409 conflito (violação de unicidade), 429 rate limit. No sync em lote, rejeição de uma comanda **não** derruba o lote: vem no resultado daquela comanda.

### 6.1 Endpoints do painel — proposta (Sprint 3, a validar pelo Paulo)

O painel **não calcula dinheiro** (ADR 018): totais, ticket médio, esperado × contado e somas por forma de pagamento vêm prontos da API, calculados com o Domain. "Hoje/de/até" são **datas do dia comercial** (`yyyy-MM-dd`, America/Sao_Paulo — `DiaComercial`); a API converte para o intervalo UTC. Todos `[Authorize(Roles = "Admin")]`.

| Método | Rota | Tela | Resposta |
|---|---|---|---|
| GET | `/api/relatorios/dia?data=` | Dashboard | `ResumoDiaDto` |
| GET | `/api/comandas?de=&ate=&status=&pagina=&tamanho=` | Vendas (lista) | `PaginaDto<ComandaResumoDto>` |
| GET | `/api/comandas/{id}` | Vendas (detalhe) | `ComandaDetalheDto` |
| POST | `/api/comandas/{id}/estornar` | Vendas (estorno — RN-CM-09, já previsto) | `ComandaDetalheDto` |
| GET | `/api/caixas?de=&ate=` | Caixas (histórico) | `IReadOnlyList<CaixaResumoDto>` |
| GET | `/api/caixas/atual` | Dashboard (caixa aberto) | `CaixaResumoDto` ou 204 sem caixa aberto |
| GET | `/api/caixas/{id}` | Caixas (detalhe) | `CaixaDetalheDto` |

DTOs (namespace `Contracts.Relatorios`, `Contracts.Comandas`, `Contracts.Caixas`; enums como texto, dinheiro `decimal` 2 casas, datas UTC):

```csharp
// Dashboard — RN-RL-01: faturamento = comandas fechadas; estornos à parte.
record ResumoDiaDto(
    DateOnly Data,                              // dia comercial consultado
    decimal TotalVendido, int QuantidadeComandas, decimal TicketMedio,   // ticket médio calculado na API (0 sem vendas)
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento,
    decimal TotalEstornado, int QuantidadeEstornos,
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
    DateTime? CanceladaEm, string? MotivoCancelamento, bool RecebidaAposFechamentoCaixa, string AtendenteNome,
    IReadOnlyList<ItemComandaDto> Itens, IReadOnlyList<PagamentoDto> Pagamentos);
record ItemComandaDto(Guid Id, Guid? ProdutoId, string Descricao, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
record PagamentoDto(Guid Id, string Forma, decimal Valor, decimal ValorRecebido, decimal Troco);
record EstornarComandaRequest([Required, StringLength(300, MinimumLength = 3)] string Motivo);

// Caixas — RN-CX-06: esperado e diferença gravados no fechamento; aberto → null.
record CaixaResumoDto(Guid Id, string Status, DateTime AbertoEm, string AbertoPorNome, decimal FundoTroco,
    DateTime? FechadoEm, string? FechadoPorNome,
    decimal TotalVendas, int QuantidadeComandas,            // todas as formas, calculado na API
    decimal? TotalVendasDinheiro, decimal? ValorEsperado, decimal? ValorContado, decimal? Diferenca);
record CaixaDetalheDto(CaixaResumoDto Resumo, IReadOnlyList<MovimentoCaixaDto> Movimentos,
    IReadOnlyList<TotalPorFormaDto> PorFormaPagamento, string? Observacao);
record MovimentoCaixaDto(Guid Id, string Tipo, decimal Valor, string Motivo, string UsuarioNome, DateTime Em);
```

Pontos a decidir antes de implementar:
1. **Estorno × cancelamento:** RN-CM-09 diz que a comanda estornada "vira `Cancelada`", mas o relatório precisa separá-la da comanda cancelada ainda aberta (RN-CM-08). Proposta: status próprio `Estornada` ou colunas `estornada_em` / `estornada_por_usuario_id` / `motivo_estorno` (migração).
2. **Estorno depois do caixa fechado** (RN-CX-07): o estorno entra no caixa original (só relatório) ou vira ajuste no caixa aberto? Afeta "esperado" se a venda foi em dinheiro.
3. **Fechamento forçado pelo painel** (RN-CX-09): `POST /api/caixas/{id}/forcar-fechamento` entra no Sprint 3 ou fica para o Sprint 4?
4. `CaixaAtual` no dashboard depende de o app já ter sincronizado o caixa aberto (`POST /api/sync/caixas` — docs/03 §8): sem sincronização recente, o painel mostra o último estado recebido e a hora dele.

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

Nunca commitar segredo: `appsettings.Development.json` só contém valores locais; produção usa variáveis de ambiente (Render → Environment). Ver docs/07.

## 10. Deploy — gratuito no desenvolvimento e no piloto

Detalhes, limites e variáveis em **docs/07-hospedagem.md**.

- **API:** Render (Web Service Free, a partir do `Dockerfile` da raiz). Dorme após 15 min sem tráfego; acorda em ~1 min. Health check `/health`.
- **Banco:** Neon Free (PostgreSQL, 0,5 GB). `SSL Mode=Require` na string de conexão.
- **Painel:** Cloudflare Pages (site estático). A Vercel Hobby não permite uso comercial.
- **App:** APK assinado por link fixo; `/api/versao` avisa sobre atualização.
- **Backup:** `pg_dump` semanal guardado fora da nuvem; o celular mantém as vendas até a API confirmar.
