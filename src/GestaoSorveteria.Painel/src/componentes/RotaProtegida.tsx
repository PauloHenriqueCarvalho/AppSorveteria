import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { Navigate, Outlet } from 'react-router'
import { obterUsuarioAtual } from '../api/auth.ts'
import { mensagemDoErro } from '../api/erros.ts'
import { PERFIL_ADMIN } from '../api/tipos.ts'
import { MENSAGEM_SO_ADMIN, useSessao } from '../sessao/contexto.ts'
import ConectandoServidor from './ConectandoServidor.tsx'

/** Só deixa passar quem está logado como Admin. Ao abrir, confere o token na API (e espera ela acordar). */
export default function RotaProtegida() {
  const { usuario, sair } = useSessao()

  const eu = useQuery({
    queryKey: ['auth', 'me'],
    queryFn: obterUsuarioAtual,
    enabled: usuario !== null,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  })

  const naoEAdmin = eu.data !== undefined && eu.data.perfil !== PERFIL_ADMIN
  useEffect(() => {
    if (naoEAdmin) {
      sair(MENSAGEM_SO_ADMIN)
    }
  }, [naoEAdmin, sair])

  if (!usuario) {
    return <Navigate to="/login" replace />
  }

  if (eu.isPending) {
    return (
      <main className="tela-central">
        <ConectandoServidor falhas={eu.failureCount} />
      </main>
    )
  }

  if (eu.isError) {
    return (
      <main className="tela-central">
        <div className="cartao">
          <p className="alerta alerta-erro">{mensagemDoErro(eu.error)}</p>
          <button type="button" className="botao" onClick={() => void eu.refetch()}>
            Tentar de novo
          </button>
        </div>
      </main>
    )
  }

  if (naoEAdmin) {
    return null
  }

  return <Outlet />
}
