import { createContext, useContext } from 'react'
import type { LoginResponse, UsuarioDto } from '../api/tipos.ts'

export const MENSAGEM_SO_ADMIN = 'Este painel é só para a administração da loja. Entre com o usuário da dona.'

export interface ContextoSessao {
  /** Usuário logado, ou null na tela de login. */
  usuario: UsuarioDto | null
  /** Recado para a tela de login (ex.: "Sua sessão terminou"). */
  aviso: string | null
  entrar: (resposta: LoginResponse) => void
  sair: (aviso?: string) => void
}

export const SessaoContext = createContext<ContextoSessao | null>(null)

export function useSessao(): ContextoSessao {
  const contexto = useContext(SessaoContext)
  if (!contexto) {
    throw new Error('useSessao precisa estar dentro de <SessaoProvider>.')
  }
  return contexto
}
