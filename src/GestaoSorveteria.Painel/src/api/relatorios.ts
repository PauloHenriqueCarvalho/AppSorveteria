import { requisicao } from './cliente.ts'
import type { ResumoDiaDto } from './tipos.ts'

export const CHAVE_RELATORIOS = ['relatorios'] as const

/** GET /api/relatorios/dia?data= — sem dia, a API usa hoje (horário de Brasília). */
export function obterResumoDoDia(dia?: string): Promise<ResumoDiaDto> {
  const consulta = dia ? `?data=${encodeURIComponent(dia)}` : ''
  return requisicao<ResumoDiaDto>(`/api/relatorios/dia${consulta}`)
}
