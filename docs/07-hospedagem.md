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
| `App__VersaoMinima` / `App__VersaoAtual` | versão mínima e atual do app (formato `1.0`); o app compara com a própria em `GET /api/versao`. Padrão `1.0` |
| `App__LinkApk` | link fixo do APK assinado, mostrado no aviso de atualização. Vazio até publicar o APK |
| `Cors__PainelOrigem` | URL do painel no Cloudflare Pages, só esquema e domínio (ex.: `https://sorveteria-painel.pages.dev`). Várias origens: separar por `;`. Vazio = nenhum site chama a API pelo navegador |
| `Database__MigrateOnStartup` | `true` **só no piloto** (um único serviço, sem réplicas); em produção paga voltar a `false` e rodar `dotnet ef database update` no deploy |
| `Clima__ChaveApi` | chave do **Visual Crossing** (clima do dia e previsão, RN-CL-07). Vazia = coleta desligada; o resto do sistema não muda |
| `Pix__*` | **Pix integrado** (RN-PX-01), seção "Pix integrado" abaixo. Vazio = desligado; o app cobra o Pix à mão, como antes |

O `Dockerfile` da raiz já está pronto: o Render faz o build a partir dele e informa a porta pela variável `PORT`, que o `Program.cs` já lê. Health check: `/health`.

### Criar o serviço pelo Blueprint (`render.yaml`)

O arquivo `render.yaml` na raiz descreve o serviço inteiro (Docker, plano Free, região, health check, branch `main`, lista de variáveis). Não é preciso preencher nada à mão no formulário do Render:

1. Render Dashboard → **New → Blueprint** → conectar o GitHub e escolher o repositório `AppSorveteria`.
2. O Render mostra o serviço `sorveteria-api` e pede o valor das variáveis marcadas `sync: false`: `ConnectionStrings__Default` (string do Neon), `Seed__AdminSenha` e `Cors__PainelOrigem` (pode ficar vazia até o painel existir). `Jwt__Key` é gerada pelo próprio Render (`generateValue`).
3. **Apply**. O primeiro build leva uns 5 min. A URL fica `https://sorveteria-api.onrender.com` (ou com sufixo, se o nome já estiver em uso).
4. Testar: `GET https://<url>/health` deve responder `Healthy`.

Região: `virginia` (AWS us-east-1), a mesma do projeto no Neon, para a API e o banco ficarem perto um do outro. O Render não tem região no Brasil; a latência Brasil ↔ Virgínia (~120 ms) é imperceptível para o app, que sincroniza em segundo plano.

Depois de criado, o serviço continua ligado ao `render.yaml`: mudança no arquivo (nova variável, outro health check) vale no próximo commit da `main`. **Valor de segredo nunca vai para o arquivo**: só no painel do Render (Environment).

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

## App do atendente: endereço da API

O endereço da API depende da configuração do build do app (`ConfiguracaoApi`, decisão do Paulo em 07/10/2026):

| Build | Endereço | Editável na tela de login? | http permitido? |
|---|---|---|---|
| **Debug** (emulador, tablet de teste via Visual Studio) | padrão `http://10.0.2.2:5080` (`10.0.2.2` é o PC visto do emulador); no tablet, trocar por `http://<IP do PC>:5080` (ex.: `http://192.168.0.10:5080`) | sim ("Servidor: …" no rodapé; fica guardado no aparelho) | sim (`usesCleartextTraffic="true"` só nesta configuração, via `AndroidManifestPlaceholders`) |
| **Release** (APK da loja) | fixo, vem do build: `-p:ApiUrlPadrao=https://<servico>.onrender.com`, gravado como metadado do assembly | **não** (o campo não existe) | **não** (manifesto com `usesCleartextTraffic="false"`; só https) |

- `scripts/publicar-apk.ps1 -ApiUrl https://<servico>.onrender.com …` passa a URL ao build (parâmetro obrigatório, só https). Um APK gerado sem ela **não** aponta para um endereço chutado: o app mostra "Este app foi instalado sem o endereço do servidor" e não tenta logar. Quando a API estiver publicada no Render, registrar a URL aqui.
- Para a API local aceitar o tablet (Debug): `dotnet run --project src/GestaoSorveteria.Server --urls http://0.0.0.0:5080` e liberar a porta 5080 no firewall do Windows; tablet na mesma rede Wi-Fi do PC.
- O login exige internet (RN-US-06); depois disso a venda segue offline com o token guardado no aparelho (docs/03, seção 7).

## Clima (Visual Crossing)

A API guarda o clima de cada dia e a previsão para cruzar com as vendas (docs/02, seção 9b). Fonte: **Visual Crossing**, plano grátis — permite uso comercial, 1.000 registros por dia (a loja usa algumas dezenas: um registro por dia consultado), mais de 50 anos de histórico e 15 dias de previsão. Exige atribuição visível: o painel mostra "Dados do clima: Visual Crossing" na tela de clima. Não usar o Open-Meteo grátis: os termos dele proíbem uso comercial (o plano comercial custa US$ 29/mês).

