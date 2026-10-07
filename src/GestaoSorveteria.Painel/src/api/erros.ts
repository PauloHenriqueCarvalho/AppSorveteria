/** A API respondeu com erro (ProblemDetails). A mensagem já vem pronta para a tela. */
export class ErroApi extends Error {
  readonly status: number

  constructor(status: number, mensagem: string) {
    super(mensagem)
    this.name = 'ErroApi'
    this.status = status
  }
}

/** Sem resposta da API: sem internet, tempo esgotado ou servidor acordando (Render Free — docs/07). */
export class ErroConexao extends Error {
  constructor() {
    super('Não foi possível conectar ao servidor. Confira a internet do aparelho e tente de novo.')
    this.name = 'ErroConexao'
  }
}

/** Quantas vezes tentar de novo quando a API não responde (cada tentativa espera até 70 s). */
export const MAX_NOVAS_TENTATIVAS = 4

/** Espera entre uma tentativa e outra. */
export const ESPERA_ENTRE_TENTATIVAS_MS = 3_000

/** Só tenta de novo quando o servidor não respondeu; erro de regra ou de acesso não muda repetindo. */
export function deveTentarDeNovo(falhas: number, erro: unknown): boolean {
  return erro instanceof ErroConexao && falhas < MAX_NOVAS_TENTATIVAS
}

/** Texto para mostrar na tela para qualquer erro. */
export function mensagemDoErro(erro: unknown): string {
  if (erro instanceof ErroApi || erro instanceof ErroConexao) {
    return erro.message
  }
  return 'Algo deu errado. Tente de novo.'
}
