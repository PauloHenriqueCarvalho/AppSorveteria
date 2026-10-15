// Rótulos do relatório de período (Sprint 8). Só formata as datas que a API devolveu: nada de dinheiro é calculado aqui.
import type { AgrupamentoRelatorio } from './api/tipos.ts'

const MESES = ['jan', 'fev', 'mar', 'abr', 'mai', 'jun', 'jul', 'ago', 'set', 'out', 'nov', 'dez']
const DIAS = ['dom', 'seg', 'ter', 'qua', 'qui', 'sex', 'sáb']

function partes(dia: string) {
  const [ano, mes, d] = dia.split('-').map(Number)
  return { ano, mes, dia: d, semana: new Date(Date.UTC(ano, mes - 1, d)).getUTCDay() }
}

const ddMM = (dia: string) => `${dia.slice(8, 10)}/${dia.slice(5, 7)}`

function ultimoDiaDoMes(ano: number, mes: number) {
  return new Date(Date.UTC(ano, mes, 0)).getUTCDate()
}

/** "qua 14/10", "05/10 a 11/10", "out/26" (mês inteiro) ou "out/26 (01 a 14)". */
export function descreverTrecho(t: { de: string; ate: string }, agrupamento: AgrupamentoRelatorio): string {
  const de = partes(t.de)
  if (agrupamento === 'Dia') {
    return `${DIAS[de.semana]} ${ddMM(t.de)}`
  }
  if (agrupamento === 'Mes') {
    const ate = partes(t.ate)
    const mes = `${MESES[de.mes - 1]}/${String(de.ano).slice(2)}`
    const inteiro = de.dia === 1 && ate.dia === ultimoDiaDoMes(ate.ano, ate.mes)
    return inteiro ? mes : `${mes} (${t.de.slice(8, 10)} a ${t.ate.slice(8, 10)})`
  }
  return t.de === t.ate ? ddMM(t.de) : `${ddMM(t.de)} a ${ddMM(t.ate)}`
}

/** Rótulo curto embaixo da coluna do gráfico: "05/10" ou "out". */
export function rotuloCurto(t: { de: string }, agrupamento: AgrupamentoRelatorio): string {
  return agrupamento === 'Mes' ? MESES[partes(t.de).mes - 1] : ddMM(t.de)
}

const DIAS_POR_EXTENSO = ['Domingo', 'Segunda', 'Terça', 'Quarta', 'Quinta', 'Sexta', 'Sábado']

/** 0 → "Domingo". */
export function nomeDoDiaDaSemana(dia: number): string {
  return DIAS_POR_EXTENSO[dia] ?? String(dia)
}

/** "01/10 a 14/10/2026" (ou só "14/10/2026"). */
export function descreverPeriodo(de: string, ate: string): string {
  const ano = (dia: string) => `${ddMM(dia)}/${dia.slice(0, 4)}`
  if (de === ate) {
    return ano(de)
  }
  return de.slice(0, 4) === ate.slice(0, 4) ? `${ddMM(de)} a ${ano(ate)}` : `${ano(de)} a ${ano(ate)}`
}

/** "01/09 a 14/09" (sem o ano), para os textos curtos dos indicadores. */
export function descreverPeriodoCurto(de: string, ate: string): string {
  return de === ate ? ddMM(de) : `${ddMM(de)} a ${ddMM(ate)}`
}

/** Último dia do mês anterior a `dia`: "2026-10-14" → "2026-09-30". */
export function fimDoMesAnterior(dia: string): string {
  const { ano, mes } = partes(dia)
  return new Date(Date.UTC(ano, mes - 1, 0)).toISOString().slice(0, 10)
}

/** Primeiro dia do mês `meses` antes: ("2026-10-14", 2) → "2026-08-01". */
export function inicioDoMesAntes(dia: string, meses: number): string {
  const { ano, mes } = partes(dia)
  return new Date(Date.UTC(ano, mes - 1 - meses, 1)).toISOString().slice(0, 10)
}
