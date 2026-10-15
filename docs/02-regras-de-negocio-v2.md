# 02 — Regras de negócio v2

Cada regra tem um ID (usado nos testes e nos commits), a fase em que entra e onde é aplicada. Regras de **domínio** vivem nas entidades (`GestaoSorveteria.Domain`); regras de **aplicação** vivem nos serviços (`GestaoSorveteria.Application`).

Legenda de fase: **F1** sistema básico · **F2** estoque inteligente · **F3** análise e relatórios · **F4** expansões.

## 1. Usuários e acesso

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-US-01 | Existem dois perfis: **Atendente** e **Admin** (a dona). Admin tem acesso total; Atendente só ao necessário para vender e operar o caixa | F1 | Domain + autorização na API |
| RN-US-02 | Login é único, minúsculo, sem espaços, 3–50 caracteres (`a-z`, `0-9`, `.`, `_`) | F1 | Domain (`Usuario`) |
| RN-US-03 | Atendente autentica com **PIN de 4 a 6 dígitos**; Admin com **senha de no mínimo 8 caracteres** | F1 | Domain (`PoliticaSenha`) |
| RN-US-04 | Senhas/PINs nunca são gravados em texto; só o hash (PBKDF2-SHA256, 210 000 iterações, salt por usuário) | F1 | Infrastructure |
| RN-US-05 | Login aceita no máximo **10 tentativas por minuto por IP**; acima disso responde 429 | F1 | Server (rate limit) |
| RN-US-06 | Token JWT expira em **12 horas** (um turno). Expirou → novo login com PIN | F1 | Server |
| RN-US-07 | Usuário desativado não faz login **e o token que ele já tinha deixa de valer na hora** (a API confere `Ativo` a cada requisição, não só no login). O histórico dele permanece. Usuário nunca é apagado | F1 | Domain + Application + Server (`OnTokenValidated`) |
| RN-US-08 | Só Admin cria, edita, desativa usuários e redefine PIN/senha | F1 | Server (política Admin) + Application (`UsuarioService`) |
| RN-US-09 | O sistema nasce com um Admin criado a partir da configuração (`Seed:AdminLogin` / `Seed:AdminSenha`); só é criado se não houver usuários | F1 | Infrastructure (seed) |
| RN-US-10 | **(07/10/2026)** O Admin logado não pode desativar o próprio usuário (a loja nunca fica sem ninguém que entre no painel). Para desativar um Admin, outro Admin faz isso | F1 | Application (`UsuarioService`) |

## 2. Produtos

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-PR-01 | Produto tem nome (2–80 caracteres, único sem diferenciar maiúsculas), categoria (obrigatória) e preço ≥ 0 | F1 | Domain (`Produto`) |
| RN-PR-02 | Produto com `PermiteValorLivre` (ex.: "Self-service por kg") aceita valor digitado pelo atendente na venda; o preço cadastrado é apenas sugestão (pode ser 0) | F1 | Domain |
| RN-PR-03 | Produto é **desativado**, nunca apagado, quando já foi vendido. Produto inativo não aparece no app: a API entrega os inativos na sincronização (`GET /api/produtos?desde=`) e o app esconde o botão | F1 | Application + App |
| RN-PR-04 | Alterar o preço não altera vendas passadas (o item guarda o preço praticado) | F1 | Domain (`ItemComanda`) |
| RN-PR-05 | `Ordem` define a posição do botão no app (mais vendidos primeiro) | F1 | Domain |
| RN-PR-06 | Produto de venda pode ser ligado a um **grupo de estoque** (ex.: "Pote 1L" → grupo *Pote 1L*) com `UnidadesPorVenda` (padrão 1), pela tela do produto (separado do cadastro); grupo só de aviso não liga, e o grupo com produto ligado não vira "só aviso". Trocar o modo do grupo marca os produtos ligados como alterados, para o app saber se pede sabor. Pede o sabor na venda **só** se o grupo for `SaldoPelaVenda` (potes, RN-ES-09); picolé e massa vendem sem sabor. Ficha técnica de insumos fica para a Fase 4 (docs/10) | F2 | Domain |
| RN-PR-07 | Custo estimado de um item vendido = custo da embalagem do item de estoque ÷ unidades por embalagem × unidades por venda (ex.: picolé = R$ 25,20 ÷ 28 = R$ 0,90); produto sem vínculo de estoque aceita custo informado manualmente | F3 | Application |

