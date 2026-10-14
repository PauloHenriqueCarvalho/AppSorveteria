import { Link, useNavigate } from 'react-router'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Selo } from '../../componentes/Ui.tsx'
import { descreverDias, formatarWhatsApp, nomeDoEnvio } from '../../estoque.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

/** /fornecedores — RN-PF-01: quem vende para a loja, quando a dona pede e como o pedido chega a eles. */
export default function ListaFornecedores() {
  const navegar = useNavigate()
  const catalogo = useCatalogoEstoque()

  return (
    <>
      <CabecalhoPagina
        titulo="Fornecedores"
        subtitulo="Dia do pedido, prazo de entrega e como o pedido é enviado"
        acoes={
          <Link to="/fornecedores/novo" className="botao">
            Novo fornecedor
          </Link>
        }
      />
      <Consulta consulta={catalogo}>
        {(dados) =>
          dados.fornecedores.length === 0 ? (
            <section className="secao vazio">
              <h2>Nenhum fornecedor ainda</h2>
              <p className="suave">
                A importação da planilha cria a fábrica sozinha. Cadastre aqui quem vende copos, casquinhas e insumos.
              </p>
            </section>
          ) : (
            <section className="secao secao-tabela">
              <div className="tabela-caixa">
                <table className="tabela">
                  <thead>
                    <tr>
                      <th scope="col">Fornecedor</th>
                      <th scope="col">Pedido</th>
                      <th scope="col" className="so-largo">
                        Entrega
                      </th>
                      <th scope="col" className="so-largo">
                        Envio
                      </th>
                      <th scope="col" className="so-largo">
                        WhatsApp
                      </th>
                      <th scope="col" className="numero">
                        Itens
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {dados.fornecedores.map((f) => {
                      const itens = dados.itens.filter((i) => i.fornecedorId === f.id && i.ativo).length
                      return (
                        <tr key={f.id} className="linha-clicavel" onClick={() => navegar(`/fornecedores/${f.id}`)}>
                          <td>
                            <Link to={`/fornecedores/${f.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
                              {f.nome}
                            </Link>
                            {!f.ativo && (
                              <>
                                {' '}
                                <Selo>Desativado</Selo>
                              </>
                            )}
                          </td>
                          <td>{descreverDias(f.diasPedido)}</td>
                          <td className="so-largo">
                            {f.prazoEntregaDias === 0
                              ? 'No mesmo dia'
                              : `${f.prazoEntregaDias} ${f.prazoEntregaDias === 1 ? 'dia' : 'dias'}`}
                          </td>
                          <td className="so-largo">{nomeDoEnvio(f.envio)}</td>
                          <td className="so-largo suave">{f.whatsApp ? formatarWhatsApp(f.whatsApp) : '—'}</td>
                          <td className="numero">{itens}</td>
                        </tr>
                      )
                    })}
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
