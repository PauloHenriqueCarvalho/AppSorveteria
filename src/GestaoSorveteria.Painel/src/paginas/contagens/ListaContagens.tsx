import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router'
import { CHAVE_CONTAGENS, listarContagens } from '../../api/contagens.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Selo } from '../../componentes/Ui.tsx'
import { quando } from '../../estoque.ts'

/** /contagens — contagem semanal do freezer (RN-ES-05): feitas no tablet ou aqui, das mais recentes para as mais antigas. */
export default function ListaContagens() {
  const navegar = useNavigate()
  const contagens = useQuery({ queryKey: CHAVE_CONTAGENS, queryFn: listarContagens })

  return (
    <>
      <CabecalhoPagina
        titulo="Contagem semanal"
        subtitulo="O que tem no freezer, uma vez por semana (na véspera do pedido). Só a contagem concluída vale."
        acoes={
          <Link to="/contagens/nova" className="botao">
            Nova contagem
          </Link>
        }
      />
      <Consulta consulta={contagens}>
        {(lista) =>
          lista.length === 0 ? (
            <section className="secao vazio">
              <h2>Nenhuma contagem ainda</h2>
              <p className="suave">
                O atendente conta pelo tablet na véspera do pedido (o app lembra). Você também pode contar por aqui.
              </p>
            </section>
          ) : (
            <section className="secao secao-tabela">
              <div className="tabela-caixa">
                <table className="tabela">
                  <thead>
                    <tr>
                      <th scope="col">Quando</th>
                      <th scope="col">Quem contou</th>
                      <th scope="col" className="so-largo">
                        Onde
                      </th>
                      <th scope="col" className="numero">
                        Contados
                      </th>
                      <th scope="col">Situação</th>
                    </tr>
                  </thead>
                  <tbody>
                    {lista.map((c) => (
                      <tr key={c.id} className="linha-clicavel" onClick={() => navegar(`/contagens/${c.id}`)}>
                        <td>
                          <Link to={`/contagens/${c.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
                            {quando(c.concluidaEm ?? c.iniciadaEm)}
                          </Link>
                        </td>
                        <td>{c.contadoPorNome}</td>
                        <td className="so-largo suave">{c.origem === 'App' ? 'Tablet' : 'Painel'}</td>
                        <td className="numero">
                          {c.itensContados}
                          {c.itensNaoContados > 0 && <span className="suave"> · {c.itensNaoContados} sem contar</span>}
                        </td>
                        <td>
                          <Selo tom={c.concluidaEm ? 'bom' : 'atencao'}>{c.concluidaEm ? 'Concluída' : 'Em andamento'}</Selo>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )
        }
      </Consulta>
    </>
  )
}
