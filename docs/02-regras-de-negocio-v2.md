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
| RN-US-07 | Usuário desativado não faz login, mas o histórico dele permanece. Usuário nunca é apagado | F1 | Domain + Application |
| RN-US-08 | Só Admin cria, edita, desativa usuários e redefine PIN/senha | F1 | Application |
| RN-US-09 | O sistema nasce com um Admin criado a partir da configuração (`Seed:AdminLogin` / `Seed:AdminSenha`); só é criado se não houver usuários | F1 | Infrastructure (seed) |

## 2. Produtos

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-PR-01 | Produto tem nome (2–80 caracteres, único sem diferenciar maiúsculas), categoria (obrigatória) e preço ≥ 0 | F1 | Domain (`Produto`) |
| RN-PR-02 | Produto com `PermiteValorLivre` (ex.: "Self-service por kg") aceita valor digitado pelo atendente na venda; o preço cadastrado é apenas sugestão (pode ser 0) | F1 | Domain |
| RN-PR-03 | Produto é **desativado**, nunca apagado, quando já foi vendido. Produto inativo não aparece no app: a API entrega os inativos na sincronização (`GET /api/produtos?desde=`) e o app esconde o botão | F1 | Application + App |
| RN-PR-04 | Alterar o preço não altera vendas passadas (o item guarda o preço praticado) | F1 | Domain (`ItemComanda`) |
| RN-PR-05 | `Ordem` define a posição do botão no app (mais vendidos primeiro) | F1 | Domain |
| RN-PR-06 | Produto pode ter **ficha técnica** (lista de insumo + quantidade). Produto sem ficha técnica não movimenta estoque (ex.: picolé comprado pronto, na Fase 2) | F2 | Domain |
| RN-PR-07 | Custo estimado do produto = soma (quantidade × custo do insumo) da ficha técnica; sem ficha técnica, custo informado manualmente | F3 | Application |

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
| RN-CX-09 | **(Sprint 4 — decisão de 30/09/2026)** Admin pode forçar o fechamento pelo painel (ex.: atendente esqueceu), informando valor contado 0 e motivo; a diferença fica registrada. Se depois chegar o fechamento feito no celular, ele é **rejeitado** com motivo (o caixa não muda, RN-CX-07; o valor contado no celular aparece no motivo) e a dona registra o ajuste no próximo caixa. Vendas feitas no celular depois do fechamento forçado são aceitas e marcadas (RN-CX-08) | F1 | Application |
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

## 6. Estoque de insumos (Fase 2)

| ID | Regra | Fase | Onde |
|---|---|---|---|
| RN-ES-01 | Insumo tem nome, unidade (g, kg, ml, L, un), quantidade atual, quantidade mínima e custo médio | F2 | Domain |
| RN-ES-02 | Ao **fechar** uma comanda, para cada item com produto que tenha ficha técnica, gera-se um movimento `BaixaVenda` por insumo (quantidade × ficha). Produto sem ficha técnica não baixa | F2 | Application |
| RN-ES-03 | Estorno gera movimento inverso (`EstornoVenda`) | F2 | Application |
| RN-ES-04 | **Entrada de reposição** registra quantidade, custo total e fornecedor (opcional); atualiza o custo médio do insumo | F2 | Domain |
| RN-ES-05 | **Ajuste** e **perda** exigem motivo; toda movimentação fica no histórico (nunca se edita `QuantidadeAtual` direto) | F2 | Domain |
| RN-ES-06 | Alerta quando `QuantidadeAtual ≤ QuantidadeMinima` ("Morango: 0,8 kg — repor!") | F2 | Application |
| RN-ES-07 | **Previsão de ruptura**: `consumo médio diário` = média dos últimos 14 dias com venda; `dias restantes = QuantidadeAtual ÷ consumo médio`. Sem histórico → "sem previsão" | F2 | Application |
| RN-ES-08 | Estoque negativo é permitido com aviso (a venda nunca é bloqueada por estoque; o estoque é informativo) | F2 | Domain |

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
| RN-TD-02 | Dinheiro em `decimal(12,2)`; nunca `double`/`float`; arredondamento `MidpointRounding.AwayFromZero` | F1 |
| RN-TD-03 | Nenhum registro de venda, pagamento, caixa ou movimento é apagado; correções geram novos registros com motivo | F1 |
| RN-TD-04 | Todo registro guarda quem fez (`UsuarioId`) e quando | F1 |

## 10. Perguntas abertas para confirmar com a dona

1. Aceita cartão (maquininha)? Quais bandeiras/formas devem aparecer?
2. Lista de produtos, categorias e preços atuais.
3. Quais insumos ela quer controlar primeiro (Fase 2) e em que unidade compra cada um.
4. Self-service é por peso? Existe balança com impressão de valor?
5. Quem abre e fecha o caixa no dia a dia? Existe fundo de troco fixo?
6. Delivery: próprio ou por app (iFood)? Precisa registrar endereço?
7. Internet no balcão: Wi-Fi da loja ou dados do celular? Cai com frequência?
8. Modelo do celular Android que ficará no balcão (versão mínima: Android 8).
