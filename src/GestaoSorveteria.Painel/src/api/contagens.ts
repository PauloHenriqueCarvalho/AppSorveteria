import { requisicao } from './cliente.ts'
import type { ContagemDto, ContagemResumoDto, SalvarContagemRequest } from './tipos.ts'

export const CHAVE_CONTAGENS = ['contagens'] as const

/** GET /api/estoque/contagens — últimos 60 dias e as abertas. */
export function listarContagens(): Promise<ContagemResumoDto[]> {
  return requisicao<ContagemResumoDto[]>('/api/estoque/contagens')
}

/** GET /api/estoque/contagens/nova — modelo (nada gravado), com a contagem anterior como referência. */
export function novaContagem(): Promise<ContagemDto> {
  return requisicao<ContagemDto>('/api/estoque/contagens/nova')
}

export function obterContagem(id: string): Promise<ContagemDto> {
  return requisicao<ContagemDto>(`/api/estoque/contagens/${encodeURIComponent(id)}`)
}

/** POST /api/estoque/contagens — salva parcial ou conclui (RN-ES-05). */
export function salvarContagem(dados: SalvarContagemRequest): Promise<ContagemDto> {
  return requisicao<ContagemDto>('/api/estoque/contagens', { metodo: 'POST', corpo: dados })
}
