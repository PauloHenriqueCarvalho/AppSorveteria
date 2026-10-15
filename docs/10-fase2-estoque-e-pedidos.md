# 10 — Fase 2 redesenhada: estoque de produto pronto e pedido ao fornecedor

> 13/10/2026, revisto em 14/10 com o Paulo. Base: a planilha que a dona usa hoje (`pedidos.xlsx`, compartilhada em 10/10) e a reunião em que ela contou a dor principal: **o fornecedor nem sempre manda o que foi pedido, e ela só descobre depois.**
> Substitui o desenho antigo da Fase 2 (insumos + ficha técnica). Insumo e ficha técnica vão para a Fase 4, se ela ainda quiser. Decisão registrada no ADR 019 (docs/01).

## 1. O que a planilha mostra

A dona não controla insumo (leite, morango). Ela controla **sorvete pronto** que compra de quem fabrica, **os pais dela**, que preferem receber o pedido **impresso**:

| Aba | O que tem | Unidade de compra | Custo na planilha |
|---|---|---|---|
| PICOLÉS | 16 sabores de fruta + 26 ao leite | caixa (contada em meias: 0,5 · 1,5 · 2,5) | R$ 25,20 a caixa (28 × R$ 0,90) |
| SKIMO | Skimo (4 sabores) e Moreninha | caixa de 24? / unidade? | R$ 2,50 × 24; R$ 4,00 |
| POTES | 1L (29 sabores), 2L (3), 5L (3), potes de açaí 400 ml / 1L / 1,5L | unidade | R$ 9 · 18 · 40 · 8 · 18 · 18 |
| AÇAÍS | açaí tradicional e 7 trufados | balde | R$ 160 e R$ 120 |
| MASSAS | 30 sabores de sorvete de massa | balde | R$ 90 |
| TOTAL | quantidade pedida por grupo × preço = total do pedido | — | — |
| Pedido | resumo do pedido para enviar, com coluna **Enviado** | — | — |

Cada aba tem as colunas **Na frente**, **Em estoque** e **Pedido dd/mm**. Na prática a loja costuma ter **um freezer só**, sem estoque guardado atrás: o que existe é o que está no freezer.

**Onde o processo quebra (com evidência na planilha):**

1. **A coluna "Enviado" está vazia.** Ninguém confere o que chegou contra o que foi pedido — é exatamente a dor que ela descreveu.
2. **O pedido é digitado duas vezes e as duas versões não batem.** Na coluna "Pedido 15/06" das abas há 9 caixas de picolé e nenhum pote; na aba "Pedido" há 25 caixas de picolé, 9 sabores de pote 1L (8 de cada) e 6 baldes de massa.
3. **"Em estoque" quase nunca é preenchido** (só 2 linhas, em MASSAS): quase tudo está no próprio freezer.
4. **A decisão de quanto pedir está na cabeça dela**: não há mínimo nem alvo por sabor.
5. **Preço escondido em fórmula** (`=B3*0.9*28`) e só por grupo: mudou o preço, alguém precisa lembrar de editar a fórmula.
6. **Erro de fórmula**: `AÇAÍS!D5` = `=D` (dá `#NAME?`).
7. **Contagem em fração digitada** ("0,5", "1,5" caixa) e sabores genéricos ("Outros" nos potes de 2L e 5L).

O catálogo inicial já foi extraído da planilha: **[docs/fase2/catalogo-inicial.csv](fase2/catalogo-inicial.csv)** (123 itens, com categoria, sabor, embalagem, custo e a célula de origem). Ninguém precisa redigitar nada.

## 2. O fluxo combinado (14/10)

Princípio: **a venda no balcão não pode ficar mais lenta.** Uma pessoa leva 50 picolés de sabores misturados e o atendente não tem como registrar sabor por sabor. Então cada grupo de produto tem o controle que cabe na rotina da loja:

| Grupo | Na venda | Como o sistema sabe o que tem | Alerta |
|---|---|---|---|
| **Picolé** (fruta, ao leite), Skimo, Moreninha | **Sem sabor**. Quantidade pode ser digitada ("× 50") | **Contagem semanal** no tablet ou no painel | Pelo botão **"Está acabando"** e pela contagem |
| **Massa e açaí** (balde) | Bola/peso, sem sabor (como hoje) | **Contagem semanal**, junto com os picolés | Pelo botão **"Está acabando"** |
| **Potes** 1L / 2L / 5L e potes de açaí | **Com sabor** (1 toque; vende pouco por venda). Teste: se atrapalhar, volta para a contagem | **Saldo automático**: entra na entrega, sai na venda. A contagem semanal confere | **Automático**: chegou no mínimo → resumo do dia; zerou → urgente |
| **Copos, casquinhas, colheres, coberturas e outros insumos** | — | Não controla saldo | Pelo botão **"Está acabando"**, cada um com o seu fornecedor |

