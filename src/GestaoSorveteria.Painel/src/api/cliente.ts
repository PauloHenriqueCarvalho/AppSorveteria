import { API_URL } from '../config.ts'
import { lerSessao } from '../sessao/armazenamento.ts'
import { ErroApi, ErroConexao } from './erros.ts'

// A API no Render Free leva ~1 min para acordar (docs/07): cada tentativa espera até 70 s.
const TEMPO_LIMITE_MS = 70_000

// Respostas do Render enquanto o serviço está subindo: tratar como "ainda conectando".
const STATUS_SERVIDOR_ACORDANDO = [502, 503, 504]

export const MENSAGEM_SESSAO_TERMINOU = 'Sua sessão terminou. Entre de novo.'

let aoSessaoTerminar: (() => void) | null = null

/** Chamado quando a API recusa o token (401): a sessão acabou e o painel volta para o login. */
export function definirAoSessaoTerminar(funcao: (() => void) | null): void {
  aoSessaoTerminar = funcao
}

interface Opcoes {
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  corpo?: unknown
  /** false no login: não manda token e um 401 não encerra a sessão. */
  autenticado?: boolean
}

export async function requisicao<T>(caminho: string, opcoes: Opcoes = {}): Promise<T> {
  const { metodo = 'GET', corpo, autenticado = true } = opcoes

  const cabecalhos: Record<string, string> = { Accept: 'application/json' }
  if (corpo !== undefined) {
    cabecalhos['Content-Type'] = 'application/json'
  }
  const sessao = autenticado ? lerSessao() : null
  if (autenticado && !sessao) {
    aoSessaoTerminar?.()
    throw new ErroApi(401, MENSAGEM_SESSAO_TERMINOU)
  }
  if (sessao) {
    cabecalhos.Authorization = `Bearer ${sessao.token}`
  }

  let resposta: Response
  try {
    resposta = await fetch(`${API_URL}${caminho}`, {
      method: metodo,
      headers: cabecalhos,
      body: corpo === undefined ? undefined : JSON.stringify(corpo),
      signal: AbortSignal.timeout(TEMPO_LIMITE_MS),
    })
  } catch {
    throw new ErroConexao()
  }

  if (STATUS_SERVIDOR_ACORDANDO.includes(resposta.status)) {
    throw new ErroConexao()
  }
  if (resposta.status === 401 && autenticado) {
    aoSessaoTerminar?.()
    throw new ErroApi(401, MENSAGEM_SESSAO_TERMINOU)
  }
  if (!resposta.ok) {
    throw await lerErro(resposta)
  }
  if (resposta.status === 204) {
    return undefined as T
  }
  return (await resposta.json()) as T
}

interface ProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

/** Converte o ProblemDetails da API (RFC 9457) numa mensagem para a tela. */
async function lerErro(resposta: Response): Promise<ErroApi> {
  if (resposta.status === 429) {
    return new ErroApi(429, 'Muitas tentativas seguidas. Espere um minuto e tente de novo.')
  }
  if (resposta.status === 403) {
    return new ErroApi(403, 'Você não tem permissão para fazer isso.')
  }

  let problema: ProblemDetails = {}
  try {
    problema = (await resposta.json()) as ProblemDetails
  } catch {
    // Resposta sem corpo JSON: fica a mensagem padrão.
  }

  // Erro de validação (400): a primeira mensagem já diz o que corrigir.
  const primeiraValidacao = problema.errors ? Object.values(problema.errors).flat()[0] : undefined
  const mensagem = primeiraValidacao ?? problema.title ?? 'O servidor não conseguiu atender o pedido. Tente de novo.'
  return new ErroApi(resposta.status, mensagem)
}
