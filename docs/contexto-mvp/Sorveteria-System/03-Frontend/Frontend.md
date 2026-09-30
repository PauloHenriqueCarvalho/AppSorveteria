# Frontend do Sistema de Gestão para Sorveteria

Este documento detalha as interfaces de usuário e as tecnologias empregadas para os aplicativos frontend do sistema de gestão da sorveteria, incorporando as novas funcionalidades e a lógica de sincronização offline.

## 1. Aplicativo do Atendente

- **Plataforma**: Celular Android
- **Tecnologia**: .NET MAUI (Multi-platform App UI) – uma tecnologia da Microsoft que permite o desenvolvimento de aplicativos nativos para diversas plataformas a partir de uma única base de código C#.
- **Armazenamento Local**: SQLite será utilizado para armazenar comandas e dados essenciais localmente, permitindo que o aplicativo funcione offline.
- **Sincronização Offline**: As comandas registradas offline serão marcadas e sincronizadas automaticamente com o backend (PostgreSQL) assim que a conexão com a internet for restabelecida. Isso garante que a operação de vendas não seja interrompida por problemas de rede.
- **Características**: O aplicativo funcionará offline e sincronizará os dados quando houver internet.
- **Funcionalidades Principais**:
    - Abertura e fechamento rápido de comandas.
    - Registro de itens vendidos com um toque.
    - Cálculo automático de troco.
    - Marcação de pedidos de delivery.
    - Visualização do histórico de vendas do turno.
    - **Venda Self-Service**: Interface para adicionar um item de "Self-Service" e informar manualmente o valor.
    - **Venda Rápida por Valor**: Opção para registrar uma venda completa informando apenas o valor total.

## 2. Painel da Dona

- **Plataforma**: Computador (Windows)
- **Tecnologia**: Aplicativo Windows (desktop application). Será instalado e executado como qualquer outro programa no computador.
- **Funcionalidades Principais**:
    - Controle completo do estoque.
    - Alertas de insumos próximos do fim.
    - Previsão automática de reposição.
    - Gráficos de vendas e lucro por dia, semana ou mês.
    - Relatórios por período ou produto.
    - Comparativo de períodos.
    - Ranking dos produtos mais vendidos.
    - Faturamento total e margem de lucro estimada por produto.

## 3. Experiência do Usuário

Ambos os aplicativos são projetados para serem intuitivos e eficientes, garantindo uma experiência de usuário fluida para o atendente no balcão e para a dona no acompanhamento da gestão da sorveteria. A resiliência offline do aplicativo do atendente é um ponto chave para a continuidade do negócio.