## 3. Caixa (turno)

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-CX-01 | Só existe **um caixa aberto por vez**. Abrir um novo exige fechar o anterior | F1 | Application + índice único parcial no banco |
| RN-CX-02 | Abrir caixa informa o **fundo de troco** (≥ 0) e quem abriu | F1 | Domain (`Caixa.Abrir`) |
| RN-CX-03 | Toda comanda pertence ao caixa aberto no momento da criação. **Não há venda sem caixa aberto**. Exceção: a comanda sincronizada, remontada no servidor depois do fechamento do caixa (RN-CX-08) | F1 | Domain + Application |
| RN-CX-04 | **Sangria** (retirada de dinheiro) e **suprimento** (entrada de dinheiro) exigem valor > 0 e motivo; só em caixa aberto | F1 | Domain |
| RN-CX-05 | Fechar caixa exige que **não haja comanda aberta** (fechar ou cancelar antes). **Não** exige que as vendas já tenham sido enviadas: as que ainda não subiram chegam depois e vão para conferência (RN-CX-08, RN-CX-10) | F1 | Application / App |
| RN-CX-06 | No fechamento: `esperado = fundo + vendas em dinheiro + suprimentos − sangrias`; `diferença = contado − esperado`. Os três valores ficam gravados | F1 | Domain (`Caixa.Fechar`) |
| RN-CX-07 | Caixa fechado é imutável. Erro no fechamento → Admin registra ajuste no próximo caixa (com motivo), nunca edita o anterior. Venda recebida depois do fechamento **não altera** vendas em dinheiro, esperado, contado nem diferença gravados; só é marcada para conferência (RN-CX-08) | F1 | Domain |
| RN-CX-08 | Vendas sincronizadas depois do fechamento do caixa a que pertencem são **aceitas** e marcadas `recebida após fechamento` para conferência. A comanda remontada no servidor dispensa o caixa aberto (RN-CX-03), mas passa pelas mesmas conferências da RN-SY-06; se não bater, é rejeitada no resultado do lote | F1 | Domain + Application |
| RN-CX-09 | **(Sprint 4 — decisão de 30/09/2026)** Admin pode forçar o fechamento pelo painel (ex.: atendente esqueceu), informando valor contado 0 e motivo; a diferença fica registrada. Se depois chegar o fechamento feito no celular, ele é **rejeitado** com motivo (o caixa não muda, RN-CX-07; o valor contado no celular aparece no motivo) e a dona registra o ajuste no próximo caixa. Vendas feitas no celular depois do fechamento forçado são aceitas e marcadas (RN-CX-08). O caixa fica marcado `fechamento forçado` e o motivo vai para a observação ("Fechamento forçado pelo painel: …") | F1 | Domain (`Caixa.FecharForcado`) + Application (`CaixaService`) |
| RN-CX-10 | Caixa fechado no celular e sincronizado: o servidor recalcula vendas em dinheiro (comandas já recebidas daquele caixa), esperado e diferença com a RN-CX-06 e **grava os valores do servidor**. Se as vendas em dinheiro ou o esperado calculados no celular forem diferentes, grava também os valores do celular e marca o caixa com `divergência na sincronização` para a dona conferir. O caixa nunca é rejeitado por essa diferença. Caixa que chega já fechado sem ter vindo aberto antes é aceito do mesmo jeito: as vendas dele ainda não chegaram, então fica marcado com divergência e as comandas que chegarem depois são marcadas pela RN-CX-08 | F1 | Domain + Application |

