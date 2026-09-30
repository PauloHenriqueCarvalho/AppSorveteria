# 06 — Análise do MVP MAUI (AppSorveteria) e decisão

> Repositório: `github.com/PauloHenriqueCarvalho/AppSorveteria` — 2 commits (28/02/2026 e 14/09/2026), projeto `SorveteriaMaui` (.NET 10 MAUI + sqlite-net).
> Análise de 29/09/2026.

## 1. Veredito

**Continuar em cima do MVP é a melhor opção para o app do atendente** — mas refatorando, não usando como está.

Por quê:

- Já tem os fluxos que a dona pediu e que nenhum outro documento tinha implementado: comanda com nome do cliente, **self-service por valor**, **venda rápida**, troco, lista de comandas abertas, cadastro de produtos com os produtos e preços reais.
- É **local-first** (SQLite no celular, sincroniza depois). É o modelo certo para balcão: a venda nunca trava por falta de internet.
- A tela já está no padrão MVVM com injeção de dependência; dá para evoluir sem jogar fora.
- Reescrever do zero custaria de 1 a 2 semanas só para chegar no mesmo ponto de tela.

O que **não** dá para aproveitar como está: a camada de dados/cálculo (`DatabaseService` + `PaymentService`) tem bugs que geram **valor errado de venda** — exatamente o problema "a conta não bate" que o sistema promete resolver.

### Alternativas consideradas

| Opção | Por que não |
|---|---|
| Reescrever o app do zero | Joga fora telas e fluxos prontos; mesmo destino final |
| App web/PWA para o atendente | Offline em navegador é frágil; Paulo já domina MAUI |
| Blazor Hybrid | Mais uma tecnologia sem ganho real para 5 telas |
| Usar o MVP sem mexer | Bugs de dinheiro abaixo inviabilizam produção |

## 2. Bugs encontrados (por gravidade)

### Críticos — geram valor errado ou venda perdida

| # | Onde | Problema | Efeito |
|---|---|---|---|
| B1 | `ListarComandasViewModel.ExecutarVendaRapida` | `CriarComanda` devolve o objeto com `Total = 0`; o item é gravado só no banco; `ProcessarPagamentoAsync(comanda)` recebe o objeto com total zero e recusa | **Venda rápida nunca funciona**: mostra "valor zero" e deixa uma comanda "Venda Rápida" aberta |
| B2 | `PaymentService` | `Pagamento.Valor = valorRecebido` (o que o cliente entregou), troco não é gravado | Cliente paga R$ 12,50 com nota de 50 → faturamento registra **R$ 50** |
| B3 | `PaymentService` | `if (troco < 0) troco = 0` | Aceita receber **menos** que o total em dinheiro e fecha a comanda |
| B4 | `DetalhesComandaViewModel` (+/−) | Salva a comanda com o total antigo, recarrega do banco e recalcula só em memória | Total no banco fica **desatualizado**; "Finalizar" pela lista cobra o valor antigo |
| B5 | `DatabaseService.RemoverProdutoDaComanda` | `Total = soma dos itens`, ignora acréscimo/desconto e não atualiza `Subtotal` | Total inconsistente depois de remover item |
| B6 | Todo o código | Dinheiro em `double` | Erros de centavos em somas (0,1 + 0,2 ≠ 0,3) |
| B7 | `DatabaseService` (todos os métodos) | `catch { Console.WriteLine }` e segue | Falha ao gravar venda é **silenciosa** — o atendente acha que vendeu |

### Importantes

