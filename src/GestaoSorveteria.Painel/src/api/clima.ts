import { requisicao } from './cliente.ts'
import type { ClimaVendasDto, ColetaClimaDto, DefinirLocalLojaRequest, LocalLojaDto, SituacaoClimaDto } from './tipos.ts'

export const CHAVE_CLIMA = ['clima'] as const

/** GET /api/clima/situacao — coleta ligada, local da loja, última coleta e o que já está guardado (RN-CL-04/07). */
export function obterSituacaoClima(): Promise<SituacaoClimaDto> {
  return requisicao<SituacaoClimaDto>('/api/clima/situacao')
}

/** GET /api/clima/procurar-local?texto= — o lugar que o serviço de clima encontrou, para a dona conferir (RN-CL-01). */
export function procurarLocal(texto: string): Promise<LocalLojaDto> {
  return requisicao<LocalLojaDto>(`/api/clima/procurar-local?texto=${encodeURIComponent(texto)}`)
}

/** PUT /api/clima/local — grava o local confirmado e já busca o histórico e a previsão. */
export function definirLocal(dados: DefinirLocalLojaRequest): Promise<SituacaoClimaDto> {
  return requisicao<SituacaoClimaDto>('/api/clima/local', { metodo: 'PUT', corpo: dados })
}

/** POST /api/clima/atualizar — coleta agora, sem esperar as 3 h. */
export function atualizarClima(): Promise<ColetaClimaDto> {
  return requisicao<ColetaClimaDto>('/api/clima/atualizar', { metodo: 'POST' })
}

/** GET /api/relatorios/clima-vendas — cada dia com clima e vendas, médias e próximos dias (até 366 dias, RN-CL-06). */
export function obterClimaVendas(de: string, ate: string): Promise<ClimaVendasDto> {
  return requisicao<ClimaVendasDto>(`/api/relatorios/clima-vendas?${new URLSearchParams({ de, ate })}`)
}
