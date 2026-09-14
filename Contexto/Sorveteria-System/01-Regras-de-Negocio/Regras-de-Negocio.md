# Regras de Negócio do Sistema de Gestão para Sorveteria

Este documento detalha as regras de negócio que governam o funcionamento do sistema de gestão para sorveteria, abrangendo as interações do atendente, o painel da dona e o controle de estoque, com as novas funcionalidades e ajustes.

## 1. Fluxo do Atendente (Vendas)

- **Abertura de Comanda**: O atendente deve ser capaz de abrir uma nova comanda em até 2 segundos.
- **Registro de Pedido**: Itens (picolé, milk-shake, casquinha, etc.) são selecionados com um toque. O preço do item é pré-cadastrado no sistema.
- **Pagamento**: O sistema deve registrar a forma de pagamento (dinheiro ou Pix) e calcular o troco automaticamente.
- **Fechamento de Comanda**: A venda é registrada e o estoque é atualizado automaticamente em menos de 30 segundos após o fechamento da comanda.
- **Histórico de Vendas**: O atendente pode visualizar o histórico de todas as vendas do turno.
- **Pedidos de Delivery**: O sistema deve permitir marcar pedidos como sendo de delivery.
- **Venda Self-Service**: O atendente deve poder adicionar um item de "Self-Service" à comanda, informando manualmente o valor final do item. Esta funcionalidade deve ser rápida e acessível.
- **Venda Rápida por Valor**: Em casos específicos, o atendente deve ter a opção de registrar uma venda completa informando apenas o valor total, sem a necessidade de adicionar cada item individualmente à comanda.

## 2. Painel da Dona (Gestão)

- **Visualização de Vendas**: Gráficos de vendas diárias, semanais ou mensais, com comparativos de períodos.
- **Controle de Insumos**: Lista de insumos com quantidade atual e alertas de mínimo (ex: 'Morango: 0,8kg — repor!').
- **Ranking de Produtos**: Exibição dos produtos mais vendidos.
- **Faturamento e Lucro**: Cálculo do faturamento total e margem de lucro estimada por produto.
- **Previsão de Estoque**: Previsão de ruptura de insumos baseada no histórico de vendas (ex: 'Com o ritmo atual, o leite acaba em 3 dias').
- **Relatórios**: Geração de relatórios completos exportáveis para qualquer período.

## 3. Controle de Estoque Inteligente

- **Cadastro de Insumos**: Possibilidade de cadastrar insumos com quantidade mínima configurável.
- **Baixa Automática**: A cada venda registrada, a baixa no estoque dos insumos correspondentes é automática.
- **Alertas de Estoque Baixo**: O painel da dona deve exibir alertas quando um insumo atingir seu nível mínimo.
- **Entrada de Reposição**: Registro manual da entrada de mercadorias para reposição de estoque.
- **Previsão de Ruptura**: O sistema deve calcular a previsão de quando um insumo irá acabar com base no histórico de vendas.
- **Exceção de Estoque**: Produtos como picolés e sabores de sorvete não terão controle de estoque inicial. O controle de estoque será focado em insumos.

## 4. Autenticação e Segurança

- **Login Separado**: Login individual para atendente e dona, com senhas distintas.
- **Níveis de Acesso**: A dona possui acesso completo ao sistema, enquanto o atendente tem acesso restrito às funcionalidades necessárias para o trabalho.
- **Backup de Dados**: Os dados são salvos no servidor, garantindo que nenhuma informação seja perdida mesmo em caso de falha do dispositivo do atendente.

## 5. Produtos e Preços

- **Cadastro de Produtos**: O sistema deve permitir o cadastro de produtos com seus respectivos preços e categorias.

## 6. Fases de Desenvolvimento

O desenvolvimento será dividido em fases, com entregas incrementais:

- **Fase 1 (Sistema Básico)**: App do atendente (abrir comanda, adicionar itens, fechar venda), painel da dona (visualizar vendas do dia), cadastro de produtos, login e banco de dados central.
- **Fase 2 (Estoque Inteligente)**: Cadastro de insumos, baixa automática, alertas ♥