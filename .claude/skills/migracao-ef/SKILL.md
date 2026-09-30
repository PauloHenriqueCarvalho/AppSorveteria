---
name: migracao-ef
description: Como criar, revisar e aplicar migrações do EF Core no PostgreSQL da sorveteria (local e Neon), com os comandos e caminhos certos e as verificações de snake_case, precisão de dinheiro e dados existentes. Use sempre que uma entidade ou configuração EF mudar.
---

# Migração EF Core

Projeto das migrações: `src/GestaoSorveteria.Infrastructure` (pasta `Persistence/Migrations`). Projeto de inicialização: `src/GestaoSorveteria.Server`.

## 1. Pré-requisitos
- `dotnet tool install --global dotnet-ef` (ou `update`).
- Banco local no ar: `docker compose up -d`.

## 2. Criar
```powershell
dotnet ef migrations add <NomeEmPascalCase> -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server -o Persistence/Migrations
```
Nome descreve a mudança: `InitialCreate`, `AdicionaInsumos`, `ComandaTaxaEntrega`.

## 3. Revisar o arquivo gerado (obrigatório)
- Tabelas/colunas em `snake_case` (a convenção roda no `AppDbContext`).
- Dinheiro como `numeric(12,2)`; datas `timestamp with time zone`; enums `character varying`.
- PK `uuid` **sem** default gerado pelo banco (Ids vêm do domínio).
- Coluna nova **obrigatória** em tabela com dados: precisa de `defaultValue` ou de um passo de preenchimento; senão a migração quebra no piloto.
- Nada de `DropTable`/`DropColumn` em tabela de venda, pagamento, caixa ou movimento sem conversar com o Paulo.

## 4. Aplicar
```powershell
dotnet ef database update -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server
```
Em Development a API também aplica ao subir (`Database:MigrateOnStartup = true`). Confira no DBeaver.

No piloto (Render + Neon) a migração roda na subida da API (`Database__MigrateOnStartup=true`, um serviço só — docs/07). Antes de publicar uma migração que mexe em dados: `pg_dump` do Neon.

## 5. Desfazer (só antes de subir para a develop)
```powershell
dotnet ef migrations remove -p src/GestaoSorveteria.Infrastructure -s src/GestaoSorveteria.Server
```
Migração que já está na `develop`/`main` ou aplicada no Neon **nunca** é editada: crie outra que corrige.

## 6. Commitar
Migração + snapshot + mudança de entidade/configuração no mesmo commit: `feat(infra): <mudança> (migração <Nome>)`.
