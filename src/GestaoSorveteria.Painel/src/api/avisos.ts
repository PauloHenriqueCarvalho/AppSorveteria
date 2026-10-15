import { requisicao } from './cliente.ts'
import type {
  AindaFaltaRequest,
  AvisoDto,
  AvisoSalvoResponse,
  ConfiguracaoLojaDto,
  CriarAvisoRequest,
  ResumoDoDiaResponse,
} from './tipos.ts'

export const CHAVE_AVISOS = ['avisos'] as const
export const CHAVE_CONFIGURACAO = ['configuracao'] as const

/** GET /api/avisos — abertos (urgente primeiro); `comFechados` traz também os fechados dos últimos 30 dias. */
export function listarAvisos(comFechados: boolean): Promise<AvisoDto[]> {
  return requisicao<AvisoDto[]>(`/api/avisos?abertos=${comFechados ? 'false' : 'true'}`)
}

/** POST /api/avisos — item com aviso aberto é atualizado, não duplicado (RN-AV-03). */
export function criarAviso(dados: CriarAvisoRequest): Promise<AvisoSalvoResponse> {
  return requisicao<AvisoSalvoResponse>('/api/avisos', { metodo: 'POST', corpo: dados })
}

/** POST /api/avisos/{id}/ainda-falta — a prioridade só sobe. */
export function aindaFalta(id: string, dados: AindaFaltaRequest): Promise<AvisoSalvoResponse> {
  return requisicao<AvisoSalvoResponse>(`/api/avisos/${encodeURIComponent(id)}/ainda-falta`, { metodo: 'POST', corpo: dados })
}

/** POST /api/avisos/{id}/fechar (Admin) — com motivo (RN-AV-05). */
export function fecharAviso(id: string, motivo: string): Promise<AvisoDto> {
  return requisicao<AvisoDto>(`/api/avisos/${encodeURIComponent(id)}/fechar`, { metodo: 'POST', corpo: { motivo } })
}

/** GET /api/avisos/resumo-do-dia?data= */
export function obterResumoDoDia(data: string): Promise<ResumoDoDiaResponse> {
  return requisicao<ResumoDoDiaResponse>(`/api/avisos/resumo-do-dia?data=${encodeURIComponent(data)}`)
}

export function obterConfiguracao(): Promise<ConfiguracaoLojaDto> {
  return requisicao<ConfiguracaoLojaDto>('/api/configuracoes')
}

export function salvarConfiguracao(dados: ConfiguracaoLojaDto): Promise<ConfiguracaoLojaDto> {
  return requisicao<ConfiguracaoLojaDto>('/api/configuracoes', { metodo: 'PUT', corpo: dados })
}
