
### 1. O MVP (Mínimo Produto Viável) - Fase de Validação

Antes de escrever qualquer código complexo de banco de dados, desenhe as telas.

- **Ação:** Crie protótipos de baixa fidelidade (pode ser no Figma, Penpot, ou até no papel) das telas de "Venda Rápida" e "Self-Service".
    
- **Validação:** Mostre para a dona e para os atendentes. Simulem o tempo. A tela precisa ter botões grandes, poucas transições e ser intuitiva ao máximo. Se eles aprovarem a usabilidade, você economiza semanas de refatoração no .NET MAUI.
    

### 2. Fase 1: O Coração da Operação (Mobile Offline)

Foque apenas em fazer o aplicativo funcionar isoladamente no celular ou tablet do balcão.

- Crie o app .NET MAUI (Android) com o banco SQLite local.
    
- Implemente o catálogo de produtos fixos no banco local.
    
- Implemente o fluxo de abrir comanda, adicionar itens (ou apenas valor total de self-service) e calcular o troco em dinheiro.
    
- _Dica de ouro:_ Crie uma tabela `Vendas` e uma coluna `Sincronizado = false`. Por enquanto, não faça o backend, apenas garanta que as vendas estão sendo salvas no SQLite corretamente.
    

### 3. Fase 2: A Ponte (Backend + Sincronização)

Agora que o app vende perfeitamente offline, vamos conectar os pontos.

- Crie a API em .NET (ASP.NET Core) e o banco central PostgreSQL.
    
- Crie o _job_ de sincronização no app mobile: ele vai buscar todas as vendas com `Sincronizado = false` e enviar para a API. Se a API responder "OK", ele atualiza para `true`.
    
- _Dica de ouro:_ Como as vendas são registros que só se somam (append-only), você dificilmente terá conflitos de concorrência graves nessa etapa.
    

### 4. Fase 3: A Visão da Gestão (Painel Desktop)

Com os dados chegando no PostgreSQL, crie o aplicativo para a proprietária.

- Desenvolva o frontend Desktop (.NET MAUI ou WPF).
    
- Implemente a autenticação para garantir o perfil "Dona".
    
- Crie a dashboard com os gráficos de vendas, lucros e ranking de produtos.
    

### 5. Fase 4: O Refinamento (Estoque de Insumos)

Deixe isso para o final, pois é a regra de negócio mais complexa.

- Implemente a lógica onde a API, ao receber uma venda sincronizada, dá baixa automática nos insumos relacionados e gera os alertas de reposição.