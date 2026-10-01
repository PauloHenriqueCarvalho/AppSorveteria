import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { CHAVE_CAIXAS, listarCaixas } from '../../api/caixas.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { ResultadoCaixa } from '../../componentes/ConferenciaCaixa.tsx'
import { formatarData, formatarHora, formatarMoeda } from '../../formatacao.ts'

export default function ListaCaixas() {
  // Vazio = a API usa os últimos 30 dias.
  const [de, setDe] = useState('')
  const [ate, setAte] = useState('')
  const caixas = useQuery({
    queryKey: [...CHAVE_CAIXAS, 'lista', de, ate],
    queryFn: () => listarCaixas(de, ate),
  })

  return (
    <>
      <h1>Caixas</h1>

      <div className="filtros filtros-datas">
        <label className="campo">
          <span>De</span>
          <input type="date" value={de} onChange={(e) => setDe(e.target.value)} />
        </label>
        <label className="campo">
          <span>Até</span>
          <input type="date" value={ate} onChange={(e) => setAte(e.target.value)} />
        </label>
      </div>
      {!de && !ate && <p className="suave">Mostrando os últimos 30 dias.</p>}

      <Consulta consulta={caixas}>
        {(lista) =>
          lista.length === 0 ? (
            <p className="cartao suave">Nenhum caixa neste período.</p>
          ) : (
            <ul className="lista">
              {lista.map((c) => (
                <li key={c.id}>
                  <Link to={`/caixas/${c.id}`} className="lista-item">
                    <span className="lista-principal">
                      <strong>{formatarData(c.abertoEm)}</strong>
                      <span className="suave">
                        {formatarHora(c.abertoEm)}
                        {c.fechadoEm ? ` às ${formatarHora(c.fechadoEm)}` : ' — aberto'} · {c.abertoPorNome}
                      </span>
                    </span>
                    <span className="lista-lateral">
                      <span>{formatarMoeda(c.totalVendas)}</span>
                      <ResultadoCaixa caixa={c} />
                      {(c.divergenciaSincronizacao || c.vendasRecebidasAposFechamento > 0) && (
                        <span className="etiqueta etiqueta-aviso">Conferir</span>
                      )}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )
        }
      </Consulta>
    </>
  )
}
