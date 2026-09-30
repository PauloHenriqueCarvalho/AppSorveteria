# Contexto Global: Sistema de Gestão para Sorveteria

Este documento fornece a visão macro do projeto, servindo como ponto de partida para qualquer agente de IA entender o propósito e os objetivos do sistema.

## 1. Visão Geral

O projeto consiste em um sistema de gestão sob medida para uma sorveteria, focado em resolver problemas de pedidos manuais, controle de estoque ineficaz e falta de análise de resultados. O sistema é composto por um aplicativo móvel para o atendente e um painel desktop para a proprietária.

## 2. Objetivos Principais

- **Agilidade Extrema no Atendimento:** Registro de pedidos em menos de 10 segundos. Para maximizar a velocidade, o app utiliza o banco local por padrão.
    
- **Arquitetura de Sincronização Inteligente:**
    
    - **Operação Diária:** Todas as requisições e registros de vendas são feitos diretamente no **SQLite local** durante o expediente. Isso elimina latências de rede e garante que o sistema nunca trave por instabilidade de sinal.
        
    - **Sincronização Agendada:** Os dados acumulados localmente são sincronizados com a API (PostgreSQL) em horários pré-definidos (ex: fechamento do turno ou horários de baixo movimento), otimizando o tráfego de dados.
        
- **Gestão de Estoque Inteligente:** Foco em insumos, com alertas de reposição e previsões baseadas no histórico de vendas.
    
- **Análise de Performance:** Gráficos de vendas, lucros e ranking de produtos no painel administrativo.
    

## 3. Tecnologias Core

- **Backend:** .NET (ASP.NET Core) seguindo Clean Architecture.
    
- **Frontend Mobile:** .NET MAUI (Android).
    
- **Frontend Desktop:** .NET MAUI ou WPF (Windows).
    
- **Banco de Dados:** **PostgreSQL** (Servidor Central) e **SQLite** (Cache Local de Alta Performance).
    

## 4. Diferenciais Estratégicos

- **Venda Self-Service:** Adição manual de valor de forma rápida.
    
- **Venda Rápida por Valor:** Registro de venda total sem detalhamento de itens.
    
- **Sem Estoque Inicial para Produtos Finais:** Picolés e sabores de sorvete não terão controle de estoque inicialmente, apenas os insumos base.