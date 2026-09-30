import type { LoginResponse } from '../api/tipos.ts'

// Token guardado no navegador até a validade do JWT (ExpiraEmUtc). Depois disso é descartado.
const CHAVE = 'sorveteria.painel.sessao'

export type Sessao = LoginResponse

/** Sessão salva, ou null se não houver ou se o token já venceu. */
export function lerSessao(): Sessao | null {
  try {
    const texto = localStorage.getItem(CHAVE)
    if (!texto) {
      return null
    }
    const sessao = JSON.parse(texto) as Sessao
    if (!(Date.parse(sessao.expiraEmUtc) > Date.now())) {
      localStorage.removeItem(CHAVE)
      return null
    }
    return sessao
  } catch {
    return null
  }
}

export function salvarSessao(sessao: Sessao): void {
  try {
    localStorage.setItem(CHAVE, JSON.stringify(sessao))
  } catch {
    // Navegador sem armazenamento (aba anônima restrita): a sessão vale só enquanto a página estiver aberta.
  }
}

export function limparSessao(): void {
  try {
    localStorage.removeItem(CHAVE)
  } catch {
    // Nada a limpar.
  }
}
