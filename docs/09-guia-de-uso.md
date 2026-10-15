# Guia de uso — Sorveteria

Uma página para quem está no balcão e para a dona. Guarde impressa perto do tablet.

> Endereço do painel: `https://________________.pages.dev` · Link do app: `https://________________/sorveteria.apk`
> (preencher quando o sistema for publicado — docs/07)

## Para o atendente (no tablet)

### 1. Começo do turno: abrir o caixa

1. Abra o app **Sorveteria** e digite seu **PIN** (4 a 6 números).
2. Toque em **Abrir caixa** e digite o **fundo de troco** (o dinheiro que já está na gaveta). Confirme.
3. Só dá para vender com o caixa aberto. Se outra pessoa deixou o caixa aberto, feche o dela primeiro (item 4).

### 2. Vender

- **➕ Nova comanda**: para quem senta ou pede mais de uma coisa. Toque nos produtos para adicionar (tocar de novo aumenta a quantidade). Use **📝 Observação** para recados ("sem calda").
- **⚡ Venda rápida**: um produto, pagamento na hora. Vai direto para o pagamento.
- **⚖️ Self-service (peso)**: digite o **valor que a balança mostrou** (em reais).
- **🛵 Delivery**: igual à comanda, com a observação de endereço/telefone.

**Pagar:**

1. Toque em **Pagar** e escolha a forma: **Dinheiro, Pix, Cartão débito ou Cartão crédito**.
2. Em **dinheiro**, digite quanto o cliente **entregou**. O app calcula o **troco** sozinho. Se faltar dinheiro, ele não deixa fechar.
3. O cliente pode pagar **metade em Pix e metade em dinheiro**: registre um pagamento de cada vez até zerar o total.
4. Toque em **Finalizar**. A venda fica guardada no tablet e é enviada para o painel sozinha.

**Errou?** Comanda ainda aberta: remova o item (**Remover**) ou toque em **Cancelar comanda** (pode escrever o motivo). Comanda já finalizada: só a dona desfaz, pelo painel (item "Estornar").

### 3. Durante o turno: dinheiro que entra ou sai da gaveta

- **Retirada (sangria)**: tirou dinheiro da gaveta para pagar o entregador, trocar notas ou guardar no cofre → registre no app com o valor e o motivo.
- **Reforço (suprimento)**: colocou dinheiro na gaveta → registre também.

Sem isso, o caixa "não bate" no fim do dia.

### 4. Fim do turno: fechar o caixa

1. Feche ou cancele todas as comandas abertas.
2. Toque em **Fechar caixa**. **Conte o dinheiro da gaveta** (notas e moedas, incluindo o fundo) e digite o total.
3. O app mostra quanto **deveria ter** e a **diferença**. Confirme. Pronto — a dona vê o fechamento no painel.

**Internet caiu?** Continue vendendo normalmente. O tablet guarda tudo e envia quando a internet voltar. No alto da tela aparece "X vendas aguardando envio" até tudo subir. Deixe o Wi‑Fi ligado ao fim do turno.

**Ver as vendas do turno:** aba **Turno**. Mostra cada venda paga ou cancelada, com a hora, o valor e se já foi enviada para o painel. Toque numa venda para ver os itens e o pagamento. Venda marcada em laranja foi **recusada pelo sistema**: ela continua guardada no tablet; mostre a mensagem para a dona.

## Para a dona (no celular ou no computador)

Abra o **endereço do painel** (no alto desta página) e entre com o usuário **admin** e a sua senha. A primeira abertura do dia pode levar **até 1 minuto** ("Conectando ao servidor…"): é normal, o servidor estava dormindo.

