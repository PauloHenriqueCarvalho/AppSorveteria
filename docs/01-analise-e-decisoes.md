# 01 — Análise e decisões

> Base: `proposta_sorveteria_v2.pdf` (abr/2026), `roadmap_sorveteria.docx`, `Visao-Geral-do-Sistema.md`, `Regras-de-Negocio.md` e a pasta `source/repos/GestaoSorveteria` existente no PC.
> Data da análise: 17/09/2026.

## 1. Resumo executivo

O projeto tem uma proposta comercial boa e um roadmap técnico razoável, mas os documentos se contradizem em pontos importantes, o código iniciado em abril está inconsistente (mistura .NET Framework 4.7.2 com .NET 8 e .NET 10) e — o mais grave — **um dos cinco problemas prometidos à dona não tem nenhuma funcionalidade que o resolva** (controle de caixa).

O que muda com esta reestruturação:

| Área | Antes | Agora |
|---|---|---|
| Problema "caixa não bate" | Sem funcionalidade | Módulo **Caixa** (abertura com fundo de troco, sangria/suprimento, fechamento com conferência e diferença) na Fase 1 |
| Painel da dona | WinForms instalado no PC | **Painel web** (Blazor) hospedado junto com a API: abre no PC e no celular, atualiza sozinho |
| Offline | Prometido na proposta, ausente no roadmap | Arquitetura **preparada para offline** desde o Sprint 0 (IDs gerados no celular, API idempotente, fila local no app) |
| Banco | "SQL Server" na proposta, PostgreSQL no roadmap | **PostgreSQL** (custo zero de licença, roda em qualquer nuvem) |
| Versão .NET | net472 / net8.0 / net10.0 misturados | **.NET 10 LTS** em tudo (suporte até nov/2028) |
| Fases | "Fase 1–4" significava coisas diferentes na proposta e no roadmap | Proposta = **Fases** (comercial). Roadmap = **Sprints** (técnico) |
| Regras novas (self-service, venda por valor) | Só no documento de regras | Refletidas no modelo de dados (item livre sem produto) |
| Código | 6 projetos-template desconexos | Solução limpa em Clean Architecture, compilável, com testes |

## 2. Os cinco problemas da dona → o que resolve cada um

| # | Problema (proposta) | Solução | Onde | Fase |
|---|---|---|---|---|
| 1 | Pedidos anotados em papel | Comanda digital: abrir, adicionar itens com 1 toque, fechar com pagamento e troco. Item livre para self-service e venda rápida por valor | App do atendente + API | 1 |
| 2 | Estoque "no olhômetro" | Cadastro de insumos com mínimo, baixa automática por **ficha técnica** (produto → insumos), alertas | Painel + API | 2 |
| 3 | Não sabe o que vende mais | Ranking de produtos, vendas por dia/semana/mês, comparativo de períodos, horários de pico | Painel | 3 |
| 4 | **Caixa manual — "a conta não bate"** | **Abertura de caixa com fundo de troco, sangria/suprimento, fechamento com valor contado × valor esperado e diferença** | App do atendente + Painel | **1 (novo)** |
| 5 | Reposição sem planejamento | Previsão de ruptura (dias restantes = estoque atual ÷ consumo médio diário), lista de compras sugerida | Painel | 2 |

O problema 4 estava listado na proposta e na visão geral, mas nenhuma fase o entregava. Entrou na Fase 1 porque é pré-requisito para o relatório de faturamento por forma de pagamento (Fase 3) e é o que a dona vai conferir todo dia.

## 3. Inconsistências entre os documentos

| Tema | Proposta (PDF) | Roadmap (DOCX) | Regras (MD) | Decisão |
|---|---|---|---|---|
| Banco de dados | SQL Server | PostgreSQL (Railway) | — | **PostgreSQL**. Corrigir a proposta (ver doc 05) |
| Offline | "Funciona offline e sincroniza" | App chama a API via HTTP, sem fila local | — | Preparar para offline no Sprint 0; fila local no app no Sprint 2 |
| Painel da dona | "Aplicativo Windows instalado" | WinForms consumindo a API | — | Painel web (Blazor Server) no mesmo host da API |
| Prazo da Fase 1 | 3 a 4 semanas | 4 a 5 semanas (Sprint 0 + 4 semanas) | — | **5 semanas** (Sprint 0 + Sprints 1–4). Alinhar a proposta |
| Nome das etapas | Fase 1, 2, 3, 4 (comercial) | "FASE 0–4" = semanas da Fase 1 | Fase 1, 2, 3 | Proposta usa **Fase**; roadmap usa **Sprint** |
| Formas de pagamento | Dinheiro ou Pix | Dinheiro/Pix | Dinheiro ou Pix | Adicionar **cartão débito/crédito** (custo zero no código; confirmar com a dona) |
| Self-service e venda por valor | Não aparecem | Não aparecem | Regras 1.7 e 1.8 | Entram na Fase 1 (item livre na comanda) |
| Controle de estoque de picolés/sabores | "Controle completo" | — | Exceção: só insumos | Fase 2 controla **insumos**; produtos sem ficha técnica não baixam estoque. Explicar na proposta |
| Hospedagem | "R$ 0 a R$ 360/ano" | "Railway — grátis para começar" | — | Railway não é mais grátis: plano Hobby ≈ US$ 5/mês (≈ R$ 30) mais uso. Orçar **R$ 30–50/mês** ou alternativa (ver seção 6) |
| Data do documento | "03 de April de 2026" | Rodapé "2025" | — | Corrigir para "abril de 2026" |
| Tecnologia do painel na proposta | ".NET MAUI … Aplicativo Windows" | WinForms | — | "Painel web (abre no navegador)" |