1. Criar a conta grátis no site do Visual Crossing (visualcrossing.com) e copiar a **API key** na página da conta.
2. Render → `sorveteria-api` → Environment → `Clima__ChaveApi` = a chave → Save (a API reinicia).
3. Painel → Configurações → **Local da loja (clima)**: escrever a cidade (ex.: "Ribeirão Preto, SP"), **Procurar**, conferir o lugar encontrado e **Confirmar**. A API já busca o histórico desde a primeira venda e a previsão dos próximos dias.
4. Conferir em Configurações a "Última coleta". Erro de chave aparece ali ("A chave do serviço de clima foi recusada").

A coleta roda quando a API está acordada (ao ligar e a cada 3 h). A API dormindo não perde dias: na próxima rodada ela busca o que faltou. Para testar local: `dotnet user-secrets set "Clima:ChaveApi" "<chave>" --project src/GestaoSorveteria.Server`.

## Pix integrado (Inter PJ)

A API cria a cobrança com o valor exato no banco da loja e consulta até o cliente pagar (docs/02, seção 5a). As credenciais ficam **só no Render**: nunca no app, no painel, no Git ou em mensagem.

**1. A dona cria a integração** (o login do Internet Banking é dela): Internet Banking do Inter PJ (no computador, entra pelo QR Code) → **Soluções para sua empresa** → **Nova integração** → nome "Sistema da sorveteria" → permissões **Emissão e consulta de cobranças Pix** (`cob.write`, `cob.read`) — só essas. Depois de criada: baixar o **certificado e a chave** (um .zip com um `.crt` e um `.key`) e copiar o **Client ID** e o **Client Secret**. O certificado vale **1 ano**: na véspera de vencer, criar outra integração e trocar as variáveis (anotar a data no Trello).

**2. Passar para o Render** (Environment → Add Environment Variable). O conteúdo dos arquivos vai inteiro, com as linhas `-----BEGIN…-----` e `-----END…-----`:

| Variável | Valor |
|---|---|
| `Pix__Provedor` | `Inter` (vazio = desligado) |
| `Pix__ClientId` / `Pix__ClientSecret` | da integração |
| `Pix__CertificadoPem` | conteúdo do `.crt` |
| `Pix__ChavePrivadaPem` | conteúdo do `.key` |
| `Pix__Chave` | a chave Pix da conta que recebe (CNPJ, e-mail, telefone ou aleatória) |
| `Pix__ContaCorrente` | opcional: número da conta, só se a integração enxergar mais de uma |
| `Pix__Ambiente` | `Producao` (padrão) ou `Sandbox` (credenciais do sandbox do Inter, `developers.inter.co/sandbox`) |
| `Pix__ExpiracaoSegundos` | opcional, padrão `600` (10 min) |

Ao subir, o log da API diz `Pix integrado: Inter` ou o motivo de estar desligado (ex.: "falta Pix:CertificadoPem", "o certificado não abriu"). **Teste:** um Pix de R$ 0,01 pelo app do balcão e conferir no extrato.

**Testar sem banco** (emulador, tablet de teste, nunca em produção): `Pix__Provedor=Simulado` na API local (`dotnet user-secrets set "Pix:Provedor" "Simulado" --project src/GestaoSorveteria.Server`). Toda cobrança fica paga depois de 8 s (`Pix__SimuladoPagaEmSegundos`). Fora de `Development` o Simulado é desligado sozinho.

## Backup no piloto

O Neon grátis guarda histórico curto de restauração. Mesmo assim: **`pg_dump` semanal** feito do PC do Paulo e guardado fora da nuvem. Vendas também ficam no SQLite do celular até serem confirmadas pela API.

### Fazer o backup: `scripts/backup.ps1`

Gera `sorveteria-AAAA-MM-DD-HHMM.dump` (formato *custom* do `pg_dump`, com `--no-owner --no-privileges` para restaurar em qualquer servidor), confere que o arquivo abre (`pg_restore --list`) e apaga os mais antigos além de `-Manter` (padrão 12 ≈ 3 meses). Pasta padrão: `%USERPROFILE%\Backups\sorveteria`. Testado em 07/10/2026 contra o PostgreSQL local, com restauração conferida tabela a tabela.

Pré-requisito: `pg_dump`/`pg_restore` **na mesma versão ou mais nova** que o servidor. O Neon usa PostgreSQL 17; no PC do Paulo está o 18 (`C:\Program Files\PostgreSQL\18\bin`, o script acha sozinho). Para instalar só as ferramentas: instalador do PostgreSQL → marcar apenas *Command Line Tools*.

A string de conexão **não fica no script nem no Git**. Três formas de informar, na ordem em que o script procura:

