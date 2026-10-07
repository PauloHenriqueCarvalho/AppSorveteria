import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { CHAVE_RELATORIOS, obterResumoDoDia } from '../api/relatorios.ts'
import type { ResumoDiaDto } from '../api/tipos.ts'
import Consulta from '../componentes/Consulta.tsx'
import { ConferenciaCaixa, ResultadoCaixa } from '../componentes/ConferenciaCaixa.tsx'
import { formatarDataHora, formatarDia, formatarMoeda, hojeNaLoja, nomeDaForma } from '../formatacao.ts'

// Enquanto a tela está aberta, atualiza sozinha (só com a aba visível).
const ATUALIZAR_A_CADA_MS = 2 * 60_000

/** Dashboard do dia: o que foi vendido e se o caixa está batendo. */
export default function Inicio() {
  const [dia, setDia] = useState(() => hojeNaLoja())
  const hoje = dia === hojeNaLoja()

  const resumo = useQuery({
    queryKey: [...CHAVE_RELATORIOS, 'dia', dia],
    queryFn: () => obterResumoDoDia(dia),
    refetchInterval: hoje ? ATUALIZAR_A_CADA_MS : false,
  })

  return (
    <>
      <div className="cabecalho-pagina">
        <h1>{hoje ? 'Hoje' : formatarDia(dia)}</h1>
        <input
          type="date"
          className="data-compacta"
          aria-label="Escolher o dia"
          value={dia}
          max={hojeNaLoja()}
          onChange={(e) => setDia(e.target.value || hojeNaLoja())}
        />
      </div>
      <Consulta consulta={resumo}>{(r) => <Resumo resumo={r} hoje={hoje} />}</Consulta>
    </>
  )
}

function Resumo({ resumo, hoje }: { resumo: ResumoDiaDto; hoje: boolean }) {
  const caixa = resumo.caixaAtual

  return (
    <>
      <section className="cartao destaque">
        <span className="suave">Total vendido</span>
        <strong className="valor-grande">{formatarMoeda(resumo.totalVendido)}</strong>
        <div className="indicadores">
          <span>
            <strong>{resumo.quantidadeComandas}</strong> venda{resumo.quantidadeComandas === 1 ? '' : 's'}
          </span>
          <span>
            Ticket médio <strong>{formatarMoeda(resumo.ticketMedio)}</strong>
          </span>
        </div>
      </section>

      {resumo.porFormaPagamento.length > 0 && (
        <section className="cartao">
          <h2>Como pagaram</h2>
          <dl className="valores">
            {resumo.porFormaPagamento.map((f) => (
              <div key={f.forma} className="valores-linha">
                <dt>
                  {nomeDaForma(f.forma)} <span className="suave">({f.quantidade})</span>
                </dt>
                <dd>{formatarMoeda(f.total)}</dd>
              </div>
            ))}
          </dl>
        </section>
      )}

      {(resumo.quantidadeEstornos > 0 || resumo.quantidadeCanceladas > 0) && (
        <section className="cartao">
          <dl className="valores">
            {resumo.quantidadeEstornos > 0 && (
              <div className="valores-linha">
                <dt>
                  Estornadas <span className="suave">({resumo.quantidadeEstornos})</span>
                </dt>
                <dd>{formatarMoeda(resumo.totalEstornado)}</dd>
              </div>
            )}
            {resumo.quantidadeCanceladas > 0 && (
              <div className="valores-linha">
                <dt>Canceladas no balcão</dt>
                <dd>{resumo.quantidadeCanceladas}</dd>
              </div>
            )}
          </dl>
        </section>
      )}

      {hoje && (
        <section className="cartao">
          <div className="cabecalho-pagina">
            <h2>Caixa</h2>
            {caixa && <ResultadoCaixa caixa={caixa} />}
          </div>
          {caixa ? (
            <>
              <p className="suave">
                Aberto em {formatarDataHora(caixa.abertoEm)} por {caixa.abertoPorNome}. Mostra o que o celular do balcão já
                enviou.
              </p>
              <ConferenciaCaixa caixa={caixa} />
              <p>
                <Link to={`/caixas/${caixa.id}`}>Ver o caixa</Link>
              </p>
            </>
          ) : (
            <p className="suave">
              Nenhum caixa aberto agora. <Link to="/caixas">Ver os caixas fechados</Link>
            </p>
          )}
        </section>
      )}

      {resumo.vendasRecebidasAposFechamento > 0 && (
        <p className="alerta alerta-aviso">
          {resumo.vendasRecebidasAposFechamento === 1
            ? '1 venda chegou do celular depois que o caixa foi fechado. '
            : `${resumo.vendasRecebidasAposFechamento} vendas chegaram do celular depois que o caixa foi fechado. `}
          <Link to="/caixas">Conferir nos caixas</Link>
        </p>
      )}

      <p>
        <Link to="/vendas">Ver as vendas</Link>
      </p>
    </>
  )
}
