# Entidades: Modelos de Domínio

Definição das entidades principais que compõem o núcleo do sistema da sorveteria.

## 1. Entidades Principais

### 1.1. Produto
Representa os itens vendidos (ex: Milk-shake, Casquinha, Picolé).
- **Atributos**: Id, Nome, Preço, Categoria, Ativo.
- **Nota**: Picolés e Sabores de Sorvete não possuem controle de estoque vinculado inicialmente.

### 1.2. Insumo
Representa os materiais base para produção (ex: Morango, Leite, Calda).
- **Atributos**: Id, Nome, UnidadeMedida, QuantidadeAtual, QuantidadeMinima.

### 1.3. Comanda
Representa um pedido em aberto ou finalizado.
- **Atributos**: Id, IdAtendente, DataAbertura, DataFechamento, Status (Aberta, Fechada, Sincronizada), TipoPedido (Balcão, Delivery, Self-Service, VendaRápida), ValorTotal.

### 1.4. ItemComanda
Representa um item específico dentro de uma comanda.
- **Atributos**: Id, IdComanda, IdProduto (opcional), DescricaoManual (para Self-Service), Quantidade, PrecoUnitario, PrecoTotal.

### 1.5. Venda
Registro financeiro da comanda finalizada.
- **Atributos**: Id, IdComanda, DataVenda, ValorTotal, FormaPagamento, Troco.

### 1.6. Usuario
Representa os operadores do sistema.
- **Atributos**: Id, Nome, Email, SenhaHash, Perfil (Atendente, Dona).

## 2. Relacionamentos de Domínio
- Uma **Comanda** possui uma lista de **ItensComanda**.
- Um **ItemComanda** pode estar vinculado a um **Produto** ou ser um item de valor manual.
- Uma **Venda** é gerada a partir de uma **Comanda** fechada.
- Movimentações de **Insumo** são registradas para controle de estoque.
