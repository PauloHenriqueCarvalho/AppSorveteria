# Prompts: Instruções para o Claude Code

Use estes prompts para orientar a geração de código seguindo os padrões do projeto.

## 1. Prompt para Criação de Entidades (Domain)
"Crie a entidade de domínio [NOME_ENTIDADE] seguindo os princípios da Clean Architecture. A entidade deve estar na camada Domain, ser independente de frameworks e conter as propriedades: [LISTA_PROPRIEDADES]. Inclua validações básicas de domínio."

## 2. Prompt para Caso de Uso (Application)
"Implemente o Caso de Uso para [ACAO] na camada Application. Utilize o padrão Command/Query se aplicável. Garanta que a lógica respeite as regras definidas em `regras/regras-negocio.md`. O caso de uso deve depender apenas de interfaces de repositório do domínio."

## 3. Prompt para Repositório (Infrastructure)
"Crie a implementação do repositório para [NOME_ENTIDADE] na camada Infrastructure utilizando Entity Framework Core. Configure o mapeamento para PostgreSQL. Garanta que a implementação respeite a interface definida no Domain."

## 4. Prompt para Sincronização SQLite
"Desenvolva a lógica de sincronização para o banco SQLite local no .NET MAUI. O fluxo deve identificar registros com status `PendenteSincronizacao`, enviá-los para o endpoint da API e atualizar o status local após a confirmação de sucesso."

## 5. Prompt para UI de Venda Rápida
"Crie a tela de venda rápida no .NET MAUI. A interface deve permitir a inserção de um valor numérico de forma proeminente e botões rápidos para fechar a venda (Dinheiro/Pix). Foque na velocidade de operação para o atendente."
