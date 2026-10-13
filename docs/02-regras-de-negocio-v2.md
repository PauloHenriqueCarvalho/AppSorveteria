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
| RN-PR-06 | Produto de venda pode ser ligado a um **grupo de estoque** (ex.: "Picolé ao leite" → grupo *Picolé ao leite*) com `UnidadesPorVenda` (padrão 1). Produto ligado pede o **sabor** na venda (RN-ES-05). Produto sem vínculo (ex.: self-service, venda avulsa) não movimenta estoque. Ficha técnica de insumos fica para a Fase 4 (docs/10) | F2 | Domain |
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

## 6. Estoque e pedidos ao fornecedor (Fase 2)

Desenho e motivo em **docs/10**: a loja compra sorvete **pronto** de um fornecedor (picolé por caixa, pote, balde). O estoque é desses itens, não de insumos. Objetivo: o pedido e a conferência da entrega com o mínimo de trabalho manual.

### 6.1 Itens de estoque e saldo

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-ES-01 | **Item de estoque** = o que se compra pronto: grupo (Picolé de fruta, Picolé ao leite, Skimo, Moreninha, Pote 1L, Pote 2L, Pote 5L, Pote de açaí, Açaí, Açaí trufado, Massa), sabor, fornecedor, unidade de compra (`Caixa`, `Balde`, `Unidade`), unidades por embalagem (ex.: 28 picolés por caixa), custo da embalagem (≥ 0), estoque alvo em embalagens (opcional), ativo. Grupo + sabor é único (sem diferenciar maiúsculas). Catálogo inicial importado de `docs/fase2/catalogo-inicial.csv` | F2 | Domain (`ItemEstoque`) |
| RN-ES-02 | Saldo guardado na **menor unidade** (picolé, pote, balde), inteiro; exibido em embalagens com meia caixa ("2,5 cx"). Entrada de contagem aceita passos de 0,5 caixa | F2 | Domain |
| RN-ES-03 | Saldo = soma dos movimentos: `Entrada` (recebimento), `BaixaVenda`, `EstornoVenda`, `AberturaBalde`, `AjusteContagem`, `Perda`. Nunca se edita o saldo direto; `Perda` exige motivo | F2 | Domain |
| RN-ES-04 | **Saldo inicial** = primeira contagem (RN-ES-08) feita pelo painel ao começar a Fase 2 | F2 | Application |
| RN-ES-05 | **Baixa pela venda:** item de comanda de produto ligado a um grupo (RN-PR-06) guarda o **sabor escolhido** (`ItemEstoqueId`). Quando a comanda **fechada** chega ao servidor, gera `BaixaVenda` = quantidade × unidades por venda. Comanda cancelada não baixa; estorno gera `EstornoVenda` | F2 | Application (sync) |
| RN-ES-06 | **Massa e açaí em balde** saem quando o atendente registra **"Abri um balde"** de um sabor (1 balde, `AberturaBalde`). Venda por bola/peso não baixa. O registro nasce no celular com Id próprio e sobe pela sincronização (idempotente) | F2 | App + Application |
| RN-ES-07 | A venda **nunca** é bloqueada por estoque; saldo negativo é permitido e aparece como aviso ("conferir contagem") | F2 | Domain |
| RN-ES-08 | **Contagem rápida** (painel ou app): mostra o saldo esperado de cada item; quem conta altera só o que difere. Cada diferença vira `AjusteContagem` com o motivo "contagem dd/mm" e fica na conferência (esperado × contado), como no caixa | F2 | Application |
| RN-ES-09 | **Consumo diário** = saídas (`BaixaVenda` + `AberturaBalde` − `EstornoVenda`) dos últimos 28 dias ÷ dias com caixa aberto. Menos de 14 dias de histórico → "sem histórico" e vale o estoque alvo. **Dias restantes** = saldo ÷ consumo diário | F2 | Application |
| RN-ES-10 | Aviso **"pedir hoje"** quando os dias restantes de um item são menores que o prazo de entrega do fornecedor + 1 dia | F2 | Application |