| Tela | O que mostra | O que dá para fazer |
|---|---|---|
| **Hoje** | Total vendido, número de vendas, ticket médio, quanto entrou em cada forma de pagamento, caixa atual | Trocar o dia (ontem, semana passada…) |
| **Vendas** | Cada comanda do dia com itens, pagamentos e troco. Dá para ver só as de uma atendente ou de uma forma de pagamento (a venda paga metade no Pix e metade no dinheiro aparece nas duas) | **Estornar** uma venda errada, escrevendo o motivo (a venda fica no histórico como estornada; nada some) |
| **Caixas** | Cada abertura e fechamento: fundo, vendas em dinheiro, retiradas, reforços, **esperado × contado × diferença** | Ver quem abriu/fechou e o que foi registrado |
| **Relatórios** (Análise) | Quanto vendeu num período (este mês, mês passado, 3 meses, este ano ou as datas que escolher) e quanto foi no período anterior: por dia, semana ou mês, os horários e os dias da semana que mais vendem, os produtos e categorias que mais faturam e os sabores dos potes que mais saem. "Este mês" compara com o começo do mês passado até o mesmo horário | Trocar o período e o "por dia / semana / mês"; ver os produtos por valor ou por quantidade; **Imprimir** e **Exportar Excel** (tudo numa planilha) |
| **Produtos** | Cardápio que aparece no tablet | Cadastrar, mudar preço, **desativar** (nunca apagar) e escolher a **ordem dos botões** (os mais vendidos primeiro) |
| **Configurações** | Nome da loja nas mensagens e o **seu WhatsApp** | Cadastrar o número que recebe o urgente na hora e o resumo do fim do dia; "Enviar mensagem de teste" |
| **Usuários** | Atendentes | Criar atendente, **redefinir o PIN**, desativar quem saiu |
| **Avisos** (Estoque e compras) | O que o balcão avisou que está acabando: urgentes primeiro, quanto tem agora, quem avisou e quando. O número vermelho no menu é quantos estão abertos | **Fechar** o aviso com o motivo ("comprei no mercado") — ele também fecha sozinho quando a entrega chegar; "Ainda falta" (a prioridade só sobe); criar aviso; **Resumo do dia** pronto para o WhatsApp |
| **Contagem** (Estoque e compras) | As contagens semanais do freezer (do tablet ou daqui): quando, quem contou, quantos itens | **Nova contagem**: digite ou use − e + (caixa vai de meia em meia), a contagem anterior aparece ao lado só como referência; "Salvar rascunho" para terminar depois; "Concluir" quando acabar (depois não muda mais) |
| **Pedidos** (Estoque e compras) | Cada fornecedor com o próximo dia de pedido, o pedido a caminho e o rascunho; abaixo, todos os pedidos (rascunho, enviado, recebido, cancelado) | No dia do pedido o rascunho já aparece montado com a sugestão do dia (o que você mudou antes fica): a sugestão vem da contagem, do que chegou depois dela, dos avisos e do que faltou na última entrega. Ajuste com − e +, "Salvar alterações", **Imprimir** (folha grande com as colunas Veio e Obs. em branco, para os pais preencherem) e **Enviar pedido** (depois não muda mais; aparece a mensagem pronta e o botão "Abrir no WhatsApp"). Quando chegar: **Conferir entrega** → "Chegou tudo certo" ou mude só o que veio diferente; o que faltou volta sozinho no próximo pedido e a mensagem para o fornecedor fica pronta |
| **Catálogo** (Estoque e compras) | O que a loja compra, separado por grupo como na planilha: sabor, fornecedor, embalagem, custo, mínimo e alvo | **Importar a planilha** (primeiro mostra a prévia, depois você confirma; o que já está cadastrado não muda), cadastrar item (copos, casquinhas, insumos), trocar o modo de controle do grupo, desativar sabor que saiu, exportar para o Excel |
| **Fornecedores** | Quem vende para a loja, dia do pedido, prazo de entrega e como o pedido é enviado; no fornecedor, como ele atendeu nos últimos 30 dias e o que mais falta | Cadastrar, mudar o dia do pedido, WhatsApp, meia caixa, mostrar preços na folha. Fornecedor com itens ativos só é desativado depois de trocar ou desativar os itens |

Mudou preço ou produto? No tablet, feche e abra o app, ou puxe a tela de produtos para baixo: o cardápio novo chega na hora.

## Quando algo sai do normal

| Situação | O que fazer |
|---|---|
| O tablet diz que não tem internet | Nada: continue vendendo. Confira se o Wi‑Fi está ligado no fim do turno |
| O painel demora para abrir | Espere 1 minuto. Se passar de 2, confira a internet do celular |
| Venda com valor errado já finalizada | Dona: painel → Vendas → Estornar (com motivo). Atendente: lança a venda certa de novo |
| Atendente esqueceu de fechar o caixa | Dona: painel → Caixas → **Fechar pelo painel** (com motivo). O valor contado fica 0 e a diferença aparece para conferir no dia seguinte |
| O tablet não deixa abrir o caixa: "já existe outro caixa aberto" | O sistema ainda vê um caixa antigo aberto. Se o tablet disser que o fechamento anterior não chegou, espere 1 minuto com internet e tente de novo. Se continuar, a dona fecha o caixa antigo pelo painel → Caixas → **Fechar pelo painel** (com motivo) e o tablet abre o novo |
| Enviei o pedido errado | Painel → Pedidos → o pedido → "Cancelar este pedido…" (com motivo), avise o fornecedor e monte outro |
| A entrega chegou com preço diferente | Na conferência, marque "A nota veio com preço diferente" e escreva o preço novo no item: vale dali para frente |
| Esqueci o PIN | Dona: painel → Usuários → Redefinir PIN |
| O app pede para atualizar | Toque no link, baixe e instale por cima. As vendas guardadas continuam lá |
| Caixa não bateu | Painel → Caixas mostra a diferença. Confira retiradas sem registro e trocos. Não dá para "consertar" o caixa fechado: registre um reforço ou retirada com motivo no caixa seguinte |

Dúvidas: fale com o Paulo. Nunca desinstale o app com vendas "aguardando envio".