## 4. Lacunas nas regras de negócio (o que faltava decidir)

1. **Cancelamento**: não existia regra para remover item, cancelar comanda aberta ou estornar venda fechada. Definido em RN-CM-08/09 (doc 02): atendente cancela comanda aberta; só Admin estorna comanda fechada, com motivo; nada é apagado.
2. **Ficha técnica**: a "baixa automática" de insumos exige saber quantos gramas de cada insumo cada produto consome. Não existia no modelo. Definido para a Fase 2 (doc 03, seção 5).
3. **Custo e margem**: "margem de lucro por produto" exige custo. Definido: custo do insumo é atualizado a cada entrada de reposição (custo médio); custo do produto = soma da ficha técnica. Produtos sem ficha técnica aceitam custo informado manualmente.
4. **Preço no momento da venda**: o item guarda o preço praticado (snapshot). Mudar o preço do produto não altera vendas passadas.
5. **Fuso horário**: "vendas do dia" precisa de um "dia" definido. Tudo é salvo em UTC; o dia comercial é calculado em `America/Sao_Paulo`.
6. **Numeração da comanda**: o atendente precisa de um número curto ("comanda 12"). Sequencial por caixa, reinicia a cada abertura de caixa.
7. **Quem fecha o caixa**: o celular do balcão (depois de sincronizar a fila offline). O painel mostra o caixa e permite fechamento forçado pelo Admin.
8. **Vendas que chegam depois do caixa fechado** (sincronização atrasada): são aceitas e marcadas para conferência, nunca rejeitadas — venda registrada é sagrada.
9. **Delivery**: na Fase 1 é só uma marcação (tipo Delivery + observação com nome/endereço). Taxa de entrega e integração iFood ficam na Fase 4.
10. **Backup**: a promessa "nenhuma informação se perde" exige backup automático do banco (diário) — entra no Sprint 4 como entrega, não como intenção.
11. **Segurança do PIN**: PIN de 4 dígitos do atendente é aceitável no balcão **somente** com limite de tentativas no login (rate limit) e token com validade de turno (12 h). A dona usa senha de verdade (mín. 8 caracteres).
12. **Múltiplos dispositivos**: o modelo suporta vários atendentes/celulares, mas a Fase 1 assume um celular no balcão.

## 5. Estado do código existente (`source/repos/GestaoSorveteria`)

Criado em 06/04/2026, sem Git. Problemas encontrados:

- `Domain`, `Application` e `Infrastructure` são class libraries **.NET Framework 4.7.2** (csproj antigo). Uma API .NET 8/10 não consegue referenciá-las.
- `Domain.csproj` e `Application.csproj` referenciam `Class1.cs`, que não existe → não compilam.
- `Application` não referencia `Domain`; `Infrastructure` referencia os dois (fluxo de dependência errado).
- `API` é o template `weatherforecast` em **net8.0**, sem referência a nenhuma camada.
- `MAUI` é o template padrão em **net10.0** (bom sinal: o SDK .NET 10 e o workload MAUI estão instalados no PC).
- `WinForms` é template .NET Framework 4.7.2.
- `GestaoSorveteria.slnx` só inclui API, MAUI e WinForms.

Decisão: **recomeçar com a estrutura correta**. A pasta antiga foi renomeada para `GestaoSorveteria_old_2026-04` (nada foi apagado) e a nova solução ocupa `GestaoSorveteria`.

## 6. Riscos técnicos e mitigação

| Risco | Impacto | Mitigação |
|---|---|---|
| Internet cai no balcão | Venda perdida, fila de clientes | Fila local (outbox) no app: a venda é gravada no celular e enviada quando voltar a conexão. IDs gerados no celular; API idempotente |
| Dona fecha caixa com vendas ainda não sincronizadas | Caixa "não bate" | Fechamento pelo celular após sincronizar; vendas tardias marcadas para conferência |
| Custo de hospedagem subestimado | Surpresa para a dona | Orçar R$ 30–50/mês (Railway Hobby ≈ US$ 5 + uso) ou VPS nacional de ~R$ 30. Deixar claro na proposta |
| Atualizar o app no celular (APK manual) | Correções demoram a chegar | Endpoint `/api/versao` + aviso "nova versão" no app; APK publicado em link fixo |
| Perda do banco | Perda total do histórico | Backup diário automático (`pg_dump`) + cópia semanal fora da nuvem (Sprint 4) |
| PIN de 4 dígitos | Acesso indevido | Rate limit no login (10 tentativas/min por IP), token de 12 h, atendente sem acesso a endpoints de gestão |
| Migrações do banco em produção | Downtime/erro | `MigrateOnStartup` só em Development; em produção rodar `dotnet ef database update` no deploy |
| Trabalhar sozinho sem controle de versão | Perder código | Git desde o Sprint 0 + GitHub privado + CI (build + testes a cada push) |