## 4. Comanda (venda)

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-CM-01 | Abrir comanda leva menos de 2 s: o app cria localmente com `Id` (UUID) gerado no celular e envia à API; a API aceita o `Id` do cliente | F1 | App + API |
| RN-CM-02 | Comanda tem tipo **Balcão** ou **Delivery** e número sequencial dentro do caixa (1, 2, 3…) | F1 | Domain |
| RN-CM-03 | Item de comanda vem de um **produto cadastrado** (descrição e preço copiados no momento) **ou** é um **item livre** (descrição + valor digitado > 0) — usado para self-service e venda rápida | F1 | Domain (`ItemComanda`) |
| RN-CM-04 | Quantidade é inteira ≥ 1. Mesmo produto adicionado de novo soma na linha existente | F1 | Domain |
| RN-CM-05 | `Subtotal = quantidade × preço unitário` (2 casas, arredondamento comercial). `Total = Σ subtotais` | F1 | Domain |
| RN-CM-06 | Enquanto **aberta**, itens podem ser adicionados, alterados e removidos; observação pode ser editada | F1 | Domain |
| RN-CM-07 | **Fechar** = registrar pagamentos cuja soma seja **exatamente** o total (total > 0). Ao fechar, a comanda vira imutável e o estoque baixa (F2) | F1 | Domain |
| RN-CM-08 | Comanda aberta pode ser **cancelada** pelo atendente (motivo opcional). Nada é apagado | F1 | Domain |
| RN-CM-09 | Comanda fechada só pode ser **estornada** por Admin, com motivo obrigatório; vira `Estornada` (status próprio, diferente da `Cancelada` da RN-CM-08), guarda quem estornou, quando e o motivo, entra nos relatórios como estorno e devolve o estoque (F2). O estorno **não altera caixa nenhum**: caixa fechado continua imutável (RN-CX-07) e o dinheiro devolvido ao cliente, se houver, é uma sangria registrada no caixa aberto pelo app (decisão de 30/09/2026) | F1 | Domain + Application |
| RN-CM-10 | **Venda rápida por valor**: uma única operação cria a comanda com um item livre "Venda avulsa" no valor informado e já fecha com o pagamento | F1 | Application |
| RN-CM-11 | Delivery na F1 é só marcação + observação (nome/endereço). Taxa de entrega e iFood ficam na F4 | F1 | — |
| RN-CM-12 | Cada comanda guarda `CriadaEm` (hora do celular) e `RecebidaEm` (hora do servidor) — a diferença mostra quanto tempo ficou offline | F1 | Domain |

## 5. Pagamento

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-PG-01 | Formas: **Dinheiro, Pix, Cartão débito, Cartão crédito** | F1 | Domain (`FormaPagamento`) |
| RN-PG-02 | Uma comanda aceita **mais de um pagamento** (ex.: metade Pix, metade dinheiro) | F1 | Domain |
| RN-PG-03 | Pagamento tem `Valor` (quanto abate da comanda, > 0). Em dinheiro, também `ValorRecebido ≥ Valor` e `Troco = ValorRecebido − Valor`. Nas outras formas, `ValorRecebido = Valor` e `Troco = 0` | F1 | Domain (`Pagamento`) |
| RN-PG-04 | Troco é calculado pelo sistema, nunca digitado | F1 | Domain |
| RN-PG-05 | Só pagamentos em dinheiro entram no "esperado" do caixa; Pix e cartão entram no faturamento, não na gaveta | F1 | Domain (`Caixa.Fechar`) |

## 6. Estoque, avisos e pedidos ao fornecedor (Fase 2)

Desenho e motivo em **docs/10** (revisto em 14/10). A loja compra sorvete **pronto** da fábrica (os pais da dona) e insumos de outros fornecedores; costuma ter **um freezer só**. A venda no balcão não pode ficar mais lenta: picolé não pede sabor.

