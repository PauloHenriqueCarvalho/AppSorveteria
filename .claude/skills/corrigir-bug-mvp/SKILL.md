---
name: corrigir-bug-mvp
description: Fluxo para corrigir um bug do app MAUI (lista B1–B15 em docs/06) ou um bug novo — reproduzir, escrever teste quando a regra for de domínio, corrigir no lugar certo, validar no emulador e fechar o cartão do Trello. Use quando a tarefa citar um bug B* ou um defeito no app.
---

# Corrigir bug do app

## 1. Identificar
- Ache o bug em `docs/06-analise-mvp-maui.md` (seção 2) ou no cartão `[Bug B*]` do Trello. Bug novo: registre como `B16`, `B17`… em docs/06 com arquivo, problema e efeito.
- Crie a branch: `git checkout develop && git pull && git checkout -b fix/b<N>-<resumo>` (ex.: `fix/b3-dinheiro-insuficiente`).

## 2. Reproduzir
- Escreva os passos no emulador (ex.: "venda de R$ 10, pagar em dinheiro com R$ 5 → comanda fecha").
- Se a causa for **regra de cálculo** (total, troco, caixa), ela deve estar no `Domain`: escreva primeiro o teste que falha em `tests/GestaoSorveteria.Tests/Domain/` (agente `testador`).

## 3. Corrigir no lugar certo
| Tipo de causa | Onde corrigir |
|---|---|
| Cálculo de dinheiro, troco, total, caixa | `Domain` (e o app passa a chamar o domínio) |
| Gravação inconsistente / sem transação | camada de dados do app (`RunInTransactionAsync`) |
| Erro engolido | remover `catch` silencioso; mostrar erro ao atendente |
| Fluxo/tela | ViewModel/View |

Respeite as invariantes do `CLAUDE.md` (decimal, UTC, `ProdutoId = null` para item livre, nada apagado).

## 4. Validar
- `dotnet test tests/GestaoSorveteria.Tests` verde.
- No Visual Studio: rodar o app no emulador e repetir os passos de reprodução; testar também **sem internet**.
- Rode o agente `revisor-dominio` no diff.

## 5. Fechar
- Commit: `fix(app): <o que mudou> (B<N>)`.
- Em `docs/06`, marque o bug como corrigido (ex.: `~~B3~~ corrigido em <commit>`).
- Trello: marque o item/cartão do bug como concluído e mova para "Concluido"; se fazia parte da Etapa A/B, marque o item correspondente no checklist.
- Siga a skill `fechar-tarefa`.
