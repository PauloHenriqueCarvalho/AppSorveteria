---
name: proximo-passo
description: Ciclo automático de desenvolvimento da sorveteria — escolhe a próxima tarefa do roadmap (ou a que o Paulo indicar), implementa com os agentes do projeto, testa, revisa, abre PR para a develop, acompanha o CI e faz o merge quando estiver verde. Use quando o Paulo pedir "próximo passo", "continua", "segue o roadmap" ou rodar /proximo-passo.
---

# Próximo passo (ciclo automático)

Uma volta deste ciclo = **uma tarefa pequena entregue em um PR para a `develop`**. Repita o ciclo enquanto houver tarefa e nada exigir o Paulo. Nunca toque na `main`.

## 0. Situação
```powershell
git fetch origin
git status
```
- Árvore suja → pare e pergunte.
- Funciona tanto na pasta principal quanto num `git worktree` (a `develop` pode estar em uso na outra pasta; por isso a branch nova sai de `origin/develop`, não da `develop` local).
- Leia `CLAUDE.md` (seção "Estado atual") e `docs/04-roadmap-v2.md`.

## 1. Escolher a tarefa
- Se o Paulo indicou uma tarefa (ex.: "Etapa A", "B3", "Sprint 1"), use ela.
- Senão, pegue o **primeiro item não marcado `- [ ]`** do sprint em andamento em `docs/04-roadmap-v2.md`, na ordem do arquivo. Sprint 1 (API) e Sprint 2 (app) podem andar em paralelo em sessões diferentes — respeite a frente pedida na sessão.
- Tamanho: um PR deve ter **uma ideia** e caber em ~400 linhas alteradas (sem contar migração gerada). Item grande → quebre em partes e faça só a primeira; anote as outras no roadmap como sub-itens.
- Escreva em 3–5 linhas o plano (o que muda, onde, como testar) antes de codar.

## 2. Branch
```powershell
git switch -c feature/<curto> origin/develop   # ou fix/b<N>-<curto> para bug
```

## 3. Implementar
- API → siga o agente `dev-api` e a skill `nova-funcionalidade-api`. Sincronização → skill `sincronizacao-local-first`. Schema → skill `migracao-ef`.
- App → siga o agente `dev-mobile`; bug do MVP → skill `corrigir-bug-mvp`.
- Testes → agente `testador` (regra nova ou bug de cálculo: teste primeiro).

## 4. Verificar (loop até verde)
```powershell
dotnet build src/GestaoSorveteria.Server
dotnet test --project tests/GestaoSorveteria.Tests
```
- Mexeu no app: `dotnet build src/GestaoSorveteria.Mobile -c Debug -p:TargetFrameworks=net10.0-android` (precisa do workload MAUI; o CI "App Android" confirma no PR).
- **Mesmo erro 3 vezes seguidas → pare**, mostre o erro e o que já tentou. Não troque de abordagem sem avisar.

## 5. Revisar
- Rode o agente `revisor-dominio` no diff. Corrija todo item **Crítico** e **Importante** e volte ao passo 4.

## 6. Documentar
- Marque o item em `docs/04-roadmap-v2.md` (`- [x]`).
- Regra nova/alterada → `docs/02`; endpoint/tabela → `docs/03`; bug do app → `docs/06`.
- Fim de sprint → atualize "Estado atual" no `CLAUDE.md`.

## 7. Commit, PR e CI
```powershell
git add -A
git commit -m "tipo(escopo): descrição (RN-xx / Bn)"
git push -u origin <branch>
gh pr create --base develop --fill
gh pr checks --watch
```
- CI vermelho → leia o log (`gh run view --log-failed`), corrija, novo commit, repita.
- CI verde e revisor sem Crítico → `gh pr merge --squash --delete-branch` (merge **só na develop**; confira `--base develop` no PR). Depois `git fetch origin` antes da próxima volta.
- Tarefa do **app** que precisa de teste manual no celular/emulador: **não faça merge**. Deixe o PR aberto com "Como testar" na descrição e avise o Paulo.

## 8. Trello (se o conector estiver disponível na sessão)
- Marque os itens do checklist do cartão, mova para "Concluido" o que terminou, crie cartão no Backlog para trabalho descoberto.
- Sem conector: liste no resumo final o que precisa ser atualizado no quadro.

## 9. Resumo e próxima volta
Relate em até 10 linhas: tarefa, PR (link), CI, o que o Paulo precisa testar/decidir. Depois:
- Se não há bloqueio e o Paulo pediu modo contínuo → volte ao passo 0 com a próxima tarefa.
- Senão, pare.

## Quando parar e chamar o Paulo (sempre)
- Regra de negócio ausente ou ambígua em `docs/02` → registre a pergunta (Trello "Dúvidas para a dona" ou no resumo) e pare essa tarefa.
- Qualquer mudança em dinheiro, pagamento ou caixa **fora** do que as regras `RN-*` já descrevem.
- Apagar dados, mexer em migração já aplicada, mudar hospedagem/segredos, mexer na `main`.
- Mesmo erro 3 vezes; CI vermelho que não é do seu código.