### 6.1 Catálogo e controle por grupo

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-ES-01 | **Item de estoque**: grupo, sabor/nome, fornecedor, unidade de compra (`Caixa`, `Balde`, `Unidade`, `Pacote`), unidades por embalagem, custo da embalagem (≥ 0), estoque alvo (opcional), mínimo (opcional), ativo, ordem. Grupo + nome único (sem diferenciar maiúsculas). Catálogo de sorvetes importado de `docs/fase2/catalogo-inicial.csv`; insumos cadastrados pelo painel | F2 | Domain (`ItemEstoque`) |
| RN-ES-02 | Cada **grupo** tem um **modo de controle**: `Contagem` (picolé, Skimo, Moreninha, massa, açaí — padrão), `SaldoPelaVenda` (potes — teste) ou `SoAviso` (copos, casquinhas, insumos: sem saldo, só avisos e pedido). A dona pode trocar o modo de um grupo | F2 | Domain |
| RN-ES-03 | Quantidades guardadas na **menor unidade** (picolé, pote, balde), inteiras; exibidas em embalagens com meia caixa ("2,5 cx"). Contagem aceita passos de 0,5 caixa | F2 | Domain |
| RN-ES-04 | Na venda, a **quantidade** de um item pode ser digitada (teclado numérico ao tocar no número), além de + e −; 1 a 999 por item | F2 | App |
| RN-ES-10 | **Importação da planilha** (CSV no formato de `docs/fase2/catalogo-inicial.csv`, com vírgula ou com ponto e vírgula do Excel, em UTF-8): **só cria o que falta** — fornecedor e grupo pelo nome, item por grupo + sabor. O que já está no catálogo **nunca é alterado** pela planilha (depois da primeira carga quem manda é o painel); custo, embalagem ou fornecedor diferente vira **aviso** na linha. Primeiro a **prévia** (nada gravado: o que seria criado, avisos, erros) e depois a confirmação. Linha com problema fica de fora com o número da linha e o motivo e não deixa nada criado pela metade. Valor ambíguo ("1,000.00") é recusado, nunca adivinhado. Fornecedor novo nasce com os padrões de docs/10 §3 (pedido na segunda, entrega em 2 dias, sem meia caixa, impresso, com preços); grupo novo nasce em `Contagem` (potes também, até a venda com sabor da Sprint 7); itens de potes nascem com mínimo 2 | F2 | Application |
| RN-ES-11 | Nome de **grupo** e de **fornecedor** único (sem diferenciar maiúsculas nem a forma do acento). Item pode mudar de grupo | F2 | Application + índice único |

### 6.2 Contagem semanal

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-ES-05 | **Contagem semanal** dos grupos `Contagem` e `SaldoPelaVenda`, por atendente ou Admin, no tablet (offline, sobe pela sincronização) ou no painel. O app mostra "Hoje é dia de contagem" na véspera do dia de pedido da fábrica. Pode ser salva parcial e concluída depois; só a contagem **concluída** vale | F2 | App + Application |
| RN-ES-06 | Na tela, a contagem anterior aparece só como referência; o campo começa vazio. "Não tem nenhum" grava 0. Item não contado numa contagem concluída fica "não contado" (não vira 0). Cada item guarda **quando foi contado** (o painel grava a hora do salvamento em que o número mudou; o tablet pode mandar a hora de cada item); sem ela vale a conclusão da contagem | F2 | App |
| RN-ES-07 | **Saída da semana** (grupo `Contagem`) = contagem anterior + recebido entre as duas − contagem atual. Negativo (contou mais do que devia) aparece como "conferir", não entra na média | F2 | Application |
| RN-ES-08 | Grupo `SaldoPelaVenda` (potes): saldo = **última contagem concluída** + movimentos que aconteceram depois dela (`Entrada` da entrega conferida, − `BaixaVenda`, + `EstornoVenda`); sem contagem, a soma dos movimentos. A contagem é a nova base (não vira movimento): venda que sincroniza depois da contagem mas foi feita antes dela já estava fora do freezer e não conta de novo; o estorno usa o momento da venda que desfaz. Na contagem, o saldo esperado fica gravado na linha e a diferença (contado − saldo) maior que 1 unidade entra no resumo do dia da dona. Movimento nunca é apagado | F2 | Domain + Application |
| RN-ES-09 | **Venda de pote com sabor**: produto de venda ligado a grupo `SaldoPelaVenda` pede o sabor (1 toque). A comanda fechada, ao chegar ao servidor, gera `BaixaVenda` (quantidade × unidades por venda, na hora do fechamento); cancelada não baixa; estorno gera `EstornoVenda`. Sabor que o servidor não conhece ou que não é do grupo do produto não rejeita a venda: ela entra sem sabor e sem baixa (a contagem corrige). A venda nunca é bloqueada por saldo (negativo é aviso) | F2 | App + Application (sync) |

