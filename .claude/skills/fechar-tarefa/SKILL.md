---
name: fechar-tarefa
description: Checklist para encerrar qualquer tarefa do projeto da sorveteria — build e testes, revisão, commit no padrão, atualização de docs e do quadro do Trello "Sorveteria", push. Use sempre ao terminar uma funcionalidade, correção ou documento.
---

# Fechar tarefa

## 1. Verificar
```powershell
dotnet build src/GestaoSorveteria.Server
dotnet test  tests/GestaoSorveteria.Tests
```
- Mexeu no app MAUI? Compile e rode no Visual Studio (o CI não compila o app).
- Rode o agente `revisor-dominio` no diff. Corrija os itens **Crítico** e **Importante**.
- `git diff` sem segredos (senhas, chaves, connection strings de produção).

## 2. Documentar
| Mudou… | Atualize |
|---|---|
| Regra de negócio | `docs/02-regras-de-negocio-v2.md` |
| Endpoint, tabela, protocolo | `docs/03-arquitetura-e-modelo-de-dados.md` |
| Bug do app | `docs/06-analise-mvp-maui.md` (marcar corrigido) |
| Deploy/hospedagem | `docs/07-hospedagem.md` |
| Fim de sprint | `docs/04-roadmap-v2.md` (checkboxes) e a seção "Estado atual" do `CLAUDE.md` |

## 3. Commitar
- Formato: `tipo(escopo): descrição` em português. Tipos: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`. Escopos: `app`, `api`, `domain`, `infra`, `painel`, `repo`, `docs`.
- Cite a regra ou o bug: `fix(app): recusa dinheiro insuficiente no pagamento (B3)`.
- Branch `feature/*` ou `fix/*` → merge na `develop`. `main` só recebe da `develop` com tudo testado.

## 4. Trello (quadro "Sorveteria")
- Marque os itens do checklist concluídos no cartão.
- Cartão terminado → lista **Concluido**. Em progresso → **Em Andamento**.
- Descobriu trabalho novo → cartão no **Backlog** com etiqueta (vermelha bug crítico, laranja bug importante, azul app, roxa API/deploy, verde painel, amarela proposta).
- Dúvida de negócio → item no cartão da lista **Dúvidas para a dona**.

## 5. Enviar
```powershell
git push
```
Confira o CI verde no GitHub Actions.