## 7. Decisões de arquitetura (ADRs)

| ADR | Decisão | Por quê |
|---|---|---|
| 001 | **.NET 10 LTS** em todos os projetos | Suporte até nov/2028; .NET 8 encerra em nov/2026; SDK e MAUI 10 já instalados no PC |
| 002 | **PostgreSQL** via Npgsql + EF Core 10 | Sem licença, nativo em Railway/Neon/Supabase; trocar de provedor no EF é uma linha |
| 003 | Clean Architecture: `Domain ← Application ← Infrastructure ← Server`; `Contracts` compartilhado com o app | Regra de ouro do roadmap mantida; DTOs num projeto próprio para o MAUI referenciar sem carregar regras de negócio |
| 004 | **Um host** (`Server`) com API REST (JWT, para o app) **e** painel Blazor Server (cookie, para a dona) | Um deploy, uma URL, um banco. Painel funciona no PC e no celular sem instalar nada |
| 005 | Preparado para offline: cliente gera `Guid` das comandas; `POST` idempotente (mesmo Id → mesma resposta) | Sem isso, a fila offline duplicaria vendas |
| 006 | Regras de negócio **dentro das entidades** (métodos que validam e lançam `DomainException`) | Testável sem banco; Application orquestra, Controller só recebe/devolve |
| 007 | Enums gravados como **texto** e colunas em **snake_case** | Legível no DBeaver sem tabela de códigos; padrão PostgreSQL |
| 008 | Senha com **PBKDF2-SHA256** (BCL, sem pacote extra) | Mesma segurança do BCrypt para este cenário, zero dependência; formato versionado permite trocar depois |
| 009 | Dinheiro em `decimal(12,2)`, arredondamento `AwayFromZero`; datas em UTC (`timestamptz`); dia comercial em `America/Sao_Paulo` | Evita erro de centavos e "venda de ontem aparecendo hoje" |
| 010 | **Caixa** como agregado próprio; toda comanda pertence a um caixa aberto | Resolve o problema 4 e dá base para o relatório por forma de pagamento |
| 011 | Testes de domínio e de aplicação com **xUnit v3** desde o Sprint 0 | Regras de troco, total e fechamento de caixa são o coração do sistema — precisam de prova |
| 012 | Migrações EF Core geradas no PC (`dotnet ef migrations add`) e versionadas no Git | Histórico do schema; nunca SQL manual |

### Decisões revisadas em 29/09/2026

Depois da análise do MVP (docs/06) e da conversa sobre hospedagem, estas decisões **substituem** as anteriores quando houver conflito:

| ADR | Decisão | Por quê |
|---|---|---|
| 004 (substituída) | ~~Um host com API + painel Blazor Server~~ → **API sozinha no `Server`; painel como site estático separado** (Blazor WebAssembly ou React + Vite — a definir no Sprint 3) | Site estático hospeda de graça (Cloudflare Pages); o painel só lê e gerencia via API |
| 005 (ampliada) | **Local-first**: comanda vive no celular; a API recebe comandas e caixas prontos por `POST /api/sync/*`, idempotente pelo `Id` | Venda nunca espera internet; menos endpoints; aceita API que dorme |
| 013 | **Continuar sobre o MVP MAUI** (`src/GestaoSorveteria.Mobile`), refatorando em 3 etapas | Fluxos e telas já validados; reescrever custaria 1–2 semanas |
| 014 | O app referencia `Domain` e `Contracts` | Total, troco e caixa com o mesmo código (e testes) no celular e na API |
| 015 | **Hospedagem gratuita** até o fim do desenvolvimento e no piloto: API no Render Free (dorme), PostgreSQL no Neon Free, painel no Cloudflare Pages (docs/07) | Custo zero; o modelo local-first tolera a API dormindo |
| 016 | **Repositório único** `AppSorveteria` no GitHub (app, API, testes, docs), trabalho em `develop`, `main` só com código testado | Desenvolver de qualquer computador com um `git clone` |
| 017 | O sistema segue o **melhor funcionamento**, não a proposta comercial ao pé da letra | A proposta é ajustada depois ao que foi construído (docs/05) |

## 8. O que **não** muda

- Preço fechado com a dona (R$ 1.300 em 3 fases) — decisão comercial sua; o escopo cresceu (caixa, offline, painel web), então vale formalizar o escopo por escrito (doc 05).
- Stack .NET/C# em tudo (API, painel, app) — alinhada ao que você treina.
- Entregas incrementais mostrando algo funcionando a cada semana.