### 6.2 Pedido ao fornecedor

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-PF-01 | **Fornecedor**: nome, WhatsApp, dias da semana de pedido, prazo de entrega em dias (≥ 0), aceita meia caixa (sim/não), ativo | F2 | Domain |
| RN-PF-02 | **Pedido sugerido** por item ativo do fornecedor: `necessário = consumo diário × (prazo de entrega + dias até o próximo pedido)`; `sugerido = necessário − saldo − já pedido e não recebido`, arredondado **para cima** em embalagens (passo 0,5 se o fornecedor aceitar meia caixa, senão 1). Sem histórico: `estoque alvo − saldo`. Sem alvo e sem histórico: 0. Negativo → 0 | F2 | Application |
| RN-PF-03 | Item que **faltou** no último recebimento (RN-RC-04) entra no sugerido com a marca "faltou no último pedido" | F2 | Application |
| RN-PF-04 | O rascunho é montado **sozinho** no dia de pedido do fornecedor (e a qualquer momento pelo botão "Montar pedido"). Existe no máximo **um rascunho** por fornecedor; remontar substitui só as linhas que a dona não alterou | F2 | Application |
| RN-PF-05 | Pedido guarda por linha: sugerido, pedido (o que a dona confirmou, em embalagens) e custo da embalagem no momento (snapshot). `Total = Σ pedido × custo`, 2 casas | F2 | Domain (`PedidoFornecedor`) |
| RN-PF-06 | **Confirmar** exige ao menos uma linha > 0; o pedido vira `Enviado`, fica imutável e gera a mensagem de WhatsApp agrupada por grupo, na ordem da planilha (link `wa.me` com o texto). Correção depois de enviado = cancelar com motivo e montar outro | F2 | Domain + Application |
| RN-PF-07 | Status: `Rascunho` → `Enviado` → `Recebido` (ou `Cancelado`, com motivo, a partir de `Rascunho`/`Enviado`). Nada é apagado | F2 | Domain |

### 6.3 Recebimento (conferência da entrega)

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-RC-01 | Recebimento parte de um pedido `Enviado`: a lista vem preenchida com `recebido = pedido`. **"Chegou tudo certo"** confirma de uma vez; senão altera só as linhas diferentes (passo de 0,5 caixa) e pode **adicionar item que veio sem pedir** (sabor trocado) | F2 | Application |
| RN-RC-02 | Cada linha fica classificada: **Certo**, **Faltou** (recebido < pedido), **Veio a mais** (recebido > pedido) ou **Não pedido**. Diferença em R$ = (recebido − pedido) × custo da embalagem | F2 | Domain |
| RN-RC-03 | Confirmar o recebimento gera uma `Entrada` por item recebido (em unidades) e muda o pedido para `Recebido`, com `% atendido = Σ min(recebido, pedido) ÷ Σ pedido` (em embalagens) | F2 | Domain + Application |
| RN-RC-04 | Linhas "Faltou" voltam no próximo pedido sugerido (RN-PF-03), a menos que a dona desmarque "pedir de novo" | F2 | Application |
| RN-RC-05 | O sistema gera a **mensagem de cobrança** para o fornecedor (faltou X, veio Y no lugar de Z, diferença R$) | F2 | Application |
| RN-RC-06 | Se o preço da nota for diferente, a dona informa o novo custo da embalagem no recebimento: atualiza o item de estoque para os próximos pedidos (o pedido antigo mantém o snapshot) | F2 | Domain |
| RN-RC-07 | **Histórico do fornecedor**: pedidos do mês, valor pedido × recebido, % atendido médio e os itens que mais faltam | F2 | Application |
| RN-RC-08 | Só **Admin** cria, envia, cancela e recebe pedidos e faz ajustes de estoque. Atendente só registra venda com sabor, "Abri um balde" e (se liberado) contagem | F2 | Application |

## 7. Relatórios (Fase 3)

| ID | Regra | Fase |
|---|---|---|
| RN-RL-01 | Faturamento = soma das comandas **fechadas** (as `Estornada` ficam à parte, somadas como estornos no dia da venda) | F3 |
| RN-RL-02 | Períodos (dia/semana/mês) usam o dia comercial em `America/Sao_Paulo` | F3 |
| RN-RL-03 | Ranking por quantidade e por valor; itens livres agrupados como "Self-service / venda avulsa" | F3 |
| RN-RL-04 | Margem estimada = preço praticado − custo estimado (RN-PR-07); sem custo → "n/d", nunca zero | F3 |
| RN-RL-05 | Exportação em CSV/Excel de qualquer período | F3 |

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
