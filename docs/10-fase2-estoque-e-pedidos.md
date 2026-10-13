# 10 — Fase 2 redesenhada: estoque de produto pronto e pedido ao fornecedor

> 13/10/2026. Base: a planilha que a dona usa hoje (`pedidos.xlsx`, compartilhada em 10/10) e a reunião em que ela contou a dor principal: **o fornecedor nem sempre manda o que foi pedido, e ela só descobre depois.**
> Substitui o desenho antigo da Fase 2 (insumos + ficha técnica). Insumo e ficha técnica vão para a Fase 4, se ela ainda quiser. Decisão registrada no ADR 019 (docs/01).

## 1. O que a planilha mostra

A dona não controla insumo (leite, morango). Ela controla **produto pronto comprado de um fornecedor**:

| Aba | O que tem | Unidade de compra | Custo na planilha |
|---|---|---|---|
| PICOLÉS | 16 sabores de fruta + 26 ao leite | caixa (contada em meias: 0,5 · 1,5 · 2,5) | R$ 25,20 a caixa (28 × R$ 0,90) |
| SKIMO | Skimo (4 sabores) e Moreninha | caixa de 24? / unidade? | R$ 2,50 × 24; R$ 4,00 |
| POTES | 1L (29 sabores), 2L (3), 5L (3), potes de açaí 400 ml / 1L / 1,5L | unidade | R$ 9 · 18 · 40 · 8 · 18 · 18 |
| AÇAÍS | açaí tradicional e 7 trufados | balde | R$ 160 e R$ 120 |
| MASSAS | 30 sabores de sorvete de massa | balde | R$ 90 |
| TOTAL | quantidade pedida por grupo × preço = total do pedido | — | — |
| Pedido | resumo do pedido para enviar, com coluna **Enviado** | — | — |

Cada aba tem as colunas **Na frente** (freezer do balcão), **Em estoque** (depósito) e **Pedido dd/mm**.

**Onde o processo quebra (com evidência na planilha):**

1. **A coluna "Enviado" está vazia.** Ninguém confere o que chegou contra o que foi pedido — é exatamente a dor que ela descreveu.
2. **O pedido é digitado duas vezes e as duas versões não batem.** Na coluna "Pedido 15/06" das abas há 9 caixas de picolé e nenhum pote; na aba "Pedido" há 25 caixas de picolé, 9 sabores de pote 1L (8 de cada) e 6 baldes de massa.
3. **"Em estoque" quase nunca é preenchido** (só 2 linhas, em MASSAS). Ela conta a frente e "lembra" do depósito.
4. **A decisão de quanto pedir está na cabeça dela**: não há mínimo nem alvo por sabor.
5. **Preço escondido em fórmula** (`=B3*0.9*28`) e só por grupo: mudou o preço, alguém precisa lembrar de editar a fórmula.
6. **Erro de fórmula**: `AÇAÍS!D5` = `=D` (dá `#NAME?`).
7. **Contagem em fração digitada** ("0,5", "1,5" caixa) e sabores genéricos ("Outros" nos potes de 2L e 5L).

O catálogo inicial já foi extraído da planilha: **[docs/fase2/catalogo-inicial.csv](fase2/catalogo-inicial.csv)** (123 itens, com categoria, sabor, embalagem, custo e a célula de origem). Ninguém precisa redigitar nada.

## 2. Objetivo: o pedido se faz sozinho

Hoje: contar tudo → decidir de cabeça → digitar na planilha → digitar de novo no resumo → mandar → receber sem conferir.

Com o sistema, o trabalho manual de cada etapa cai para o mínimo:

| Etapa | Hoje (manual) | Com o sistema | Toques da dona |
|---|---|---|---|
| Saber o que tem | Contar freezer e depósito, item por item | **Saldo automático**: entra pelo recebimento, sai pela venda e pela abertura de balde | 0 |
| Decidir o que pedir | De cabeça | **Pedido sugerido** pelo consumo real e pelo prazo de entrega | revisar |
| Montar o pedido | Digitar duas vezes | Rascunho pronto no dia do pedido, total em R$ calculado | +/− se quiser |
| Enviar | Copiar e colar | Botão **Enviar no WhatsApp** com a mensagem pronta | 1 |
| Receber | Não confere | Lista do pedido já aberta: **"Chegou tudo certo"** ou ajusta só o que veio diferente | 1 (+ só as diferenças) |
| Cobrar o fornecedor | Não cobra (não sabe) | Diferenças em R$ e mensagem de cobrança pronta; o que faltou volta sozinho no próximo pedido | 1 |
| Conferir o estoque | — | **Contagem rápida** semanal opcional: só digita o que não bate | poucos |

### 2.1 De onde vem a baixa automática (sem ninguém contar)

| Grupo | Como sai do estoque | O que o atendente faz a mais |
|---|---|---|
| Picolé, Skimo, Moreninha | Venda no app | Escolhe o **sabor** ao lançar (1 toque numa grade de sabores) |
| Potes 1L/2L/5L e potes de açaí | Venda no app | Escolhe o **sabor** ao lançar |
| Massa e açaí em balde | Quando o balde **acaba e outro é aberto** (bola e peso não dão para medir) | Toca **"Abri um balde"** e escolhe o sabor |