### 6.3 Avisos "Está acabando"

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-AV-01 | **Aviso** (requisição): item do catálogo ou texto livre ("Outro"), quantidade atual (≥ 0), prioridade `Urgente` / `Alta` / `Media`, observação opcional, quem e quando. Nasce no tablet (offline) ou no painel. Nada é apagado | F2 | Domain (`Aviso`) |
| RN-AV-02 | Significado fixo da prioridade, mostrado na tela: **Urgente** = acabou ou cliente pediu e não tinha; **Alta** = acaba hoje ou amanhã; **Média** = está baixando. Quantidade 0 já sugere Urgente | F2 | App |
| RN-AV-03 | **Sem duplicar**: se o mesmo item tem aviso aberto, o app mostra "Já avisado às HH:mm por X" e oferece **"Ainda falta"**, que atualiza quantidade/observação e só pode **subir** a prioridade. Texto livre não deduplica | F2 | Application |
| RN-AV-04 | Pote (`SaldoPelaVenda`) que chega no **mínimo** gera aviso `Alta` automático; que **zera** gera `Urgente` automático (autor "Sistema"), com a mesma regra de não duplicar: se o item já tem aviso aberto, a prioridade só sobe; a quantidade acompanha o saldo só enquanto a última informação do aviso for do sistema (o que a atendente informou — "restam 2", "Ainda falta" — nunca é trocado pela conta). Aviso criado e atualizado com a hora da venda | F2 | Application |
| RN-AV-05 | Aviso fecha: (a) sozinho quando a entrega de um pedido com aquele item é conferida com quantidade > 0; (b) pela dona, com motivo ("comprei no mercado"); (c) Média/Alta sem item de catálogo ficam abertos até a dona fechar | F2 | Domain |
| RN-AV-06 | **Urgente** → logo após salvar, o app abre o WhatsApp com a mensagem pronta para o número da dona (configurado no painel). Sem internet, fica "a enviar" e o app lembra quando a conexão voltar | F2 | App |
| RN-AV-07 | **Resumo do dia** → oferecido no **fechamento do caixa**: urgentes do dia, altas e médias abertas, potes no mínimo, diferenças da última contagem, total vendido do dia. Mensagem pronta para o WhatsApp da dona. Se o caixa for fechado pelo painel, o resumo fica disponível no painel | F2 | App + Application |
| RN-AV-08 | Envio do WhatsApp é por link (`wa.me`): o sistema nunca afirma que a mensagem foi entregue, só que foi aberta para envio. A troca para envio automático (API oficial) não muda as regras acima | F2 | App |
| RN-AV-09 | **Sincronização dos avisos** (tablet offline): o tablet manda o estado atual de cada aviso. Item que já tem aviso aberto no servidor junta no aberto; a prioridade fica com a maior das duas; quantidade e observação só mudam com informação mais nova. Mudança feita no tablet depois que a dona fechou o aviso é recusada com o motivo (o atendente avisa de novo) — nunca some em silêncio | F2 | Application (sync) |
| RN-AV-10 | **Configuração da loja**: nome que aparece nas mensagens e WhatsApp da dona (DDD + número; com 12–13 dígitos só com o 55 na frente). O app e o painel leem; só a dona altera | F2 | Domain + Application |

