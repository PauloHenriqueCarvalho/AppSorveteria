import { requisicao } from './cliente.ts'
import type { ComandaDetalheDto, ComandaResumoDto, PaginaDto, StatusComanda } from './tipos.ts'

export const CHAVE_COMANDAS = ['comandas'] as const

export interface FiltroVendas {
  /** Dia comercial "2026-09-30"; vazio = hoje (a API decide). */
  dia?: string
  status?: StatusComanda
  pagina?: number
}

export const VENDAS_POR_PAGINA = 30

/** GET /api/comandas — vendas de um dia, mais recente primeiro. */
export function listarComandas({ dia, status, pagina = 1 }: FiltroVendas): Promise<PaginaDto<ComandaResumoDto>> {
  const parametros = new URLSearchParams({ pagina: String(pagina), tamanho: String(VENDAS_POR_PAGINA) })
  if (dia) {
    parametros.set('de', dia)
    parametros.set('ate', dia)
  }
  if (status) {
    parametros.set('status', status)
  }
  return requisicao<PaginaDto<ComandaResumoDto>>(`/api/comandas?${parametros}`)
}

/** GET /api/comandas/{id} */
export function obterComanda(id: string): Promise<ComandaDetalheDto> {
  return requisicao<ComandaDetalheDto>(`/api/comandas/${encodeURIComponent(id)}`)
}

/** POST /api/comandas/{id}/estornar (Admin) — RN-CM-09. Não altera o caixa. */
export function estornarComanda(id: string, motivo: string): Promise<ComandaDetalheDto> {
  return requisicao<ComandaDetalheDto>(`/api/comandas/${encodeURIComponent(id)}/estornar`, {
    metodo: 'POST',
    corpo: { motivo },
  })
}
