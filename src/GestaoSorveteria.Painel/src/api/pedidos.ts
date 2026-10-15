import { requisicao } from './cliente.ts'
import type {
  EnviarPedidoResponse,
  HistoricoFornecedorDto,
  LinhaAlteradaRequest,
  MensagensPedidoDto,
  PedidoDto,
  PedidoResumoDto,
  ReceberPedidoRequest,
  ReceberPedidoResponse,
} from './tipos.ts'

export const CHAVE_PEDIDOS = ['pedidos'] as const

const id = (valor: string) => encodeURIComponent(valor)

/** GET /api/pedidos-fornecedor — últimos 90 dias e os que ainda não chegaram, do mais novo para o mais antigo. */
export function listarPedidos(): Promise<PedidoResumoDto[]> {
  return requisicao<PedidoResumoDto[]>('/api/pedidos-fornecedor')
}

export function obterPedido(pedidoId: string): Promise<PedidoDto> {
  return requisicao<PedidoDto>(`/api/pedidos-fornecedor/${id(pedidoId)}`)
}

/**
 * POST /api/pedidos-fornecedor/montar (RN-PF-04): monta ou remonta o rascunho com a sugestão de agora; o que a dona mudou
 * fica. `automatico` (o painel sozinho no dia do pedido): se o pedido de hoje já foi enviado, a API devolve ele.
 */
export function montarPedido(fornecedorId: string, automatico = false): Promise<PedidoDto> {
  return requisicao<PedidoDto>(`/api/pedidos-fornecedor/montar?fornecedorId=${id(fornecedorId)}&automatico=${automatico}`, {
    metodo: 'POST',
  })
}

/** PUT /api/pedidos-fornecedor/{id}/itens — +/− da dona; item fora do pedido é acrescentado. */
export function alterarItensPedido(pedidoId: string, linhas: LinhaAlteradaRequest[]): Promise<PedidoDto> {
  return requisicao<PedidoDto>(`/api/pedidos-fornecedor/${id(pedidoId)}/itens`, { metodo: 'PUT', corpo: { linhas } })
}

/** POST /api/pedidos-fornecedor/{id}/enviar (RN-PF-06): depois disso o pedido não muda mais. */
export function enviarPedido(pedidoId: string): Promise<EnviarPedidoResponse> {
  return requisicao<EnviarPedidoResponse>(`/api/pedidos-fornecedor/${id(pedidoId)}/enviar`, { metodo: 'POST' })
}

/** POST /api/pedidos-fornecedor/{id}/cancelar (RN-PF-08) — com motivo. */
export function cancelarPedido(pedidoId: string, motivo: string): Promise<PedidoDto> {
  return requisicao<PedidoDto>(`/api/pedidos-fornecedor/${id(pedidoId)}/cancelar`, { metodo: 'POST', corpo: { motivo } })
}

/** POST /api/pedidos-fornecedor/{id}/receber (RN-RC-01…05). */
export function receberPedido(pedidoId: string, dados: ReceberPedidoRequest): Promise<ReceberPedidoResponse> {
  return requisicao<ReceberPedidoResponse>(`/api/pedidos-fornecedor/${id(pedidoId)}/receber`, { metodo: 'POST', corpo: dados })
}

/** GET /api/pedidos-fornecedor/{id}/mensagens — WhatsApp do pedido e das diferenças da entrega. */
export function obterMensagensPedido(pedidoId: string): Promise<MensagensPedidoDto> {
  return requisicao<MensagensPedidoDto>(`/api/pedidos-fornecedor/${id(pedidoId)}/mensagens`)
}

/** GET /api/fornecedores/{id}/historico (RN-RC-06). */
export function obterHistoricoFornecedor(fornecedorId: string): Promise<HistoricoFornecedorDto> {
  return requisicao<HistoricoFornecedorDto>(`/api/fornecedores/${id(fornecedorId)}/historico`)
}
