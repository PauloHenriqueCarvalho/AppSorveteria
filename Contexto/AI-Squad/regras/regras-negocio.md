# Regras: Lógica de Negócio e Fluxos

Este documento define as regras fundamentais que devem ser respeitadas pela implementação dos Casos de Uso.

## 1. Regras de Venda e Comanda

### 1.1. Resiliência Offline
- O sistema **DEVE** permitir a abertura e fechamento de comandas sem conexão com a internet.
- Os dados **DEVEM** ser persistidos no SQLite local e marcados para sincronização.

### 1.2. Self-Service e Venda Rápida
- O atendente **PODE** adicionar um item de "Self-Service" informando apenas o valor.
- O sistema **DEVE** permitir o fechamento de uma venda informando apenas o valor total final, ignorando a lista de itens detalhada se necessário.

### 1.3. Cálculo de Troco
- O sistema **DEVE** calcular o troco automaticamente ao informar o valor recebido em dinheiro.

## 2. Regras de Estoque

### 2.1. Controle de Insumos
- A baixa de estoque de insumos **DEVE** ocorrer automaticamente após a sincronização da venda com o servidor central.
- Alertas de estoque baixo **DEVEM** ser gerados quando `QuantidadeAtual <= QuantidadeMinima`.

### 2.2. Isenção de Estoque
- Produtos finais (Picolés, Sabores) **NÃO DEVEM** disparar erros de "falta de estoque" no sistema inicial, pois seu controle é apenas informativo ou inexistente nesta fase.

## 3. Regras de Acesso
- Apenas usuários com perfil **Dona** podem visualizar relatórios de lucro e editar preços de produtos.
- Atendentes podem apenas registrar vendas e visualizar o histórico do próprio turno.
