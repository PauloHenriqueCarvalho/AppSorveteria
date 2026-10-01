import { requisicao } from './cliente.ts'
import type { CaixaDetalheDto, CaixaResumoDto } from './tipos.ts'

export const CHAVE_CAIXAS = ['caixas'] as const

/** GET /api/caixas?de=&ate= — dias "2026-09-30"; sem datas a API devolve os últimos 30 dias. */
export function listarCaixas(de?: string, ate?: string): Promise<CaixaResumoDto[]> {
  const parametros = new URLSearchParams()
  if (de) {
    parametros.set('de', de)
  }
  if (ate) {
    parametros.set('ate', ate)
  }
  const consulta = parametros.size > 0 ? `?${parametros}` : ''
  return requisicao<CaixaResumoDto[]>(`/api/caixas${consulta}`)
}

/** GET /api/caixas/atual — null quando não há caixa aberto (204). */
export async function obterCaixaAtual(): Promise<CaixaResumoDto | null> {
  return (await requisicao<CaixaResumoDto | undefined>('/api/caixas/atual')) ?? null
}

/** GET /api/caixas/{id} */
export function obterCaixa(id: string): Promise<CaixaDetalheDto> {
  return requisicao<CaixaDetalheDto>(`/api/caixas/${encodeURIComponent(id)}`)
}
