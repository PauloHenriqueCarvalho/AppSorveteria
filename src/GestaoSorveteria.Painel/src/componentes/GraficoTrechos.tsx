import { useState } from 'react'
import type { AgrupamentoRelatorio, VendasNoTrechoDto } from '../api/tipos.ts'
import { formatarMoeda } from '../formatacao.ts'
import { descreverTrecho, rotuloCurto } from '../relatorios.ts'

interface Props {
  atual: VendasNoTrechoDto[]
  /** Trechos do período anterior, pareados por posição com os do atual (RN-RL-09); pode ter um a mais ou a menos. */
  antes: VendasNoTrechoDto[]
  agrupamento: AgrupamentoRelatorio
  rotuloAtual: string
  rotuloAntes: string
}

/** Teto "redondo" do eixo: 137 → 150, 1.284 → 1.500. Só para desenhar; os valores vêm da API. */
function tetoDoEixo(maximo: number): number {
  if (maximo <= 0) {
    return 100
  }
  const potencia = 10 ** Math.floor(Math.log10(maximo))
  const passo = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10].find((m) => m * potencia >= maximo) ?? 10
  return passo * potencia
}

function compacto(valor: number): string {
  return valor >= 1000 ? `${(valor / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 2 })} mil` : String(valor)
}

/**
 * Colunas por dia, semana ou mês do período (cor da marca) com o trecho do período anterior ao lado (azul).
 * Passar o mouse mostra os valores; "Ver em tabela" mostra todos os números (RN-RL-08/09).
 */
export default function GraficoTrechos({ atual, antes, agrupamento, rotuloAtual, rotuloAntes }: Props) {
  const [ativo, setAtivo] = useState<number | null>(null)
  const posicoes = Math.max(atual.length, antes.length)
  if (posicoes === 0) {
    return <p className="suave">Sem vendas para mostrar.</p>
  }

  const teto = tetoDoEixo(Math.max(...atual.map((t) => t.total), ...antes.map((t) => t.total)))
  const altura = (valor: number) => `${(valor / teto) * 100}%`
  // Até ~12 rótulos embaixo, para não encavalar.
  const passo = Math.ceil(atual.length / 12)
  const indices = Array.from({ length: posicoes }, (_, i) => i)

  return (
    <div className="grafico grafico-trechos">
      <div className="grafico-legenda" aria-hidden="true">
        <span>
          <i className="grafico-chave grafico-hoje" /> {rotuloAtual}
        </span>
        <span>
          <i className="grafico-chave grafico-antes" /> {rotuloAntes}
        </span>
      </div>

      <div
        className="grafico-area"
        role="img"
        aria-label={`Vendas por ${agrupamento === 'Mes' ? 'mês' : agrupamento.toLowerCase()}: ${rotuloAtual} comparado com ${rotuloAntes}. Os números estão em "Ver em tabela".`}
      >
        <div className="grafico-eixo" aria-hidden="true">
          <span>R$ {compacto(teto)}</span>
          <span>R$ {compacto(teto / 2)}</span>
          <span>R$ 0</span>
        </div>
        <div className="grafico-colunas">
          <span className="grafico-grade" style={{ bottom: '50%' }} aria-hidden="true" />
          <span className="grafico-grade" style={{ bottom: '100%' }} aria-hidden="true" />
          {indices.map((i) => {
            const t = atual[i]
            const a = antes[i]
            return (
              <div
                key={i}
                className={ativo === i ? 'grafico-hora grafico-hora-ativa' : 'grafico-hora'}
                onMouseEnter={() => setAtivo(i)}
                onMouseLeave={() => setAtivo(null)}
              >
                <div className="grafico-par">
                  <span className="grafico-barra grafico-antes" style={{ height: altura(a?.total ?? 0) }} />
                  <span className="grafico-barra grafico-hoje" style={{ height: altura(t?.total ?? 0) }} />
                </div>
                {ativo === i && (
                  <div className={i < posicoes / 2 ? 'grafico-dica' : 'grafico-dica grafico-dica-esquerda'} role="status">
                    {t && (
                      <span>
                        <i className="grafico-chave grafico-hoje" aria-hidden="true" /> {descreverTrecho(t, agrupamento)}:{' '}
                        <strong>{formatarMoeda(t.total)}</strong> · {t.quantidade} venda{t.quantidade === 1 ? '' : 's'}
                      </span>
                    )}
                    {a && (
                      <span>
                        <i className="grafico-chave grafico-antes" aria-hidden="true" /> {descreverTrecho(a, agrupamento)}:{' '}
                        {formatarMoeda(a.total)}
                      </span>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
        <div className="grafico-horas" aria-hidden="true">
          {indices.map((i) => (
            <span key={i}>{atual[i] && i % passo === 0 ? rotuloCurto(atual[i], agrupamento) : ''}</span>
          ))}
        </div>
      </div>

      <details className="grafico-tabela">
        <summary>Ver em tabela</summary>
        <div className="tabela-rolagem">
          <table className="tabela">
            <thead>
              <tr>
                <th scope="col">{agrupamento === 'Dia' ? 'Dia' : agrupamento === 'Semana' ? 'Semana' : 'Mês'}</th>
                <th scope="col" className="numero">
                  Total
                </th>
                <th scope="col" className="numero">
                  Vendas
                </th>
                <th scope="col">Anterior</th>
                <th scope="col" className="numero">
                  Total anterior
                </th>
              </tr>
            </thead>
            <tbody>
              {indices.map((i) => (
                <tr key={i}>
                  <td>{atual[i] ? descreverTrecho(atual[i], agrupamento) : '—'}</td>
                  <td className="numero">{atual[i] ? formatarMoeda(atual[i].total) : '—'}</td>
                  <td className="numero">{atual[i]?.quantidade ?? '—'}</td>
                  <td className="suave">{antes[i] ? descreverTrecho(antes[i], agrupamento) : '—'}</td>
                  <td className="numero">{antes[i] ? formatarMoeda(antes[i].total) : '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </details>
    </div>
  )
}
