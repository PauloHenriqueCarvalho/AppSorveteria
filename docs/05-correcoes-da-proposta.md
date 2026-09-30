# 05 — Correções da proposta (v2 → v3)

> **Nota (29/09/2026):** o desenvolvimento segue o melhor funcionamento do sistema, não a proposta ao pé da letra (docs/01, ADR-017). Durante o desenvolvimento e o piloto a hospedagem é **gratuita** (docs/07); os valores de servidor abaixo valem para depois do piloto.

Lista objetiva do que mudar no PDF `proposta_sorveteria_v2.pdf` antes de reapresentar à dona. Nada aqui é jargão para ela — é o que evita promessa que o sistema não cumpre.

## Alterações de texto

| Onde | Está | Trocar por | Motivo |
|---|---|---|---|
| Capa | "Elaborado em 03 de April de 2026" | "Elaborado em setembro de 2026 (v3)" | idioma misturado; nova versão |
| Seção 2 · Painel da Dona | "Computador em casa" | "No computador **ou no celular** (abre no navegador)" | painel web |
| Seção 5 · Fase 1 | "3 a 4 semanas" | "5 semanas" | Sprint 0 + 4 sprints (doc 04) |
| Seção 5 · Fase 1 · itens | (lista atual) | Acrescentar: "**Controle de caixa**: abertura com fundo de troco, sangria, fechamento com conferência" e "**Self-service e venda rápida por valor**" e "**Funciona sem internet** no balcão: as vendas ficam guardadas no celular e sobem sozinhas quando a conexão volta" | escopo real da Fase 1 |
| Seção 5 · Fase 2 | "Baixa automática no estoque a cada venda" | "Baixa automática dos **insumos** a cada venda (ex.: 60 g de morango por milk-shake), conforme a receita de cada produto cadastrada por você" | explicar ficha técnica; deixa claro que picolé pronto não tem baixa |
| Seção 5 · Fase 4 | "Versão web do painel para celular da dona" | remover (já entregue na Fase 1) | painel web desde o início |
| Cronograma | "Semana 1 a 4 / 5 a 7 / 8 a 9" | "Semana 1 a 5 / 6 a 8 / 9 a 10" | prazos ajustados |
| Seção 7 · Painel da dona | "Aplicativo Windows instalado no computador" | "Painel na internet, protegido por senha: abre no computador ou no celular, sem instalar nada e sempre atualizado" | painel web |
| Seção 7 · Banco de dados | "SQL Server — o mesmo banco…" | "PostgreSQL — banco profissional usado por empresas de todos os tamanhos" | decisão técnica |
| Seção 7 · Backup | "Os dados ficam salvos no servidor…" | "Os dados ficam salvos na nuvem, com **cópia de segurança automática todo dia**. Mesmo que o celular quebre, nada se perde." | promessa com mecanismo |
| Seção 8 · Próximos passos | item 3 "forma de pagamento pelo desenvolvimento" | manter; acrescentar item "Confirmar se aceita cartão e quais formas de pagamento aparecem no app" | RN-PG-01 |
| Seção 9 · Servidor | "R$ 0 a R$ 360/ano — Grátis para começar" | "R$ 30 a R$ 50/mês (≈ R$ 360 a R$ 600/ano)" | Railway não tem mais plano gratuito permanente; Hobby ≈ US$ 5/mês + uso |
| Seção 9 · Total primeiro ano | "R$ 1.950 a R$ 2.460" | recalcular: 1.300 + celular (650–800) + servidor (360–600) = **R$ 2.310 a R$ 2.700 sem suporte**; com suporte (+960) **R$ 3.270 a R$ 3.660** | coerência |

## Escopo — o que entrou na Fase 1 sem mudar o preço

- Módulo de caixa (era um "problema" listado sem solução).
- Painel web em vez de programa instalado (menos trabalho de manutenção para você, mais valor para ela).
- Vendas offline com sincronização (era promessa da proposta; agora tem projeto).
- Cartão como forma de pagamento (se ela aceitar).
- Cancelamento e estorno com motivo (auditoria).

Decisão sua: manter R$ 600 na Fase 1 (relação de confiança) ou ajustar. Em qualquer caso, **formalize o escopo por escrito** com a lista da Fase 1 acima — evita "achei que vinha X".

## Perguntas para a conversa de alinhamento (seção 8)

1. Produtos, categorias e preços atuais (planilha ou foto do cardápio).
2. Aceita cartão? Débito, crédito, ambos? Vale-refeição?
3. Self-service: cobra por peso? Tem balança? A balança imprime o valor?
4. Insumos que mais faltam e em que unidade compra (kg, litro, caixa).
5. Quem abre e fecha o caixa? Tem fundo de troco fixo? Faz sangria durante o dia?
6. Delivery: próprio, por WhatsApp, iFood? Precisa anotar endereço no pedido?
7. Internet no balcão: Wi-Fi da loja? Cai muito?
8. Celular: comprar novo (Moto G) ou usar um existente (Android 8 ou superior)?
9. Horário de pico e dias de maior movimento (para planejar os testes reais).
10. Ela quer receber um resumo do dia por WhatsApp/e-mail? (Fase 4, mas bom saber.)

## O que dizer sobre "por que mudou"

> "Revisei o projeto antes de começar a construir. Três coisas melhoraram para você: o painel abre no seu celular também (não só no computador), o sistema controla o caixa — abertura, retiradas e fechamento com conferência — e as vendas continuam funcionando se a internet cair. O prazo da primeira etapa fica em 5 semanas e o custo do servidor é de uns R$ 30 a R$ 50 por mês."
