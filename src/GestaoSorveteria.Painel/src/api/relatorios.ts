import { requisicao } from './cliente.ts'
import type { AgrupamentoRelatorio, RelatorioPeriodoDto, ResumoDiaDto } from './tipos.ts'

export const CHAVE_RELATORIOS = ['relatorios'] as const

/** GET /api/relatorios/dia?data= — sem dia, a API usa hoje (horário de Brasília). */
export function obterResumoDoDia(dia?: string): Promise<ResumoDiaDto> {
  const consulta = dia ? `?data=${encodeURIComponent(dia)}` : ''
  return requisicao<ResumoDiaDto>(`/api/relatorios/dia${consulta}`)
}

/**
 * GET /api/relatorios/periodo — vendas de um período (até 366 dias, sem passar de hoje). Sem agrupamento, a API escolhe
 * pelo tamanho (dia até 31 dias, semana até 92, mês acima).
 */
export function obterRelatorioPeriodo(de: string, ate: string, agrupar?: AgrupamentoRelatorio): Promise<RelatorioPeriodoDto> {
  const parametros = new URLSearchParams({ de, ate })
  if (agrupar) {
    parametros.set('agrupar', agrupar === 'Mes' ? 'mes' : agrupar.toLowerCase())
  }
  return requisicao<RelatorioPeriodoDto>(`/api/relatorios/periodo?${parametros}`)
}
