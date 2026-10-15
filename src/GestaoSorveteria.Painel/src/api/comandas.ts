import { requisicao } from './cliente.ts'
import type { ComandaDetalheDto, ComandaResumoDto, PaginaDto, StatusComanda } from './tipos.ts'

export const CHAVE_COMANDAS = ['comandas'] as const

export interface FiltroVendas {
  /** Dia comercial "2026-09-30"; vazio = hoje (a API decide). Atalho para de = ate = dia. */
  dia?: string
  /** Período (inclusive, máx. 93 dias). Tem prioridade sobre `dia`. */
  de?: string
  ate?: string
  status?: StatusComanda
  /** Id de quem vendeu. */
  atendente?: string
  /** Dinheiro, Pix, CartaoDebito ou CartaoCredito: venda com pelo menos um pagamento nessa forma. */
  forma?: string
  pagina?: number
  /** 1 a 100 (a API recusa acima). */
  tamanho?: number
}

export const VENDAS_POR_PAGINA = 30
export const MAXIMO_POR_PAGINA = 100

/** GET /api/comandas — vendas do período, mais recente primeiro. */
export function listarComandas({
  dia,
  de,
  ate,
  status,
  atendente,
  forma,
  pagina = 1,
  tamanho = VENDAS_POR_PAGINA,
}: FiltroVendas): Promise<PaginaDto<ComandaResumoDto>> {
  const parametros = new URLSearchParams({ pagina: String(pagina), tamanho: String(tamanho) })
  const inicio = de ?? dia
  const fim = ate ?? dia
  if (inicio) {
    parametros.set('de', inicio)
  }
  if (fim) {
    parametros.set('ate', fim)
  }
  if (status) {
    parametros.set('status', status)
  }
  if (atendente) {
    parametros.set('atendente', atendente)
  }
  if (forma) {
    parametros.set('forma', forma)
  }
  return requisicao<PaginaDto<ComandaResumoDto>>(`/api/comandas?${parametros}`)
}

/** Todas as vendas do período, página a página (para exportar). Para no limite de segurança de 5.000. */
export async function listarTodasAsComandas(filtro: Omit<FiltroVendas, 'pagina' | 'tamanho'>): Promise<ComandaResumoDto[]> {
  const todas: ComandaResumoDto[] = []
  for (let pagina = 1; pagina <= 50; pagina++) {
    const resultado = await listarComandas({ ...filtro, pagina, tamanho: MAXIMO_POR_PAGINA })
    todas.push(...resultado.itens)
    if (todas.length >= resultado.totalItens || resultado.itens.length === 0) {
      break
    }
  }
  return todas
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