### 6.4 Fornecedores e pedido

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-PF-01 | **Fornecedor**: nome, WhatsApp (opcional), dias de pedido, prazo de entrega em dias, aceita meia caixa, **forma de envio** (`Impresso`, `WhatsApp` ou ambos), mostrar preços na folha (sim/não), ativo | F2 | Domain |
| RN-PF-02 | **Pedido sugerido** por fornecedor, por item ativo: grupo `Contagem` → `média de saída semanal (até 4 semanas) × semanas a cobrir − última contagem`, onde **semanas a cobrir** = (dias até o próximo dia de pedido + prazo de entrega) ÷ 7 — o estoque precisa durar até chegar o pedido seguinte (pedido toda segunda, entrega em 2 dias: 9 dias). O **estoque de agora** = última contagem + o que foi recebido depois dela − a saída estimada desde a contagem (média × dias ÷ 7, nunca negativo) + o que está **a caminho** (pedido `Enviado` ainda não recebido, marcado "A caminho: N no pedido nº X") — assim a dona não pede em dobro. Item **sem contagem** (ou com contagem de mais de 14 dias e sem média, marcado "Última contagem em dd/mm") usa no lugar da contagem o que os pedidos enviados desde o último dia de pedido já trouxeram; `SaldoPelaVenda` (potes) → quando a venda já manda o sabor (há vendas com baixa nos últimos 28 dias), mesma conta com o **saldo** no lugar da contagem e a média pelas **vendas** (vendas − estornos desde a primeira venda da janela, no mínimo uma semana); sem vendas com sabor, a conta da contagem (o saldo não desconta o que foi vendido sem sabor); sem histórico → `estoque alvo − contado/saldo`; `SoAviso` → só itens com aviso aberto (quantidade = 1 embalagem, a dona ajusta). Arredonda para cima em embalagens (0,5 se aceita meia caixa), depois de arredondar a conta a 6 casas (dízima não vira uma caixa a mais). Negativo → 0 | F2 | Application |
| RN-PF-09 | Fornecedor com **itens ativos** não é desativado (os itens sumiriam do pedido sem ninguém perceber): a dona troca o fornecedor dos itens ou desativa os itens antes. Item **novo**, ou item que volta a ficar ativo, não pode ser de fornecedor desativado; o item que já era dele pode ser corrigido | F2 | Application |
| RN-PF-03 | Todo **aviso aberto** do fornecedor entra no sugerido (mínimo 1 embalagem), marcado com quem avisou e a prioridade — salvo se o item já vem num pedido enviado **depois** do aviso (o aviso fecha quando essa entrega for conferida); o que **faltou** na última entrega também (pelo menos a quantidade que faltou, marcado "Faltou no pedido nº N"). As marcas se somam na observação ("Ana avisou (urgente): acabou · Faltou 1 balde no pedido nº 3"). No rascunho entram todos os itens ativos dos grupos de contagem (mesmo com 0, para a dona ajustar com +/−) e os insumos só com aviso | F2 | Application |
| RN-PF-04 | Rascunho montado sozinho no dia de pedido do fornecedor (ou pelo botão "Montar pedido"); no máximo um rascunho por fornecedor; remontar mantém a quantidade das linhas que a dona alterou (arredondada para cima se o passo mudou) e atualiza custo, passo e marcas de todas. A montagem sozinha não começa outro pedido se o do dia já foi enviado: mostra o enviado | F2 | Application |
| RN-PF-05 | Linha do pedido: sugerido, pedido (embalagens), custo da embalagem no momento (snapshot). `Subtotal = pedido × custo` com 2 casas e `Total = Σ subtotais` (o total bate com a soma da folha) | F2 | Domain (`PedidoFornecedor`) |
| RN-PF-06 | **Confirmar** exige uma linha > 0; o pedido vira `Enviado` e fica imutável (correção = cancelar com motivo e montar outro). Gera a **folha para imprimir** e/ou a **mensagem de WhatsApp**, conforme o fornecedor | F2 | Domain + Application |
| RN-PF-07 | **Folha impressa**: A4, letra grande, cabeçalho (loja, fornecedor, nº, data), itens agrupados na ordem da planilha só com quantidade > 0, colunas **Sabor · Pedido · Veio · Obs.** ("Veio" e "Obs." em branco), total estimado (oculto se o fornecedor estiver configurado sem preços). Pode ser reimpressa a qualquer momento | F2 | Painel |
| RN-PF-08 | Status: `Rascunho` → `Enviado` → `Recebido` (ou `Cancelado`, com motivo e quem cancelou, de `Rascunho`/`Enviado`). Nada é apagado | F2 | Domain |

