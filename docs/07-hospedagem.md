# 07 — Hospedagem (gratuita no desenvolvimento e no piloto)

> Decisão de 29/09/2026: **custo zero de hospedagem** até o fim do desenvolvimento e durante a fase piloto na loja. A API pode "dormir" quando ninguém usa — o app do atendente funciona offline e envia as vendas quando a API acorda.

## Onde cada parte roda

| Parte | Onde | Plano | Custo | Observação |
|---|---|---|---|---|
| App do atendente (MAUI) | Celular Android do balcão | — | R$ 0 | APK instalado por link; SQLite local |
| Painel da dona | **Cloudflare Pages** | Free | R$ 0 | Site estático; o plano grátis permite uso comercial |
| API (`GestaoSorveteria.Server`) | **Render** (Web Service, Docker) | Free | R$ 0 | Dorme após 15 min sem requisições; acorda em ~1 min |
| Banco PostgreSQL | **Neon** | Free | R$ 0 | 0,5 GB por projeto; suspende após 5 min ocioso e acorda sozinho |
| Código + CI | GitHub (repositório privado) + GitHub Actions | Free | R$ 0 | |

**Por que não Vercel para o painel:** o plano Hobby da Vercel é restrito a uso pessoal e não comercial, e "receber pagamento para criar o site" conta como uso comercial. O Cloudflare Pages é equivalente para site estático e permite uso comercial no plano grátis.

**Por que não o PostgreSQL grátis do Render:** o banco grátis do Render expira 30 dias após a criação (e é apagado 14 dias depois). O Neon grátis é permanente.

## Limites que importam

| Limite | Valor | Impacto no projeto |
|---|---|---|
| Render Free — horas por mês | 750 h por workspace | Um único serviço (a API) cabe no mês inteiro (~730 h) |
| Render Free — sono | 15 min sem tráfego → dorme; ~1 min para acordar | Primeira sincronização/abertura do painel após um período parado demora ~1 min. O celular continua vendendo |
| Neon Free — armazenamento | 0,5 GB | Uma venda ocupa poucos KB: sobra para anos de piloto. Monitorar no painel do Neon |
| Neon Free — computação | 100 CU-hora por mês | Suficiente para uma loja; se acabar, o banco para até o mês seguinte → monitorar |
| Neon Free — sono | 5 min ocioso, não desligável | Primeira consulta após sono leva alguns segundos |

## Como o sistema convive com a API dormindo

1. O app **nunca** depende da API para vender (local-first — docs/06, seção 3).
2. O `SyncService` do app tenta enviar a fila a cada minuto; a primeira tentativa acorda a API, as seguintes passam. Tempo limite do HttpClient na sincronização: **90 s** (cobre o "acordar").
3. O painel mostra "Conectando ao servidor…" enquanto a API acorda, em vez de erro.
4. **Não** usar "pingadores" externos para manter a API acordada: consome as 750 h do mês do mesmo jeito e vai contra o uso justo do plano. Se o sono virar problema, é a hora de migrar (abaixo).

## Configuração da API no Render

| Variável | Valor |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | string do Neon no formato Npgsql, com `SSL Mode=Require` |
| `Jwt__Key` | segredo aleatório com 32+ caracteres |
| `Seed__AdminSenha` | senha inicial da dona (trocar no 1º acesso) |
| `Cors__PainelOrigem` | URL do painel no Cloudflare Pages, só esquema e domínio (ex.: `https://sorveteria-painel.pages.dev`). Várias origens: separar por `;`. Vazio = nenhum site chama a API pelo navegador |
| `Database__MigrateOnStartup` | `true` **só no piloto** (um único serviço, sem réplicas); em produção paga voltar a `false` e rodar `dotnet ef database update` no deploy |

O `Dockerfile` da raiz já está pronto: o Render faz o build a partir dele e informa a porta pela variável `PORT`, que o `Program.cs` já lê. Health check: `/health`.

## Configuração do painel no Cloudflare Pages

Projeto do painel: `src/GestaoSorveteria.Painel` (React + Vite + TypeScript — ADR 018). Criar em **Workers & Pages → Create → Pages → Connect to Git**, repositório `AppSorveteria`:

| Campo | Valor |
|---|---|
| Production branch | `main` (a `develop` gera deploys de prévia, com URL própria) |
| Framework preset | `React (Vite)` (ou `None` com os campos abaixo) |
| Root directory | `src/GestaoSorveteria.Painel` |
| Build command | `npm run build` (o Pages roda `npm ci` antes, pelo `package-lock.json`) |
| Build output directory | `dist` |
| Build watch paths (Settings → Builds) | incluir só `src/GestaoSorveteria.Painel/*` — mudança só na API ou no app não gera deploy |
| Versão do Node | lida do `.nvmrc` do painel (Node 24 LTS); se precisar forçar, variável `NODE_VERSION` |

| Variável (Settings → Variables and Secrets) | Production | Preview |
|---|---|---|
| `VITE_API_URL` | URL da API no Render (ex.: `https://sorveteria-api.onrender.com`, sem barra no fim) | a mesma, ou uma API de teste |

- `VITE_API_URL` é gravada no site **na hora do build** e fica visível no navegador: não é segredo. Trocou o valor → refazer o deploy (Deployments → Retry).
- Rotas do painel (`/produtos`, `/login`…): sem `404.html` na saída, o Pages trata o site como SPA e devolve o `index.html` — não precisa de `_redirects`.
- Depois do primeiro deploy, colocar a URL `https://<projeto>.pages.dev` em `Cors__PainelOrigem` na API (Render).
- Local: `npm run dev` usa `http://localhost:5080` quando `VITE_API_URL` não está definida; para outra API, criar `src/GestaoSorveteria.Painel/.env.local` com `VITE_API_URL=...` (ignorado pelo Git).

## Backup no piloto

O Neon grátis guarda histórico curto de restauração. Mesmo assim: **`pg_dump` semanal** feito do PC do Paulo (ou GitHub Actions agendado) e guardado fora da nuvem. Vendas também ficam no SQLite do celular até serem confirmadas pela API.

## Quando sair do gratuito (fim do piloto)

Critérios: a loja passa a depender do painel em tempo real, o sono de 1 min incomoda, ou o banco passa de ~70% do limite. Caminho mais simples: API no Render pago (Starter) ou Railway Hobby, banco no Neon pago — trocar só variáveis de ambiente, sem mudar código. Rever preços na época.

## Fontes (consultadas em 29/09/2026)

- Render — Deploy for Free: https://render.com/docs/free
- Neon — Pricing: https://neon.com/pricing
- Vercel — Fair Use Guidelines (Commercial usage): https://vercel.com/docs/limits/fair-use-guidelines
- Cloudflare Community — uso comercial no plano grátis do Pages: https://community.cloudflare.com/t/does-the-pages-free-plan-allow-a-static-site-owned-by-an-llc-that-links-to-an-exter/961849
