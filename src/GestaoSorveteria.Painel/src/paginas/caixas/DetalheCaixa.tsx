import { useQuery } from '@tanstack/react-query'
import { Link, useParams } from 'react-router'
import { CHAVE_CAIXAS, obterCaixa } from '../../api/caixas.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { ConferenciaCaixa, ResultadoCaixa } from '../../componentes/ConferenciaCaixa.tsx'
import { formatarDataHora, formatarHora, formatarMoeda, nomeDaForma } from '../../formatacao.ts'

export default function DetalheCaixa() {
  const { id = '' } = useParams()
  const caixa = useQuery({ queryKey: [...CHAVE_CAIXAS, id], queryFn: () => obterCaixa(id) })

  return (
    <>
      <p>
        <Link to="/caixas">← Caixas</Link>
      </p>
      <Consulta consulta={caixa}>
        {({ resumo, movimentos, porFormaPagamento, observacao }) => (
          <>
            <section className="cartao">
              <div className="cabecalho-pagina">
                <h1>Caixa de {formatarDataHora(resumo.abertoEm)}</h1>
                <ResultadoCaixa caixa={resumo} />
              </div>
              <p className="suave">
                Aberto por {resumo.abertoPorNome}
                {resumo.fechadoEm && ` · fechado às ${formatarHora(resumo.fechadoEm)} por ${resumo.fechadoPorNome}`}
              </p>
              <ConferenciaCaixa caixa={resumo} />
              {observacao && <p>Observação: {observacao}</p>}
            </section>

            {resumo.divergenciaSincronizacao && (
              <p className="alerta alerta-aviso">
                A conta feita no celular no fechamento foi diferente da conta do sistema. Os valores acima são os do
                sistema; vale conferir com o atendente.
              </p>
            )}
            {resumo.vendasRecebidasAposFechamento > 0 && (
              <p className="alerta alerta-aviso">
                {resumo.vendasRecebidasAposFechamento === 1
                  ? '1 venda chegou do celular depois que o caixa foi fechado'
                  : `${resumo.vendasRecebidasAposFechamento} vendas chegaram do celular depois que o caixa foi fechado`}
                {resumo.dinheiroRecebidoAposFechamento > 0 &&
                  ` (${formatarMoeda(resumo.dinheiroRecebidoAposFechamento)} em dinheiro, fora da conta do fechamento)`}
                .
              </p>
            )}

            <section className="cartao">
              <h2>
                Vendas: {formatarMoeda(resumo.totalVendas)}
                <span className="suave"> · {resumo.quantidadeComandas} comanda{resumo.quantidadeComandas === 1 ? '' : 's'}</span>
              </h2>
              {porFormaPagamento.length === 0 ? (
                <p className="suave">Nenhuma venda recebida.</p>
              ) : (
                <dl className="valores">
                  {porFormaPagamento.map((f) => (
                    <div key={f.forma} className="valores-linha">
                      <dt>
                        {nomeDaForma(f.forma)} <span className="suave">({f.quantidade})</span>
                      </dt>
                      <dd>{formatarMoeda(f.total)}</dd>
                    </div>
                  ))}
                </dl>
              )}
            </section>

            <section className="cartao">
              <h2>Retiradas e reforços</h2>
              {movimentos.length === 0 ? (
                <p className="suave">Nenhuma sangria ou suprimento.</p>
              ) : (
                <ul className="lista lista-simples">
                  {movimentos.map((m) => (
                    <li key={m.id} className="lista-item">
                      <span className="lista-principal">
                        <strong>{m.tipo === 'Sangria' ? 'Retirada (sangria)' : 'Reforço (suprimento)'}</strong>
                        <span className="suave">
                          {formatarHora(m.em)} · {m.usuarioNome} · {m.motivo}
                        </span>
                      </span>
                      <span className="lista-lateral">
                        {m.tipo === 'Sangria' ? '−' : '+'} {formatarMoeda(m.valor)}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </>
        )}
      </Consulta>
    </>
  )
}
