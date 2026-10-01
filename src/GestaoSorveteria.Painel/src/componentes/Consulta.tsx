import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { mensagemDoErro } from '../api/erros.ts'
import ConectandoServidor from './ConectandoServidor.tsx'

interface Props<T> {
  consulta: UseQueryResult<T>
  children: (dados: T) => ReactNode
}

/** Mostra "Conectando…" enquanto carrega, o erro com "Tentar de novo", ou o conteúdo com os dados. */
export default function Consulta<T>({ consulta, children }: Props<T>) {
  if (consulta.isPending) {
    return <ConectandoServidor falhas={consulta.failureCount} />
  }
  if (consulta.isError) {
    return (
      <div className="cartao">
        <p className="alerta alerta-erro">{mensagemDoErro(consulta.error)}</p>
        <button type="button" className="botao" onClick={() => void consulta.refetch()}>
          Tentar de novo
        </button>
      </div>
    )
  }
  return <>{children(consulta.data)}</>
}
