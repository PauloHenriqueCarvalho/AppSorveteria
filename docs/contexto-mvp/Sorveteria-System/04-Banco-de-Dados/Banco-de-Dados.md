# Banco de Dados do Sistema de Gestão para Sorveteria

Este documento descreve as tecnologias e as principais entidades do banco de dados para o sistema de gestão da sorveteria, com as atualizações para PostgreSQL e a estratégia de SQLite para resiliência offline.

## 1. Tecnologias

- **Sistema Gerenciador de Banco de Dados (SGBD) Principal**: PostgreSQL
    - **Características**: O PostgreSQL é um SGBD de código aberto, robusto, escalável e com uma vasta gama de funcionalidades, ideal para o armazenamento centralizado dos dados do sistema.
- **Banco de Dados Local (App Atendente)**: SQLite
    - **Características**: O SQLite é um banco de dados leve e embarcado, perfeito para armazenar dados localmente no dispositivo do atendente, garantindo o funcionamento do aplicativo mesmo sem conexão com a internet. As comandas registradas no SQLite serão sincronizadas posteriormente com o PostgreSQL.

## 2. Principais Entidades (Tabelas)

Com base nas funcionalidades do sistema e na mudança para PostgreSQL, as seguintes entidades são propostas para o banco de dados central:

### 2.1. Usuários

- **Propósito**: Armazenar informações dos usuários do sistema (atendentes e dona).
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `nome` (VARCHAR(255))
    - `email` (VARCHAR(255), UNIQUE)
    - `senhaHash` (VARCHAR(255))
    - `tipoUsuario` (VARCHAR(50) - ex: 'Atendente', 'Dona')
    - `dataCriacao` (TIMESTAMP DEFAULT CURRENT_TIMESTAMP)
    - `ativo` (BOOLEAN DEFAULT TRUE)

### 2.2. Produtos

- **Propósito**: Armazenar os itens que a sorveteria vende.
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `nome` (VARCHAR(255))
    - `descricao` (TEXT, opcional)
    - `preco` (NUMERIC(10, 2))
    - `categoria` (VARCHAR(100) - ex: 'Picolé', 'Milk-shake', 'Casquinha', 'Self-Service')
    - `ativo` (BOOLEAN DEFAULT TRUE)
    - **Observação**: Produtos como picolés e sabores de sorvete não terão controle de estoque inicial.

### 2.3. Insumos

- **Propósito**: Armazenar os ingredientes utilizados na produção dos produtos e controlar o estoque.
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `nome` (VARCHAR(255))
    - `unidadeMedida` (VARCHAR(50) - ex: 'kg', 'litro', 'unidade')
    - `quantidadeAtual` (NUMERIC(10, 2))
    - `quantidadeMinima` (NUMERIC(10, 2))
    - `ativo` (BOOLEAN DEFAULT TRUE)

### 2.4. Comandas

- **Propósito**: Registrar os pedidos dos clientes antes do fechamento da venda.
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `idAtendente` (FK para Usuários, INT)
    - `dataAbertura` (TIMESTAMP DEFAULT CURRENT_TIMESTAMP)
    - `dataFechamento` (TIMESTAMP, opcional)
    - `status` (VARCHAR(50) - ex: 'Aberta', 'Fechada', 'Cancelada', 'PendenteSincronizacao')
    - `tipoPedido` (VARCHAR(50) - ex: 'Balcão', 'Delivery', 'Self-Service', 'VendaRápida')
    - `valorTotal` (NUMERIC(10, 2))
    - `idComandaLocal` (VARCHAR(255), UNIQUE, opcional - para rastrear a comanda original do SQLite)

### 2.5. ItensComanda

- **Propósito**: Detalhar os produtos contidos em cada comanda.
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `idComanda` (FK para Comandas, INT)
    - `idProduto` (FK para Produtos, INT, opcional - pode ser nulo para venda rápida ou self-service com valor manual)
    - `descricaoItem` (TEXT, opcional - para itens de self-service ou venda rápida sem produto específico)
    - `quantidade` (NUMERIC(10, 2))
    - `precoUnitario` (NUMERIC(10, 2))
    - `precoTotal` (NUMERIC(10, 2))

### 2.6. Vendas

- **Propósito**: Registrar as vendas finalizadas.
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `idComanda` (FK para Comandas, INT, UNIQUE)
    - `dataVenda` (TIMESTAMP DEFAULT CURRENT_TIMESTAMP)
    - `valorTotal` (NUMERIC(10, 2))
    - `formaPagamento` (VARCHAR(50) - ex: 'Dinheiro', 'Pix', 'Cartão')
    - `troco` (NUMERIC(10, 2), opcional)

### 2.7. MovimentacaoEstoque

- **Propósito**: Registrar todas as entradas e saídas de insumos do estoque.
- **Campos Sugeridos**:
    - `id` (PK, SERIAL)
    - `idInsumo` (FK para Insumos, INT)
    - `tipoMovimentacao` (VARCHAR(50) - ex: 'Entrada', 'Saída')
    - `quantidade` (NUMERIC(10, 2))
    - `dataMovimentacao` (TIMESTAMP DEFAULT CURRENT_TIMESTAMP)
    - `observacao` (TEXT, opcional)

## 3. Relacionamentos

- Um `Usuário` pode ter várias `Comandas` (Atendente).
- Uma `Comanda` pode ter vários `ItensComanda`.
- Um `ItemComanda` pode referir-se a um `Produto` (opcional).
- Uma `Comanda` pode gerar uma `Venda`.
- Um `Insumo` pode ter várias `MovimentacoesEstoque`.
- Um `Produto` pode estar associado a um ou mais `Insumos` (para baixa automática, pode ser uma tabela de relacionamento `ProdutoInsumo`).

## 4. Considerações Adicionais

- **Normalização**: As tabelas serão projetadas seguindo princípios de normalização para evitar redundância e garantir a integridade dos dados.
- **Índices**: Serão criados índices apropriados para otimizar o desempenho das consultas.
- **Backup**: O sistema de backup do PostgreSQL será configurado para garantir a recuperação dos dados em caso de falha.
- **Sincronização SQLite**: O aplicativo do atendente manterá uma cópia local das comandas e produtos no SQLite. As comandas serão marcadas com um status de sincronização (`PendenteSincronizacao`) e enviadas ao backend quando a conexão for restabelecida. O backend será responsável por processar essas comandas e atualizar o estado no PostgreSQL.
