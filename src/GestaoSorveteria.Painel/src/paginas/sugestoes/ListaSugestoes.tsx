import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router'
import { CHAVE_SUGESTOES, listarSugestoes } from '../../api/sugestoes.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Indicador, Selo } from '../../componentes/Ui.tsx'
import { formatarData } from '../../formatacao.ts'
import { nomeArea, nomeImportancia, nomeTipo, status } from '../../sugestoes.ts'

/** /sugestoes — o que a dona pediu para mudar ou ganhar no sistema e em que pé está (RN-SG-01..03). */
export default function ListaSugestoes() {
  const navegar = useNavigate()
  const sugestoes = useQuery({ queryKey: CHAVE_SUGESTOES, queryFn: listarSugestoes })

  return (
    <>
      <CabecalhoPagina
        titulo="Sugestões"
        subtitulo="Ideias, mudanças e problemas para o sistema. Escreva do seu jeito, com print ou foto se ajudar."
        acoes={
          <Link to="/sugestoes/nova" className="botao">
            Nova sugestão
          </Link>
        }
      />

      <Consulta consulta={sugestoes}>
        {(lista) => {
          if (lista.length === 0) {
            return (
              <section className="secao vazio">
                <h2>Nenhuma sugestão ainda</h2>
                <p className="suave">
                  Quer um botão em outro lugar, um relatório novo ou achou algo estranho? Toque em “Nova sugestão” e conte.
                </p>
              </section>
            )
          }
          const abertas = lista.filter((s) => s.status === 'Nova' || s.status === 'EmAnalise').length
          const planejadas = lista.filter((s) => s.status === 'Planejada').length
          const feitas = lista.filter((s) => s.status === 'Feita').length
          return (
            <>
              <div className="grade-indicadores grade-indicadores-3">
                <Indicador rotulo="Aguardando" valor={abertas} detalhe="novas ou em análise" tom={abertas > 0 ? 'atencao' : 'neutro'} />
                <Indicador rotulo="Planejadas" valor={planejadas} detalhe="vão entrar no sistema" />
                <Indicador rotulo="Feitas" valor={feitas} detalhe="já estão no sistema" tom={feitas > 0 ? 'bom' : 'neutro'} />
              </div>
              <section className="secao secao-tabela">
                <div className="tabela-caixa">
                  <table className="tabela">
                    <thead>
                      <tr>
                        <th scope="col">Sugestão</th>
                        <th scope="col">Parte</th>
                        <th scope="col">Importância</th>
                        <th scope="col">Andamento</th>
                        <th scope="col">Atualizada</th>
                      </tr>
                    </thead>
                    <tbody>
                      {lista.map((s) => {
                        const st = status(s.status)
                        return (
                          <tr key={s.id} className="linha-clicavel" onClick={() => navegar(`/sugestoes/${s.id}`)}>
                            <td>
                              <Link to={`/sugestoes/${s.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
                                {s.titulo}
                              </Link>
                              <span className="suave bloco">
                                {nomeTipo(s.tipo)} · {s.criadaPorNome}
                                {s.comentarios > 0 && ` · ${s.comentarios} ${s.comentarios === 1 ? 'mensagem' : 'mensagens'}`}
                                {s.imagens > 0 && ` · ${s.imagens} ${s.imagens === 1 ? 'imagem' : 'imagens'}`}
                              </span>
                            </td>
                            <td>{nomeArea(s.area)}</td>
                            <td>{nomeImportancia(s.importancia)}</td>
                            <td>
                              <Selo tom={st.tom}>{st.nome}</Selo>
                            </td>
                            <td className="suave">{formatarData(s.atualizadaEm)}</td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </section>
            </>
          )
        }}
      </Consulta>
    </>
  )
}
