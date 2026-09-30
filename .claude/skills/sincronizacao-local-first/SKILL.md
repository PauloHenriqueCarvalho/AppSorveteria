---
name: sincronizacao-local-first
description: Contrato e checklist da sincronização entre o app do atendente (SQLite) e a API (POST /api/sync/caixas e /api/sync/comandas) — idempotência, resultados por item, API gratuita que dorme, fechamento de caixa. Use ao mexer no SyncService do app, nos endpoints de sync ou nos Contracts de sincronização.
---

# Sincronização local-first

Fonte: `docs/03-arquitetura-e-modelo-de-dados.md` (seções 6 e 8) e `docs/07-hospedagem.md`.

## Princípios
1. O celular é a fonte da verdade enquanto a comanda está aberta. A API só recebe o que está **fechado ou cancelado**.
2. **Idempotência pelo `Id`** (UUID gerado no celular): reenviar o mesmo lote nunca duplica.
3. Rejeição nunca some em silêncio: fica visível no app com o motivo.
4. A venda nunca espera a API.

## Contrato
```
POST /api/sync/caixas     body: { caixas: [ CaixaSyncDto ] }        (caixa + movimentos + fechamento, se houver)
POST /api/sync/comandas   body: { comandas: [ ComandaSyncDto ] }    (comanda + itens + pagamentos; status Fechada/Cancelada)
→ 200 { resultados: [ { id, status: "aceita" | "ja_recebida" | "rejeitada", motivo? } ] }
```
- Caixa antes das comandas daquele caixa.
- Um item inválido **não** derruba o lote: vira `rejeitada` com motivo.
- A API reconstrói os agregados com os métodos do `Domain` (mesmas regras do celular) e confere totais. Comanda: divergência → `rejeitada` (RN-SY-06). Caixa: grava os valores do servidor e marca divergência, nunca rejeita por isso (RN-CX-10). Movimentos do caixa entram antes de `FecharSincronizado`.
- Comanda de caixa já fechado no servidor → aceita e marcada `RecebidaAposFechamentoCaixa` (RN-CX-08), sem mudar os valores do caixa (RN-CX-07).
- Comanda remontada com o preço da venda (RN-SY-06), via `Comanda.Remontar`; caixa fechado no celular → valores do servidor + marca de divergência (RN-CX-10).

## Lado do app (`SyncService`)
- Tabela/flag de pendência por comanda e caixa (`pendente_envio`, `tentativas`, `ultimo_erro`, `rejeitada_motivo`).
- Dispara: ao fechar/cancelar comanda, ao fechar caixa, a cada 1 min, ao abrir o app.
- `HttpClient` com timeout de **90 s** (a API no Render Free leva ~1 min para acordar).
- `aceita`/`ja_recebida` → marca enviada. `rejeitada` → mostra ao atendente. Erro de rede/timeout/5xx → tenta de novo depois (sem apagar nada).
- Token expirado (401) → pede o PIN quando houver internet; fila espera.
- Fechar caixa exige só nenhuma comanda aberta (RN-CX-05), com aviso se houver envio pendente; sem internet, fecha localmente e sobe depois (RN-SY-04). Ordem de envio: caixa aberto → comandas → caixa fechado (RN-SY-03).
- Indicador sempre visível: "X vendas aguardando envio".

## Lado da API
- Verificar existência pelo `Id` antes de criar (`ObterPorIdAsync`) → `ja_recebida`.
- Índice único e `ValueGenerated.Never` garantem que corrida entre dois envios vire 409/`ja_recebida`, nunca duplicata.
- Uma transação por comanda (falha de uma não desfaz as outras).

## Testes obrigatórios
- Mesmo lote enviado 2× → 1 registro por comanda, segundo envio `ja_recebida`.
- Lote com 1 comanda inválida + 2 válidas → 2 `aceita`, 1 `rejeitada`.
- Pagamentos que não somam o total → `rejeitada`.
- Comanda chegando após fechamento do caixa → aceita e marcada.
- App: fila persiste após fechar/reabrir o app; envio retoma quando a rede volta.
