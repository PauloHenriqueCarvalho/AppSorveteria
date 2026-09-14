# Backend do Sistema de Gestão para Sorveteria

Este documento descreve a arquitetura e as tecnologias propostas para o backend do sistema de gestão da sorveteria, com as atualizações para PostgreSQL e a estratégia de sincronização com SQLite.

## 1. Tecnologias

O backend será desenvolvido utilizando o ecossistema **.NET**, alinhado com a escolha de .NET MAUI para o aplicativo do atendente.

- **Linguagem**: C#
- **Framework**: .NET (ASP.NET Core para a API e lógica de negócio)
- **Banco de Dados Principal**: PostgreSQL (para persistência de dados centralizada)
- **Banco de Dados Local (App Atendente)**: SQLite (para armazenamento temporário de comandas e resiliência offline)

## 2. Funcionalidades do Backend

O backend será responsável por gerenciar a lógica de negócio e a persistência dos dados no PostgreSQL, além de coordenar a sincronização com os bancos de dados SQLite locais.

- **Autenticação e Autorização**: Gerenciamento de usuários (atendente e dona) e seus respectivos níveis de acesso.
- **Gestão de Produtos**: CRUD (Create, Read, Update, Delete) de produtos, incluindo preços e categorias.
- **Gestão de Comandas e Vendas**: Processamento de abertura, registro de itens, fechamento de comandas e registro de vendas no banco de dados central.
- **Controle de Estoque**: Gerenciamento de insumos, baixa automática, alertas de estoque mínimo e registro de reposições. **Produtos como picolés e sabores de sorvete não terão controle de estoque inicial.**
- **Geração de Relatórios**: Processamento de dados para geração de gráficos de vendas, ranking de produtos, faturamento e margem de lucro.
- **Sincronização de Dados**: Implementação de mecanismos para sincronizar as comandas registradas localmente no SQLite do aplicativo do atendente com o banco de dados PostgreSQL central, garantindo a consistência e atualização em tempo real dos dados.
- **Backup de Dados**: Implementação de rotinas de backup para garantir a segurança e recuperação dos dados no PostgreSQL.
- **Venda Self-Service**: Suporte à funcionalidade de registro de itens de self-service com valor manual.
- **Venda Rápida por Valor**: Suporte à funcionalidade de registro de vendas completas apenas com o valor total.

## 3. Integração

O backend atuará como um serviço centralizado, expondo APIs para que o aplicativo do atendente (.NET MAUI) e o painel da dona (aplicativo Windows) possam interagir e trocar informações de forma segura e eficiente. A comunicação incluirá a lógica de sincronização para dados offline do atendente.
