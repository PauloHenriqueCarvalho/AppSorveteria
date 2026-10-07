import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { definirAoSessaoTerminar, MENSAGEM_SESSAO_TERMINOU } from '../api/cliente.ts'
import type { LoginResponse } from '../api/tipos.ts'
import { limparSessao, lerSessao, salvarSessao, type Sessao } from './armazenamento.ts'
import { SessaoContext, type ContextoSessao } from './contexto.ts'

// setTimeout aceita no máximo ~24,8 dias; o token dura 12 h.
const MAIOR_ESPERA_MS = 2_147_483_647

export default function SessaoProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()
  const [sessao, setSessao] = useState<Sessao | null>(() => lerSessao())
  const [aviso, setAviso] = useState<string | null>(null)

  const sair = useCallback(
    (novoAviso?: string) => {
      limparSessao()
      queryClient.clear()
      setSessao(null)
      setAviso(novoAviso ?? null)
    },
    [queryClient],
  )

  const entrar = useCallback((resposta: LoginResponse) => {
    salvarSessao(resposta)
    setAviso(null)
    setSessao(resposta)
  }, [])

  // A API recusou o token (401) → volta para o login.
  useEffect(() => {
    definirAoSessaoTerminar(() => sair(MENSAGEM_SESSAO_TERMINOU))
    return () => definirAoSessaoTerminar(null)
  }, [sair])

  // Sai sozinho quando o token vence, mesmo sem nenhuma chamada à API.
  useEffect(() => {
    if (!sessao) {
      return
    }
    const restante = Date.parse(sessao.expiraEmUtc) - Date.now()
    const id = setTimeout(() => sair(MENSAGEM_SESSAO_TERMINOU), Math.min(Math.max(restante, 0), MAIOR_ESPERA_MS))
    return () => clearTimeout(id)
  }, [sessao, sair])

  const valor = useMemo<ContextoSessao>(
    () => ({ usuario: sessao?.usuario ?? null, aviso, entrar, sair }),
    [sessao, aviso, entrar, sair],
  )

  return <SessaoContext.Provider value={valor}>{children}</SessaoContext.Provider>
}
