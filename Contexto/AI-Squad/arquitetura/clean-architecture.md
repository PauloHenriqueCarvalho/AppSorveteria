# Arquitetura: Clean Architecture

O projeto segue os princípios da Clean Architecture para garantir independência de frameworks, testabilidade e separação de preocupações.

## 1. Camadas do Sistema

### 1.1. Domain (Domínio)
- **Localização**: Core do sistema.
- **Conteúdo**: Entidades, interfaces de repositórios, exceções de domínio e lógica de negócio pura.
- **Regra de Ouro**: Não depende de nenhuma outra camada.

### 1.2. Application (Aplicação)
- **Conteúdo**: Casos de uso (Use Cases), DTOs (Data Transfer Objects), Mappers e validações de fluxo.
- **Dependência**: Depende apenas da camada de Domínio.

### 1.3. Infrastructure (Infraestrutura)
- **Conteúdo**: Implementações de repositórios (Entity Framework Core com PostgreSQL/SQLite), serviços externos, integrações de API e persistência de dados.
- **Dependência**: Depende de Domínio e Aplicação.

### 1.4. Presentation (Apresentação)
- **Conteúdo**: Web API (.NET Core), Interfaces de Usuário (.NET MAUI).
- **Dependência**: Depende de Aplicação e Infraestrutura (para Injeção de Dependência).

## 2. Estratégia de Dados e Sincronização

### 2.1. Persistência Híbrida
- **Dispositivo Mobile**: Utiliza SQLite para garantir que o atendente possa registrar vendas mesmo sem conectividade.
- **Servidor Central**: Utiliza PostgreSQL como fonte da verdade e para consolidação de relatórios da proprietária.

### 2.2. Lógica de Sincronização
1. O aplicativo registra a venda no SQLite local.
2. Um serviço de background verifica a conectividade.
3. Ao detectar internet, as comandas com status `PendenteSincronizacao` são enviadas para a API.
4. O Backend processa a comanda, atualiza o PostgreSQL e retorna a confirmação para o app atualizar o status local.
