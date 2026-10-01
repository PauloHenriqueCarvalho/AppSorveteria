import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { CHAVE_COMANDAS, listarComandas, VENDAS_POR_PAGINA } from '../../api/comandas.ts'
import type { StatusComanda } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { formatarDia, formatarHora, formatarMoeda, hojeNaLoja, nomeDaForma, nomeDoStatus } from '../../formatacao.ts'

const FILTROS: { valor: StatusComanda | undefined; texto: string }[] = [
  { valor: undefined, texto: 'Todas' },
  { valor: 'Fechada', texto: 'Pagas' },
  { valor: 'Cancelada', texto: 'Canceladas' },
  { valor: 'Estornada', texto: 'Estornadas' },
]

export default function ListaVendas() {
  const [dia, setDia] = useState(() => hojeNaLoja())
  const [status, setStatus] = useState<StatusComanda | undefined>(undefined)
  const [pagina, setPagina] = useState(1)

  const vendas = useQuery({
    queryKey: [...CHAVE_COMANDAS, 'lista', dia, status, pagina],
    queryFn: () => listarComandas({ dia, status, pagina }),
    placeholderData: keepPreviousData,
  })

  function trocarDia(novo: string) {
    setDia(novo || hojeNaLoja())
    setPagina(1)
  }

  function trocarStatus(novo: StatusComanda | undefined) {
    setStatus(novo)
    setPagina(1)
  }

  return (
    <>
      <h1>Vendas</h1>

      <div className="filtros">
        <label className="campo campo-dia">
          <span>Dia</span>
          <input type="date" value={dia} max={hojeNaLoja()} onChange={(e) => trocarDia(e.target.value)} />
        </label>
        <div className="abas" role="group" aria-label="Mostrar">
          {FILTROS.map((f) => (
            <button
              key={f.texto}
              type="button"
              className={f.valor === status ? 'aba aba-ativa' : 'aba'}
              aria-pressed={f.valor === status}
              onClick={() => trocarStatus(f.valor)}
            >
              {f.texto}
            </button>
          ))}
        </div>
      </div>

      <Consulta consulta={vendas}>
        {(resultado) => {
          const paginas = Math.max(1, Math.ceil(resultado.totalItens / VENDAS_POR_PAGINA))
          if (resultado.itens.length === 0) {
            return <p className="cartao suave">Nenhuma venda em {formatarDia(dia)}.</p>
          }
          return (
            <>
              <p className="suave">
                {resultado.totalItens} venda{resultado.totalItens === 1 ? '' : 's'} em {formatarDia(dia)}
              </p>
              <ul className="lista">
                {resultado.itens.map((v) => (
                  <li key={v.id}>
                    <Link to={`/vendas/${v.id}`} className="lista-item">
                      <span className="lista-principal">
                        <strong>
                          Comanda {v.numero}
                          {v.tipo === 'Delivery' && <span className="suave"> · delivery</span>}
                        </strong>
                        <span className="suave">
                          {formatarHora(v.fechadaEm ?? v.criadaEm)} · {v.atendenteNome}
                          {v.formasPagamento.length > 0 && ` · ${v.formasPagamento.map(nomeDaForma).join(', ')}`}
                        </span>
                      </span>
                      <span className="lista-lateral">
                        <span className={v.status === 'Fechada' ? undefined : 'riscado'}>{formatarMoeda(v.total)}</span>
                        {v.status !== 'Fechada' && <span className="etiqueta">{nomeDoStatus(v.status)}</span>}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
              {paginas > 1 && (
                <div className="paginacao">
                  <button type="button" className="botao botao-secundario" disabled={pagina <= 1} onClick={() => setPagina(pagina - 1)}>
                    Anteriores
                  </button>
                  <span className="suave">
                    {pagina} de {paginas}
                  </span>
                  <button type="button" className="botao botao-secundario" disabled={pagina >= paginas} onClick={() => setPagina(pagina + 1)}>
                    Próximas
                  </button>
                </div>
              )}
            </>
          )
        }}
      </Consulta>
    </>
  )
}