### 2.1 Contagem semanal (picolé, massa, açaí e conferência dos potes)

- Feita pelo **atendente ou pela dona**, no tablet ou no painel, **no dia anterior ao dia do pedido** (que a dona escolhe). Na véspera o app mostra "Hoje é dia de contagem".
- A tela segue a **ordem da planilha** (Picolés de fruta, ao leite, Skimo, Massas, Açaí, Potes), com **+/−** em meias caixas para picolé, baldes inteiros para massa e açaí, unidades para potes.
- A contagem anterior aparece só como referência ("semana passada: 2,5"). O campo não vem preenchido, para a pessoa olhar o freezer de verdade.
- **"Não tem nenhum"** em um toque. Pode salvar no meio e continuar depois.
- Nos **potes**, a contagem é conferência: o sistema mostra o saldo esperado e, se a diferença passar de 1, avisa a dona ("Pote 1L Flocos: sistema 6, contado 3"). Se as diferenças forem grandes por duas semanas, o teste de sabor na venda falhou e os potes voltam só para a contagem.

**Por que basta para o pedido:** com duas contagens e a entrega do meio, o sistema sabe quanto saiu de cada sabor na semana:

```
saiu na semana = contagem anterior + recebido na entrega − contagem de hoje
```

### 2.2 Botão "Está acabando" (requisição)

Botão grande na tela principal do tablet (e no painel). Em até 4 toques:

1. **O quê**: busca no catálogo (sabor de picolé, pote, balde, copo 300 ml, casquinha…) ou "Outro" com texto livre.
2. **Quanto tem agora**: número com +/−, ou "acabou" (0).
3. **Prioridade**, com significado fixo para não virar tudo urgente:

| Prioridade | Quando usar | Chega para a dona |
|---|---|---|
| **Urgente** | Acabou, ou cliente pediu e não tinha | **Na hora**, pelo WhatsApp |
| **Alta** | Acaba hoje ou amanhã | No **resumo do fim do dia** |
| **Média** | Está baixando; entra no próximo pedido | No **resumo do fim do dia** |

4. Observação opcional ("cliente pediu 3 vezes hoje").

**Regras:** "acabou" já vem como urgente. Se o mesmo item já tem aviso aberto, aparece "Já avisado hoje às 14:10 por Ana" com o botão **"Ainda falta"**, que atualiza a quantidade e pode subir a prioridade, sem criar aviso repetido. O aviso fecha sozinho quando a entrega daquele item é conferida, ou a dona fecha à mão ("comprei no mercado").

### 2.3 Como chega para a dona (WhatsApp)

| Quando | O quê | Como sai |
|---|---|---|
| **Na hora** | Aviso **urgente** (botão ou pote que zerou) | Logo depois de salvar, o app abre o WhatsApp do tablet com a mensagem pronta para o número da dona. O atendente só toca em enviar |
| **Fim do dia** | **Resumo**: urgentes do dia, altas, médias, potes no mínimo, diferenças da contagem | No **fechamento do caixa** (já é o fim do turno), o app mostra "Enviar resumo do dia para a dona" com a mensagem pronta |

Funciona do WhatsApp do tablet ou do celular de quem estiver com ele, sem custo. Sem internet, o aviso fica guardado e o app lembra de enviar quando a conexão voltar. Se depois a dona quiser envio **100% automático** (sem ninguém tocar em enviar), dá para trocar pela API oficial do WhatsApp: exige conta Meta Business, um número próprio da loja e cobra por mensagem. O resto do fluxo não muda.

Exemplo de urgente:

```
🔴 URGENTE — Sorveteria
Picolé ao leite Coco preto: ACABOU
Cliente pediu e não tinha.
Ana · 14/10 15:42
```

Exemplo de resumo:

```
Resumo de quarta 14/10 — Sorveteria
🔴 Urgente (1): Picolé ao leite Coco preto — acabou
🟠 Alta (2): Pote 1L Flocos — 1 · Copo 300 ml — 1
🟡 Média (1): Massa Pistache — 1
📦 Potes no mínimo: Pote 1L Amarena (2)
Vendas do dia: R$ 1.284,50 · Caixa fechado por Ana
```

Quantidades na menor unidade (picolé, pote, balde, copo). Hoje o resumo já traz urgentes, altas, médias e vendas do dia; **potes no mínimo** entram com o saldo dos potes (Sprint 7), **diferenças da contagem** com a contagem semanal, e "caixa fechado por" quando o app pedir o resumo no fechamento. Urgente que a dona já resolveu no dia aparece com "(resolvido)".

### 2.4 Fornecedores e pedido

Cada item do catálogo tem um **fornecedor**, com dia de pedido escolhido pela dona e forma de envio:

| Fornecedor | O que vende | Como recebe o pedido |
|---|---|---|
| **Fábrica (pais da dona)** | Picolé, Skimo, Moreninha, potes, massa, açaí | **Impresso** (e WhatsApp, se quiser) |
| Descartáveis (ex.) | Copos, colheres, guardanapos | WhatsApp |
| Casquinhas / coberturas (ex.) | Casquinha, cascão, calda | WhatsApp |

No dia do pedido de cada fornecedor, o painel monta o **pedido sugerido** (só com os itens dele):

```
sorvete com contagem  → saiu por semana (média das últimas 4) × semanas até a próxima entrega − contado hoje
pote com saldo        → mesma conta, com o saldo do sistema
sem histórico ainda   → estoque alvo que a dona define uma vez − contado
avisos abertos        → entram sempre, marcados ("Ana avisou: acabou")
insumos               → só o que tiver aviso aberto (não há contagem)
arredonda para cima em caixas (meia caixa se a fábrica aceitar)
```

A dona revisa com **+/−** e escolhe **Imprimir** e/ou **WhatsApp**.

**Folha impressa para a fábrica** (A4, 1 página, letra grande): cabeçalho com loja, nº do pedido e data; itens agrupados como na planilha, só os que têm quantidade; colunas **Sabor · Pedido · Veio · Obs.**, com "Veio" e "Obs." em branco para preencher à mão na entrega. É a coluna "Enviado" da planilha, agora no papel. Total estimado em R$ no rodapé, que pode ser ocultado se ela não quiser mostrar valores.

### 2.5 Conferência da entrega

Na entrega, a dona (ou o atendente) abre o pedido no painel: **"Chegou tudo certo"** ou corrige só as linhas diferentes, olhando a coluna "Veio" da folha. O sistema mostra **faltou / veio a mais / sabor trocado** com o valor em R$, gera a mensagem para a fábrica, põe o que faltou no próximo pedido e soma a entrega no saldo dos potes e na conta da contagem.

**Histórico por fornecedor:** % do pedido entregue e itens que mais faltam.

## 3. Dúvidas que ainda faltam (com padrão adotado)

| # | Pergunta | Padrão adotado |
|---|---|---|
| 1 | Qual o dia do pedido de cada fornecedor e em quantos dias chega? | Fábrica: pedido semanal na **segunda-feira**, entrega em 2 dias; contagem na véspera (domingo). A dona troca o dia no painel |
| 2 | A fábrica aceita **meia caixa** de picolé? | Não: pedido em caixas inteiras |
| 3 | Skimo vem em caixa de 24? Moreninha é por unidade? Pote de açaí 1,5L custa R$ 18? | Como está na planilha; corrigir no catálogo |
| 4 | O que são os "Outros" dos potes de 2L e 5L? | Cadastrar os sabores reais |
| 5 | Lista de copos, casquinhas e insumos e quem fornece | Começa vazia; a dona cadastra no painel |
| 6 | A folha impressa mostra preços? | Mostra o total; dá para ocultar |
| 7 | Mínimo de cada pote para alertar | 2 unidades para todos; a dona ajusta |

## 4. O que muda nas outras partes

- **App do atendente**: quantidade digitada na venda; grade de sabores só nos potes; botão **"Está acabando"**; tela de **contagem semanal**; mensagem urgente e resumo do dia no **fechamento do caixa**. Tudo funciona sem internet e sobe depois.
- **Painel**: avisos abertos, contagem, pedido por fornecedor (imprimir e WhatsApp), conferência da entrega, catálogo com fornecedor, mínimo e alvo; configuração do WhatsApp da dona.
- **Fase 3**: o custo por grupo já permite margem por produto (RN-PR-07); margem por sabor só nos potes.
- **Proposta (docs/05)**: "estoque dos sorvetes comprados prontos com contagem semanal, aviso do que está acabando pelo WhatsApp, pedido pronto para imprimir e conferência da entrega".

## 5. Ordem de entrega

1. **Sprint 5 — Catálogo, contagem e avisos**: catálogo da planilha com fornecedores, contagem semanal (tablet e painel), botão "Está acabando", mensagem urgente e resumo do dia, quantidade digitada na venda. *Sozinho já substitui a planilha de contagem e organiza os avisos.*
2. **Sprint 6 — Pedido, impressão e entrega**: pedido sugerido por fornecedor, folha impressa, WhatsApp, conferência da entrega, histórico.
3. **Sprint 7 — Potes automáticos**: sabor na venda de potes, saldo automático, alerta de mínimo, conferência na contagem; ajustes depois de 2 semanas reais.

O catálogo inicial continua em `docs/fase2/catalogo-inicial.csv`; insumos e outros fornecedores entram pelo painel.