### 6.5 Recebimento (conferência da entrega)

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-RC-01 | Recebimento parte de um pedido `Enviado`, com `recebido = pedido`. A dona informa **quando a entrega chegou** (padrão: agora; entre o envio e agora): é essa data que conta para o estoque, então conferir dias depois não conta a entrega duas vezes com a contagem. **"Chegou tudo certo"** confirma de uma vez; senão altera só as linhas diferentes (meia caixa vale sempre para caixa, mesmo que o pedido tenha ido em caixas inteiras) e pode **adicionar item não pedido** (sabor trocado). Conferência com erro em qualquer linha não grava nada. A hora do recebimento é a da conferência no sistema | F2 | Application |
| RN-RC-02 | Cada linha: **Certo**, **Faltou**, **Veio a mais** ou **Não pedido** (também a linha que estava com 0 no pedido e veio — sabor trocado —, que fica com o custo de agora); diferença em R$ = (recebido − pedido) × custo | F2 | Domain |
| RN-RC-03 | Confirmar: grupos `SaldoPelaVenda` recebem `Entrada`; grupos `Contagem` registram o recebido para a conta da saída semanal (RN-ES-07); avisos dos itens recebidos fecham (RN-AV-05). `% atendido = Σ min(recebido, pedido) ÷ Σ pedido` | F2 | Domain + Application |
| RN-RC-04 | Linhas "Faltou" voltam no próximo sugerido, salvo se a dona desmarcar "pedir de novo" — só uma vez: volta enquanto nenhum pedido do fornecedor tiver sido enviado depois da conferência da entrega com falta; o sistema gera a mensagem para o fornecedor com o que faltou/veio trocado | F2 | Application |
| RN-RC-05 | Preço diferente na entrega: a dona informa o novo custo (só de item que chegou, quantidade > 0), que vale para os próximos pedidos e para o sabor trocado desta entrega (o pedido antigo mantém o snapshot) | F2 | Domain |
| RN-RC-06 | **Histórico do fornecedor**: pedidos recebidos nos últimos 30 dias, valor pedido × recebido, % atendido médio, itens que mais faltam | F2 | Application |
| RN-RC-07 | Só **Admin** monta, envia, cancela, recebe pedidos, fecha avisos e muda catálogo/modo de controle. Atendente faz venda, contagem e avisos | F2 | Application |

## 7. Relatórios (Fase 3)

| ID | Regra | Fase |
|---|---|---|
| RN-RL-01 | Faturamento = soma das comandas **fechadas** (as `Estornada` ficam à parte, somadas como estornos no dia da venda) | F3 |
| RN-RL-02 | Períodos (dia/semana/mês) usam o dia comercial em `America/Sao_Paulo` | F3 |
| RN-RL-03 | Ranking por quantidade e por valor; itens livres agrupados como "Self-service / venda avulsa". Nome e categoria do **cadastro atual** do produto (a linha do pote traz o sabor, "Pote 1L · Flocos", e não serve de nome); produto fora do cadastro usa a descrição da venda mais recente. No dashboard do dia (adiantado em 14/10/2026): até 5 produtos por faturamento das comandas pagas (estornadas fora); empate → mais unidades, depois nome. No relatório de período: todos os produtos, com a parte do faturamento (%, 1 casa), a soma por categoria e os **sabores** dos potes (RN-ES-09) por unidades que saíram (quantidade × "quantos saem por venda" atual do produto) | F1/F3 |
| RN-RL-04 | Margem estimada = preço praticado − custo estimado (RN-PR-07); sem custo → "n/d", nunca zero | F3 |
| RN-RL-05 | Exportação em CSV/Excel de qualquer período | F3 |
| RN-RL-06 | **Vendas por hora** do dia: comandas pagas agrupadas pela hora local do fechamento (0–23, America/Sao_Paulo); só as horas com venda | F1 |
| RN-RL-07 | **Comparativo** com o mesmo dia da semana anterior (hoje − 7 dias). Se o dia consultado é hoje, a semana passada conta só até o mesmo horário. Variação % = (atual − anterior) ÷ anterior × 100, 1 casa; anterior zero → sem variação ("—"), nunca 100% | F1 |
| RN-RL-08 | **Relatório de período**: de–até em dias comerciais, inclusive, até 366 dias, sem passar de hoje; sem datas = este mês até hoje. Dividido por **dia** (até 93 dias), **semana** (segunda a domingo) ou **mês**, recortados nas pontas do período; todo trecho aparece, mesmo sem venda. Sem escolha: dia até 31 dias, semana até 92, mês acima | F3 |
| RN-RL-09 | **Comparativo do período**: menos de 7 dias → os mesmos dias da semana anterior (sábado com sábado; 1 dia = o mesmo do dashboard, RN-RL-07). Começa no dia 1 → os mesmos meses logo antes: meses inteiros contra meses inteiros (setembro contra agosto inteiro, trimestre contra trimestre) e mês pela metade contra o mesmo trecho ("este mês" até o dia 14 contra 1–14 do mês passado), limitado ao fim do mês (30/03 → 28/02). Qualquer outro → os mesmos N dias logo antes. Se o período termina hoje, o último dia do anterior conta só até o mesmo horário — quando é o mesmo dia deslocado; encurtado (28/02 no lugar de 30/03) ou mês inteiro, conta inteiro. Os trechos do anterior vêm pareados por posição com os do período (o mês anterior pode ter um a menos). Variação como no RN-RL-07 | F3 |
| RN-RL-10 | **Picos**: vendas pagas do período por hora local do fechamento (RN-RL-06) e por dia da semana (segunda a domingo, só os dias que o período tem), com a **média por dia** = total ÷ quantos desses dias o período tem (hoje conta como um dia, mesmo aberto) | F3 |