| # | Problema |
|---|---|
| ~~B8~~ | ~~Número da comanda = `Comandas.Count + 1` (só as abertas) → números repetidos depois de fechar uma~~ — corrigido em c93fddd (#15): sequencial dentro do caixa, contando as fechadas (RN-CM-02) |
| B9 | Self-service e venda rápida criam `ProdutoId = "MANUAL_..."/"RAPIDA_..."` que não existe → vai quebrar a sincronização (chave estrangeira) |
| B10 | Não existe cancelar comanda (status 2 existe, mas nenhuma tela usa) — comanda aberta por engano fica para sempre |
| B11 | `DisplayAlert("Sucesso")` a cada item adicionado → um toque extra por item (contra a meta de rapidez) |
| B12 | Busca de produto não funciona: a tela faz binding em `FiltroNome` / `BuscarCommand`, que não existem no ViewModel |
| B13 | Categorias inconsistentes: seed usa `Tipo` 0=Sorvete, 1=Açaí, 2=Bebida; cadastro usa índice de "Picolé, Pote, Bebida, Acompanhamento, Self-Service" |
| B14 | Datas em `DateTime.Now` (hora local) → sincronização precisa de UTC |
| B15 | Não há caixa, login, delivery, histórico do turno, pagamento dividido nem sincronização implementada (só os campos) |
| B16 | Cadastro de produto no app grava direto no SQLite sem passar pelo `Produto` do Domain: aceita nome com 1 caractere e nome repetido (RN-PR-01). O produto aparece na tela de venda, mas o Domain recusa ao adicionar ("O nome do produto deve ter pelo menos 2 caracteres."); o nome repetido fica com dois botões iguais e preços diferentes. Passos na seção 2.1 |

### Menores

`Application.Current.MainPage` e `Frame` estão obsoletos no .NET 10 · `edit_icon.png` não existe em `Resources/Images` · compila para iOS/Mac sem necessidade · `ApplicationId = com.companyname.sorveteriamaui` · UI (`DisplayAlert`) dentro de serviço · carregamento duplo em `ListarComandasViewModel` (construtor + `OnAppearing`) · pasta `Contexto/` com 3 cópias divergentes das regras dentro do projeto do app.

Vistos no tablet em 30/09/2026: "Valor recebido" em dinheiro vem pré-preenchido com ponto (`6.50`), o resto do app usa vírgula · botões − e + do resumo da comanda quase invisíveis (cinza-claro sobre cinza) · no self-service com valor inválido o botão diz "Tentar novamente", mas só fecha o aviso · fechar comanda vazia pergunta a forma de pagamento antes de avisar que não tem itens, e o aviso manda "cancelar a comanda", que ainda não existe (B10) · produto novo entra com `Ordem = 0` e empata com o primeiro do seed · aviso XA0141 no build: `libe_sqlite3.so` (SQLitePCLRaw 2.1.2) sem página de 16 KB — o tablet de teste já está no Android 16 e o app rodou normal, mas o Google Play vai exigir.

### 2.1 Teste no tablet (30/09/2026)

Galaxy Tab SM-X230, Android 16, develop em 03cabeb (o commit seguinte, 4276cd7, só mexe na API e em partes do Domain que o app não usa). Etapa A conferida: B1, B2, B3, B4, B5, B6, B8, B9, B13 e B14 corrigidos no aparelho, inclusive em modo avião e depois de fechar e reabrir o app; B7 conferido no código (o único `catch` é o de `Services/Operacao.cs`, que mostra alerta e para o fluxo).

**B16 — como reproduzir**

1. Aba **Produtos** → **Novo Produto** → Nome `X`, Preço `1,00` → **Salvar**. Esperado: recusar (nome de 2 a 80 caracteres). Obtido: salva e aparece na lista.
2. Abrir uma comanda → **+ Produto** → **+ Add** no `X`. Obtido: "Atenção — O nome do produto deve ter pelo menos 2 caracteres." O produto não pode ser vendido; só sai da tela de venda se alguém editar o nome, porque o app não tem desativar produto.
3. **Novo Produto** → Nome `sorvete de fruta`, Preço `9,00` → **Salvar**. Esperado: recusar (já existe "Sorvete de Fruta"). Obtido: salva; a tela de venda mostra dois "Sorvete de Fruta" (R$ 2,00 e R$ 9,00).

Causa: `CadastroProdutoViewModel.Salvar` só confere nome vazio e preço, e grava pelo `ProdutoRepository.SalvarAsync` sem criar o `Produto` do Domain nem checar nome repetido. Some quando o catálogo passar a vir da API (Etapa C); até lá, o cadastro local deveria validar pelo `Produto.Criar`/`Atualizar` e checar nome repetido com `Produto.NormalizarNome`.

## 3. O que o MVP ensina — e muda no desenho do sistema

O MVP adota **local-first**: a comanda vive no celular do início ao fim; o servidor só recebe a **venda pronta**. Isso é mais simples e mais robusto do que o protocolo de fila de operações do `docs/03` (seção 8). **Adotado:**

| Antes (docs/03) | Agora |
|---|---|
| App chama `POST /api/comandas`, `/itens`, `/fechar`... uma a uma (fila de operações) | Comanda aberta existe **só no celular**. Ao fechar/cancelar, o app envia **o documento inteiro** (comanda + itens + pagamentos) |
| ~10 endpoints de comanda | `POST /api/sync/comandas` (lote, idempotente pelo `Id`) + `POST /api/sync/caixas` + `GET /api/produtos` |
| Numeração de comanda pelo servidor | Numeração pelo celular, sequencial dentro do caixa (resolve B8) |
| Sincronização "agendada" (MVP) | Envio **logo após fechar**, se houver internet; fila pendente reenviada a cada 1 min e ao abrir o app. A dona vê a venda em segundos |
| Caixa aberto/fechado pela API | Caixa aberto e fechado **no celular**, enviado ao servidor ao fechar (e o painel mostra "caixa aberto" pelo último envio) |

Consequência para o Sprint 0 já feito: **o domínio não muda** (as entidades `Comanda`, `Caixa`, `Pagamento` servem igual). Muda o Sprint 1 da API: em vez dos endpoints por operação, um serviço de **recebimento de comandas sincronizadas** que reconstrói o agregado com as mesmas regras e grava.

### Compartilhar regras entre celular e servidor

`GestaoSorveteria.Domain` e `GestaoSorveteria.Contracts` não têm dependências e são `net10.0` — o projeto MAUI pode referenciá-los. Assim o **cálculo de total, troco e fechamento de caixa é o mesmo código** no celular e no servidor (e já tem 100 testes). O SQLite do app guarda tabelas simples (`sqlite-net`), convertidas para as entidades do domínio na hora de calcular e para os DTOs de `Contracts` na hora de sincronizar.

## 4. Plano de refatoração (substitui o Sprint 2 do docs/04)

**Repositório:** usar o `AppSorveteria` como repositório único. Mover o app para `src/GestaoSorveteria.Mobile/` com `git mv` (mantém o histórico) e adicionar a solução do Sprint 0 ao lado. Resolve também o "git init" pendente.

### Etapa A — Base sólida (2–3 dias)
- [x] Monorepo: `src/GestaoSorveteria.Mobile` na `GestaoSorveteria.slnx`; só `net10.0-android` (+ `net10.0-windows` para testar no PC); `ApplicationId = br.com.sorveteria.atendente`
- [x] Referenciar `Domain` e `Contracts`
- [x] Modelos SQLite com `decimal`, datas UTC, enums em vez de `int` mágico, `ProdutoId` nulo para item livre (B6, B9, B13, B14)
- [x] Substituir `DatabaseService` por repositórios pequenos + `ComandaAppService` que carrega → chama o domínio → grava tudo numa transação (B1, B4, B5)
- [x] Erros visíveis: gravação que falha mostra alerta e não segue (B7)
- [x] Numeração da comanda sequencial dentro do caixa, contando as fechadas (B8, RN-CM-02)
- [x] Mover `Contexto/` para `docs/contexto-mvp/` (uma cópia só)

### Etapa B — Corrigir fluxos de venda (2 dias)
- [ ] Pagamento pelo domínio: `Valor` + `ValorRecebido` + `Troco`, recusa dinheiro insuficiente, pagamento dividido (B2, B3)
- [ ] Venda rápida funcionando em uma operação (B1)
- [ ] Cancelar comanda aberta com confirmação (B10)
- [ ] Toast em vez de `DisplayAlert` ao adicionar item; mesmo produto soma na linha (B11)
- [ ] Busca e filtro por categoria na seleção de produto (B12)
- [ ] Marcar Delivery + observação

### Etapa C — Caixa, login e sincronização (3–4 dias)
- [ ] Telas de abrir caixa (fundo de troco), sangria/suprimento, fechar caixa com conferência
- [ ] Login com PIN (valida online na 1ª vez, guarda token em `SecureStorage`)
- [ ] `SyncService`: envia comandas fechadas/canceladas e caixas pendentes; baixa produtos
- [ ] Indicador "X vendas aguardando envio"; fechar caixa com envio pendente é permitido, com aviso (RN-CX-05, RN-SY-05)
- [ ] Teste no celular real com internet desligada

Total: ~1,5 semana de trabalho — encaixa no Sprint 2 original.
