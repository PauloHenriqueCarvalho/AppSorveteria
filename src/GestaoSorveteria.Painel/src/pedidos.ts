// Pedido ao fornecedor na linguagem da dona: nomes de situação, dia de pedido, passo da conferência.
// Nenhuma conta de dinheiro aqui (ADR 018): totais, subtotais e diferenças em R$ vêm prontos da API.
import type { DiaSemana, OrigemLinhaPedido, SituacaoRecebimento, StatusPedido, UnidadeCompra } from './api/tipos.ts'
import type { Tom } from './componentes/Ui.tsx'
import { formatarEmbalagens, nomeDaEmbalagem } from './estoque.ts'
import { hojeNaLoja, somarDias } from './formatacao.ts'

export const STATUS_PEDIDO: Record<StatusPedido, { nome: string; tom: Tom }> = {
  Rascunho: { nome: 'Rascunho', tom: 'atencao' },
  Enviado: { nome: 'Enviado', tom: 'marca' },
  Recebido: { nome: 'Recebido', tom: 'bom' },
  Cancelado: { nome: 'Cancelado', tom: 'neutro' },
}

export const SITUACAO_RECEBIMENTO: Record<SituacaoRecebimento, { nome: string; tom: Tom }> = {
  Certo: { nome: 'Certo', tom: 'bom' },
  Faltou: { nome: 'Faltou', tom: 'ruim' },
  VeioAMais: { nome: 'Veio a mais', tom: 'atencao' },
  NaoPedido: { nome: 'Veio sem pedir', tom: 'atencao' },
}

/** Marca da linha no rascunho; a sugestão comum não leva marca. */
export const ORIGEM_LINHA: Record<OrigemLinhaPedido, { nome: string; tom: Tom } | null> = {
  Sugestao: null,
  Aviso: { nome: 'Aviso', tom: 'ruim' },
  FaltouAnterior: { nome: 'Faltou antes', tom: 'atencao' },
  AdicionadoPelaDona: { nome: 'Você acrescentou', tom: 'marca' },
  NaoPedido: { nome: 'Veio sem pedir', tom: 'atencao' },
}

const DIAS_DA_SEMANA: DiaSemana[] = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']

/** Dia comercial "2026-10-19" → "Monday" (sem depender do fuso do aparelho). */
export function diaDaSemana(dia: string): DiaSemana {
  return DIAS_DA_SEMANA[new Date(`${dia}T12:00:00Z`).getUTCDay()]
}

/** Próximo dia de pedido a partir de `hoje` (hoje conta). Null se o fornecedor não tem dia. */
export function proximoDiaDePedido(diasPedido: DiaSemana[], hoje: string): string | null {
  for (let d = 0; d < 7; d++) {
    const dia = somarDias(hoje, d)
    if (diasPedido.includes(diaDaSemana(dia))) {
      return dia
    }
  }
  return null
}

/** Dia comercial em que o pedido foi enviado mais o prazo: quando a entrega deve chegar. */
export function entregaPrevistaDe(enviadoEmUtc: string, prazoEntregaDias: number): string {
  return somarDias(hojeNaLoja(new Date(enviadoEmUtc)), prazoEntregaDias)
}

/** O pedido foi enviado hoje (no dia da loja)? */
export function enviadoHoje(enviadoEmUtc: string | null, hoje: string): boolean {
  return enviadoEmUtc !== null && hojeNaLoja(new Date(enviadoEmUtc)) === hoje
}

/** RN-RC-01: na conferência meia caixa sempre vale para caixa; balde, pacote e unidade vão inteiros. */
export function passoDeRecebimento(unidade: UnidadeCompra): number {
  return unidade === 'Caixa' ? 0.5 : 1
}

/** (2.5, "Caixa") → "2,5 caixas"; (1, "Balde") → "1 balde". */
export function descreverEmbalagens(quantidade: number, unidade: UnidadeCompra): string {
  return `${formatarEmbalagens(quantidade)} ${nomeDaEmbalagem(unidade, quantidade)}`
}

/** Última contagem: caixa mostra em unidades ("14 un."); balde e unidade, na própria embalagem ("3 baldes"). */
export function descreverContado(unidades: number | null, unidadesPorEmbalagem: number, unidade: UnidadeCompra): string {
  if (unidades === null) {
    return '—'
  }
  return unidadesPorEmbalagem > 1 ? `${unidades} un.` : descreverEmbalagens(unidades, unidade)
}

/** Situação da linha enquanto a dona confere (só compara quantidades). */
export function situacaoNaConferencia(pedido: number, veio: number): SituacaoRecebimento {
  if (pedido === 0) {
    return veio > 0 ? 'NaoPedido' : 'Certo'
  }
  return veio < pedido ? 'Faltou' : veio > pedido ? 'VeioAMais' : 'Certo'
}

/** 83.33 → "83,3%". */
export function formatarPercentual(valor: number | null): string {
  return valor === null ? '—' : `${valor.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`
}