## 8. Sincronização offline (app do atendente)

| ID | Regra | Fase |
|---|---|---|
| RN-SY-01 | O app guarda em SQLite: cache de produtos, o caixa atual e as comandas. A comanda vive só no celular enquanto está aberta; ao fechar ou cancelar, vira um **documento pronto** (comanda + itens + pagamentos) marcado `pendente de envio`. O caixa também é documento (abertura + movimentos + fechamento). Não existe fila de operações (docs/06, seção 3) | F1 (Sprint 2) |
| RN-SY-02 | Todo documento (caixa, movimento, comanda, item, pagamento) leva um `Id` UUID gerado no celular. A API é **idempotente**: reenviar o mesmo documento devolve o mesmo resultado sem duplicar | F1 |
| RN-SY-03 | Os documentos pendentes são enviados em lote (até 100), nesta ordem: caixa aberto → comandas do caixa → caixa fechado. Envio logo após fechar/cancelar, a cada 1 min e ao abrir o app. Falha de rede pausa o envio sem descartar nada; documento `rejeitado` fica visível com o motivo | F1 (Sprint 2) |
| RN-SY-04 | Só há venda offline se o app conhece um caixa aberto. **Abrir** caixa exige conexão (garante um só caixa aberto, RN-CX-01); **fechar** pode ser sem internet — o caixa fechado sobe depois (RN-CX-10) | F1 |
| RN-SY-05 | O app mostra um indicador "X vendas aguardando envio". Fechar o caixa com envio pendente é permitido (RN-CX-05), com o aviso de que essas vendas sobem depois | F1 (Sprint 2) |
| RN-SY-06 | Comanda sincronizada é remontada no servidor com a **descrição e o preço praticados na venda** (RN-PR-04), mesmo que o produto tenha mudado de preço ou sido desativado depois. O servidor confere subtotal, total, pagamentos somando **exatamente** o total e troco calculado (RN-PG-04); qualquer diferença → `rejeitada` no resultado do lote, com motivo | F1 |

## 9. Tempo, dinheiro e auditoria

| ID | Regra | Fase |
|---|---|---|
| RN-TD-01 | Datas gravadas em UTC (`timestamptz`); exibidas no fuso `America/Sao_Paulo` | F1 |
| RN-TD-02 | Dinheiro em `decimal(12,2)`; nunca `double`/`float`; arredondamento `MidpointRounding.AwayFromZero`. Valor digitado aceito até R$ 9.999.999,99; subtotal e total até o limite da coluna (R$ 9.999.999.999,99) — acima disso é erro de digitação, recusado com motivo | F1 |
| RN-TD-03 | Nenhum registro de venda, pagamento, caixa ou movimento é apagado; correções geram novos registros com motivo | F1 |
| RN-TD-04 | Todo registro guarda quem fez (`UsuarioId`) e quando | F1 |

## 10. Perguntas abertas para confirmar com a dona

1. Aceita cartão (maquininha)? Quais bandeiras/formas devem aparecer?
2. Lista de produtos, categorias e preços atuais.
3. Fase 2 (estoque e pedidos ao fornecedor): perguntas em docs/10, seção 5.
4. Self-service é por peso? Existe balança com impressão de valor?
5. Quem abre e fecha o caixa no dia a dia? Existe fundo de troco fixo?
6. Delivery: próprio ou por app (iFood)? Precisa registrar endereço?
7. Internet no balcão: Wi-Fi da loja ou dados do celular? Cai com frequência?
8. Modelo do celular Android que ficará no balcão (versão mínima: Android 8).