Sem a escolha de sabor, o sistema só saberia "vendeu 40 picolés" e não teria como sugerir o pedido por sabor. É o único passo novo para o atendente e substitui toda a contagem manual.

Enquanto o histórico de vendas por sabor é curto (menos de 14 dias), a sugestão usa um **estoque alvo** por item, que a dona define uma vez (ex.: "Pote 1L Morango: manter 8"). Depois o consumo real assume.

### 2.2 Conta da sugestão (RN-PF-02)

```
consumo diário   = saídas (venda + abertura de balde) dos últimos 28 dias ÷ dias com loja aberta
cobertura (dias) = prazo de entrega do fornecedor + dias até o próximo pedido (padrão: 7)
necessário       = consumo diário × cobertura
sugerido         = necessário − saldo − já pedido e ainda não recebido
                   arredondado para cima em embalagens (meia caixa se o fornecedor aceitar)
sem histórico    → sugerido = estoque alvo − saldo
faltou no último recebimento → entra marcado "faltou no último pedido"
```

Exemplo: Pote 1L Morango vende 1,2 por dia; o fornecedor entrega em 2 dias e o próximo pedido é daqui a 7. Necessário = 1,2 × 9 ≈ 10,8. Saldo 4 → sugere **7 potes**.

## 3. Fluxo da dona (no painel, pelo celular)

1. **Dia do pedido** (ex.: segunda): o painel mostra "**Pedido sugerido pronto — R$ 1.284,00**". Também aparece um aviso "pedir hoje" se algum sabor vai acabar antes da próxima entrega.
2. Ela abre, vê a lista agrupada como na planilha (Picolés de fruta, ao leite, Potes 1L…) e ajusta com **+/−** o que quiser.
3. **Confirmar e enviar**: abre o WhatsApp com a mensagem pronta para o fornecedor. O pedido fica "Enviado".
4. **Chegou a entrega**: abre o pedido → "Chegou tudo certo" ou corrige só as linhas diferentes (e "veio sem pedir" para sabor trocado).
5. O sistema mostra **Faltou / Veio a mais / Sabor trocado** com o valor em R$, gera a mensagem de cobrança e põe o que faltou no próximo pedido. O estoque entra sozinho.
6. **Fornecedor**: % do pedido entregue por mês e os itens que mais faltam — argumento para negociar.

## 4. O que muda nas outras partes

- **App do atendente**: grade de sabores ao lançar picolé/pote; botão "Abri um balde"; (opcional) contagem rápida. O resto não muda. A venda continua offline; a baixa acontece no servidor quando a comanda sincroniza.
- **Produtos de venda** (`produtos`): ganham o vínculo com o grupo de estoque (ex.: produto "Picolé ao leite" → grupo *Picolé ao leite*, 1 unidade por venda). Assim o preço de venda continua um só por produto, e o sabor vem do estoque.
- **Fase 3 fica melhor**: o custo de cada venda passa a ser conhecido (custo da embalagem ÷ unidades), então margem por produto e por sabor sai sem ficha técnica (RN-PR-07 revisada).
- **Proposta (docs/05)**: a frase "baixa automática dos insumos" vira "controle de estoque dos sorvetes comprados prontos, pedido sugerido e conferência da entrega".

## 5. Dúvidas para confirmar com a dona

As regras abaixo já têm um padrão para não travar o desenvolvimento; a resposta só ajusta valores.

| # | Pergunta | Padrão adotado |
|---|---|---|
| 1 | É um fornecedor só? Qual o WhatsApp? Em que dia pede e em quantos dias chega? | Um fornecedor; pedido semanal; entrega em 2 dias |
| 2 | O fornecedor aceita **meia caixa** de picolé? | Não: pedido em caixas inteiras (a contagem continua em meias) |
| 3 | Skimo vem em caixa de 24? Moreninha é por unidade? Pote de açaí 1,5L custa R$ 18 como o de 1L? | Como está na planilha; corrigir no catálogo |
| 4 | O que são os "Outros" dos potes de 2L e 5L? | Cadastrar os sabores reais |
| 5 | O que o fornecedor mais erra: quantidade, sabor trocado, item que não vem? | Tratar os três |
| 6 | Quem recebe a entrega: ela ou o atendente? | Ela, pelo painel; atendente fica para depois |
| 7 | Topa o atendente escolher o sabor ao vender picolé e pote? | Sim (sem isso não há baixa automática) |
| 8 | Precisa saber o que está **na frente** × **no depósito**, ou só o total? | Só o total (a reposição da frente fica com ela) |

## 6. Ordem de entrega

1. **Sprint 5 — Catálogo e estoque**: importar o catálogo da planilha, fornecedor, saldo com movimentos, contagem inicial pelo painel. *Já substitui as abas de contagem.*
2. **Sprint 6 — Pedido e recebimento**: pedido sugerido pelo estoque alvo, mensagem de WhatsApp, recebimento com diferenças e cobrança, histórico do fornecedor. *Já resolve a dor principal, mesmo antes da baixa automática.*
3. **Sprint 7 — Baixa automática**: sabor na venda, "Abri um balde", baixa e estorno no servidor, sugestão por consumo real, aviso "pedir hoje", contagem rápida.

Regras: docs/02 seção 6. Tabelas e endpoints: docs/03 seção 5. Tarefas: docs/04, Sprints 5–7.
