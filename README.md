# Gestão Sorveteria (AppSorveteria)

Sistema de gestão para sorveteria, tudo num repositório só:

| Parte | Projeto | Estado |
|---|---|---|
| App do atendente (Android, offline) | `src/GestaoSorveteria.Mobile` — .NET MAUI + SQLite | MVP funcionando; refatoração no Sprint 2 (docs/06) |
| API | `src/GestaoSorveteria.Server` + `Domain`, `Contracts`, `Application`, `Infrastructure` — ASP.NET Core 10 + PostgreSQL | Fundação pronta (Sprint 0): login JWT, entidades, banco |
| Testes | `tests/GestaoSorveteria.Tests` — xUnit v3 | 100 casos de domínio/aplicação |
| Painel da dona | site estático (Sprint 3) | a criar |

## Documentação

| Documento | O que tem |
|---|---|
| [docs/01-analise-e-decisoes.md](docs/01-analise-e-decisoes.md) | Problemas, inconsistências e decisões (ADRs) — ver "Decisões revisadas em 29/09/2026" |
| [docs/02-regras-de-negocio-v2.md](docs/02-regras-de-negocio-v2.md) | Regras numeradas (RN-*) por módulo e fase |
| [docs/03-arquitetura-e-modelo-de-dados.md](docs/03-arquitetura-e-modelo-de-dados.md) | Arquitetura local-first, tabelas, endpoints, sincronização |
| [docs/04-roadmap-v2.md](docs/04-roadmap-v2.md) | Sprints e critérios de pronto (espelhado no Trello "Sorveteria") |
| [docs/05-correcoes-da-proposta.md](docs/05-correcoes-da-proposta.md) | Ajustes na proposta comercial |
| [docs/06-analise-mvp-maui.md](docs/06-analise-mvp-maui.md) | Análise do MVP, bugs B1–B15 e plano de refatoração do app |
| [docs/07-hospedagem.md](docs/07-hospedagem.md) | Hospedagem gratuita (Render + Neon + Cloudflare Pages) e limites |
| [docs/contexto-mvp/](docs/contexto-mvp/) | Documentos originais do MVP (histórico) |

## Começar em qualquer computador

### 1. Instalar (uma vez por máquina)

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) — `dotnet --version` deve mostrar 10.x
- Visual Studio 2026 (ou 2022 17.14+) com as cargas **ASP.NET e desenvolvimento web** e **.NET Multi-platform App UI** (Android)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) para o PostgreSQL local (ou PostgreSQL 16 instalado)
- Ferramenta do EF: `dotnet tool install --global dotnet-ef`
- Git (ou GitHub Desktop) e, opcional, [DBeaver](https://dbeaver.io/)

### 2. Baixar e abrir

```powershell
git clone https://github.com/PauloHenriqueCarvalho/AppSorveteria.git
cd AppSorveteria
git checkout develop
```

Abra `GestaoSorveteria.slnx` no Visual Studio.

### 3. API (backend)

```powershell
docker compose up -d                       # PostgreSQL local
dotnet build src/GestaoSorveteria.Server
dotnet test  tests/GestaoSorveteria.Tests

# Só na primeira vez (a migração fica versionada no Git depois disso):
dotnet ef migrations add InitialCreate -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server -o Persistence/Migrations

dotnet run --project src/GestaoSorveteria.Server   # Swagger em http://localhost:5080/swagger
```

Em Development a API aplica as migrações e cria o usuário `admin` / `admin12345` ao subir. No Swagger: `POST /api/auth/login` → copie o `token` → **Authorize** → `GET /api/auth/me`. Ou use `src/GestaoSorveteria.Server/GestaoSorveteria.Server.http`.

### 4. App do atendente

No Visual Studio, defina `GestaoSorveteria.Mobile` como projeto de inicialização, escolha o emulador Android (ou o celular via USB) e pressione F5. O app funciona sozinho com SQLite local; a sincronização com a API entra no Sprint 2 (no emulador, a API fica em `http://10.0.2.2:5080`).

## Estrutura

```
AppSorveteria/
├── GestaoSorveteria.slnx             solução única (app + API + testes + docs)
├── Directory.Build.props             configurações comuns a todos os projetos
├── docker-compose.yml                PostgreSQL local
├── Dockerfile                        imagem da API (Render)
├── .github/workflows/ci.yml          build + testes da API a cada push
├── docs/
├── src/
│   ├── GestaoSorveteria.Domain           entidades + regras (sem dependências)
│   ├── GestaoSorveteria.Contracts        DTOs da API (compartilhados com app e painel)
│   ├── GestaoSorveteria.Application      casos de uso
│   ├── GestaoSorveteria.Infrastructure   EF Core/PostgreSQL, repositórios, hash, seed
│   ├── GestaoSorveteria.Server           API REST
│   └── GestaoSorveteria.Mobile           app MAUI do atendente (MVP)
└── tests/
    └── GestaoSorveteria.Tests
```

Dependências apontam para dentro: `Domain ← Application ← Infrastructure ← Server`. Domain e Application nunca referenciam Infrastructure, Server ou Mobile.

## Configuração

| Chave | Dev | Piloto (variáveis de ambiente no Render) |
|---|---|---|
| `ConnectionStrings:Default` | `appsettings.Development.json` | `ConnectionStrings__Default` (Neon) |
| `Jwt:Key` (≥ 32 caracteres) | `appsettings.Development.json` | `Jwt__Key` |
| `Seed:AdminSenha` | `admin12345` | `Seed__AdminSenha` |
| `Database:MigrateOnStartup` | `true` | `true` no piloto (docs/07) |
| `Swagger:Habilitado` | `true` | `false` |

Nunca coloque segredo de produção em arquivo versionado.

## Git

- `main`: só código testado (merge vindo da `develop`)
- `develop`: trabalho do dia a dia
- `feature/<nome>` e `fix/<nome>` saindo da `develop`
- Commits: `tipo(escopo): descrição` — ex.: `fix(app): troco em dinheiro insuficiente (B3)`

O CI compila e testa a API. O app MAUI fica fora do CI por enquanto (precisa do workload Android); compile-o no Visual Studio antes de abrir PR.