1. `-Url '...'` (fica no histórico do terminal: evitar);
2. variável de ambiente `SORVETERIA_DB_URL` **do usuário do Windows** (recomendado para o agendamento):
   ```powershell
   [Environment]::SetEnvironmentVariable('SORVETERIA_DB_URL', '<string do Neon>', 'User')   # uma vez; abrir um terminal novo depois
   ```
3. sem nada: o script pergunta na tela, sem mostrar o que é digitado.

Aceita a URL do Neon (`postgresql://usuario:senha@ep-xxx.us-east-1.aws.neon.tech/sorveteria?sslmode=require`) ou o formato Npgsql usado no Render. No Neon, copiar a string do endpoint **direto** (sem `-pooler` no host): o `pg_dump` não funciona bem pelo pooler.

```powershell
.\scripts\backup.ps1                                  # pasta padrão, mantém 12
.\scripts\backup.ps1 -Pasta D:\Backups\sorveteria     # outra pasta (ex.: pendrive)
```

**Agendar toda segunda às 8h** (Agendador de Tarefas do Windows; o PC precisa estar ligado — se não estiver, a tarefa roda na próxima vez que ligar):

```powershell
schtasks /Create /TN "Backup Sorveteria" /SC WEEKLY /D MON /ST 08:00 /F /TR "powershell -NoProfile -ExecutionPolicy Bypass -File C:\Users\<usuario>\source\repos\AppSorveteria\scripts\backup.ps1"
schtasks /Run /TN "Backup Sorveteria"                 # testar agora
```

Depois de cada backup, copiar a pasta para fora do PC (pendrive, OneDrive/Drive pessoal). Guardar fora da nuvem do Neon é o objetivo.

### Restaurar

Nunca restaurar por cima do banco em uso sem antes fazer um backup dele. Os comandos abaixo usam as ferramentas do PostgreSQL (`$env:PGPASSWORD` evita a senha na linha de comando).

**Testar a restauração no PC** (fazer uma vez por mês, leva 1 min; PostgreSQL local do `docker compose up -d`):

```powershell
$env:PGPASSWORD = 'postgres'
psql -h localhost -p 5433 -U postgres -d postgres -c "CREATE DATABASE sorveteria_restaurado;"
pg_restore -h localhost -p 5433 -U postgres -d sorveteria_restaurado --no-owner --no-privileges "$HOME\Backups\sorveteria\sorveteria-2026-10-07-0800.dump"
psql -h localhost -p 5433 -U postgres -d sorveteria_restaurado -c "select count(*) from comandas;"
psql -h localhost -p 5433 -U postgres -d postgres -c "DROP DATABASE sorveteria_restaurado;"
Remove-Item Env:\PGPASSWORD
```

Se o servidor for mais antigo que o `pg_dump` (ex.: PostgreSQL 16 local com ferramentas 18), aparece `ERROR: unrecognized configuration parameter "transaction_timeout"` e `errors ignored on restore: 1`. É inofensivo: os dados são restaurados do mesmo jeito. No Neon (17) não aparece.

**Voltar o banco do Neon a um backup** (desastre: dados apagados, migração errada):

1. Painel do Neon → Branches → criar uma branch nova a partir da `main` (fica um banco de segurança com o estado atual, sem custo).
2. Com a string do endpoint direto da `main`:
   ```powershell
   $env:PGPASSWORD = '<senha do Neon>'
   pg_restore -h ep-xxx.us-east-1.aws.neon.tech -U <usuario> -d sorveteria --clean --if-exists --no-owner --no-privileges "<arquivo>.dump"
   Remove-Item Env:\PGPASSWORD
   ```
   `--clean --if-exists` apaga e recria as tabelas antes de carregar; tudo o que foi gravado depois do backup se perde no servidor, mas as vendas ainda no celular sobem de novo na próxima sincronização (idempotente pelo `Id`).
3. Suspender e religar a API no Render (Manual Deploy → *Restart*) para limpar conexões antigas. Conferir pelo painel se o dia aparece.

Alternativa sem `pg_restore`: o Neon grátis permite *Restore* da branch para um ponto nas últimas 24 h (Branches → Restore). Serve para erro percebido no mesmo dia; para mais do que isso, o backup semanal é o único caminho.

## Quando sair do gratuito (fim do piloto)

Critérios: a loja passa a depender do painel em tempo real, o sono de 1 min incomoda, ou o banco passa de ~70% do limite. Caminho mais simples: API no Render pago (Starter) ou Railway Hobby, banco no Neon pago — trocar só variáveis de ambiente, sem mudar código. Rever preços na época.

## Fontes (consultadas em 29/09/2026)

- Render — Deploy for Free: https://render.com/docs/free
- Neon — Pricing: https://neon.com/pricing
- Vercel — Fair Use Guidelines (Commercial usage): https://vercel.com/docs/limits/fair-use-guidelines
- Cloudflare Community — uso comercial no plano grátis do Pages: https://community.cloudflare.com/t/does-the-pages-free-plan-allow-a-static-site-owned-by-an-llc-that-links-to-an-exter/961849
