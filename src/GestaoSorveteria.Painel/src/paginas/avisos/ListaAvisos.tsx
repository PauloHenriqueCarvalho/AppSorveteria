import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { CHAVE_AVISOS, listarAvisos } from '../../api/avisos.ts'
import type { AvisoDto, PrioridadeAviso } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Indicador, Selo } from '../../componentes/Ui.tsx'
import { descreverQuantidade, nomeDaPrioridade, quando, TOM_DA_PRIORIDADE } from '../../estoque.ts'

type Visao = 'abertos' | 'fechados'

/** /avisos — "Está acabando" (RN-AV-01…05): o que o balcão avisou, do mais urgente para o menos. */
export default function ListaAvisos() {
  const navegar = useNavigate()
  const [visao, setVisao] = useState<Visao>('abertos')
  const avisos = useQuery({
    queryKey: [...CHAVE_AVISOS, visao],
    queryFn: () => listarAvisos(visao === 'fechados'),
    placeholderData: keepPreviousData,
  })

  return (
    <>
      <CabecalhoPagina
        titulo="Avisos de estoque"
        subtitulo="O que o balcão avisou que está acabando. Urgente chega no seu WhatsApp na hora; o resto, no resumo do dia."
        acoes={
          <>
            <Link to="/avisos/resumo" className="botao botao-secundario">
              Resumo do dia
            </Link>
            <Link to="/avisos/novo" className="botao">
              Novo aviso
            </Link>
          </>
        }
      />

      <Consulta consulta={avisos}>
        {(lista) => {
          const abertos = lista.filter((a) => a.status === 'Aberto')
          const fechados = lista.filter((a) => a.status === 'Fechado')
          const visiveis = visao === 'abertos' ? abertos : fechados
          const conta = (p: PrioridadeAviso) => abertos.filter((a) => a.prioridade === p).length
          return (
            <>
              {visao === 'abertos' && (
                <div className="grade-indicadores">
                  <Indicador rotulo="Urgentes" valor={conta('Urgente')} detalhe="acabou ou cliente pediu" tom={conta('Urgente') > 0 ? 'atencao' : 'neutro'} />
                  <Indicador rotulo="Alta" valor={conta('Alta')} detalhe="acaba hoje ou amanhã" />
                  <Indicador rotulo="Média" valor={conta('Media')} detalhe="entra no próximo pedido" />
                  <Indicador rotulo="Abertos" valor={abertos.length} detalhe="fecham sozinhos quando a entrega chega" tom="marca" />
                </div>
              )}

              <div className="filtros">
                <div className="abas" role="group" aria-label="Mostrar">
                  <button
                    type="button"
                    className={visao === 'abertos' ? 'aba aba-ativa' : 'aba'}
                    aria-pressed={visao === 'abertos'}
                    onClick={() => setVisao('abertos')}
                  >
                    Abertos
                  </button>
                  <button
                    type="button"
                    className={visao === 'fechados' ? 'aba aba-ativa' : 'aba'}
                    aria-pressed={visao === 'fechados'}
                    onClick={() => setVisao('fechados')}
                  >
                    Fechados (30 dias)
                  </button>
                </div>
              </div>

              {visiveis.length === 0 ? (
                <section className="secao vazio">
                  <h2>{visao === 'abertos' ? 'Nada faltando' : 'Nenhum aviso fechado nos últimos 30 dias'}</h2>
                  {visao === 'abertos' && (
                    <p className="suave">Quando o atendente tocar em “Está acabando” no tablet, o aviso aparece aqui.</p>
                  )}
                </section>
              ) : (
                <section className="secao secao-tabela">
                  <div className="tabela-caixa">
                    <table className="tabela">
                      <thead>
                        <tr>
                          <th scope="col">Prioridade</th>
                          <th scope="col">Item</th>
                          <th scope="col">Agora</th>
                          <th scope="col" className="so-largo">
                            Avisou
                          </th>
                          <th scope="col" className="so-largo">
                            {visao === 'abertos' ? 'Última notícia' : 'Fechado'}
                          </th>
                        </tr>
                      </thead>
                      <tbody>
                        {visiveis.map((a) => (
                          <LinhaAviso key={a.id} aviso={a} aoAbrir={() => navegar(`/avisos/${a.id}`)} />
                        ))}
                      </tbody>
                    </table>
                  </div>
                </section>
              )}
            </>
          )
        }}
      </Consulta>
    </>
  )
}

function LinhaAviso({ aviso, aoAbrir }: { aviso: AvisoDto; aoAbrir: () => void }) {
  const fechado = aviso.status === 'Fechado'
  return (
    <tr className="linha-clicavel" onClick={aoAbrir}>
      <td>
        <Selo tom={fechado ? 'neutro' : TOM_DA_PRIORIDADE[aviso.prioridade]}>{nomeDaPrioridade(aviso.prioridade)}</Selo>
      </td>
      <td>
        <Link to={`/avisos/${aviso.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
          {aviso.descricao}
        </Link>
        {aviso.observacao && <span className="suave bloco">{aviso.observacao}</span>}
      </td>
      <td className={aviso.quantidadeAtual === 0 && !fechado ? 'sem-quebra forte texto-ruim' : 'sem-quebra'}>{descreverQuantidade(aviso.quantidadeAtual)}</td>
      <td className="so-largo suave">
        {aviso.criadoPorNome} · {quando(aviso.criadoEm)}
      </td>
      <td className="so-largo suave">
        {fechado
          ? `${aviso.fechadoPorNome ?? 'Entrega'} · ${quando(aviso.fechadoEm!)} — ${aviso.motivoFechamento ?? ''}`
          : aviso.atualizadoPorNome
            ? `Ainda falta: ${aviso.atualizadoPorNome} · ${quando(aviso.atualizadoEm)}`
            : '—'}
      </td>
    </tr>
  )
}
