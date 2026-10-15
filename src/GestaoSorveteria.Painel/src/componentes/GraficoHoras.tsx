import { useState } from 'react'
import type { VendasNaHoraDto } from '../api/tipos.ts'
import { formatarMoeda } from '../formatacao.ts'

interface Props {
  hoje: VendasNaHoraDto[]
  antes: VendasNaHoraDto[]
  /** Rótulo da série de comparação ("quarta passada, até 12:40"); vazio = sem comparação (só uma série). */
  rotuloAntes: string
  rotuloHoje: string
}

// Faixa mínima mostrada (a loja abre de manhã e fecha à noite); amplia se houver venda fora dela.
const PRIMEIRA_HORA = 10
const ULTIMA_HORA = 22

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
 * Colunas por hora: hoje (cor da marca) × mesmo dia da semana passada (azul). RN-RL-06/07.
 * Passar o mouse ou o foco numa hora mostra os valores; "Ver em tabela" mostra todos os números.
 */
export default function GraficoHoras({ hoje, antes, rotuloAntes, rotuloHoje }: Props) {
  const [ativa, setAtiva] = useState<number | null>(null)

  const todas = [...hoje, ...antes].map((h) => h.hora)
  if (todas.length === 0) {
    return <p className="suave">Sem vendas para mostrar por hora.</p>
  }

  const primeira = Math.min(PRIMEIRA_HORA, ...todas)
  const ultima = Math.max(ULTIMA_HORA, ...todas)
  const horas = Array.from({ length: ultima - primeira + 1 }, (_, i) => primeira + i)
  const deHoje = new Map(hoje.map((h) => [h.hora, h]))
  const deAntes = new Map(antes.map((h) => [h.hora, h]))
  const teto = tetoDoEixo(Math.max(...hoje.map((h) => h.total), ...antes.map((h) => h.total)))
  const altura = (valor: number) => `${(valor / teto) * 100}%`

  return (
    <div className="grafico">
      <div className="grafico-legenda" aria-hidden="true">
        <span>
          <i className="grafico-chave grafico-hoje" /> {rotuloHoje}
        </span>
        {rotuloAntes && (
          <span>
            <i className="grafico-chave grafico-antes" /> {rotuloAntes}
          </span>
        )}
      </div>

      <div
        className="grafico-area"
        role="img"
        aria-label={`Vendas por hora: ${rotuloHoje}${rotuloAntes ? ` comparado com ${rotuloAntes}` : ''}. Os números estão em "Ver em tabela".`}
      >
        <div className="grafico-eixo" aria-hidden="true">
          <span>R$ {compacto(teto)}</span>
          <span>R$ {compacto(teto / 2)}</span>
          <span>R$ 0</span>
        </div>
        <div className="grafico-colunas">
          <span className="grafico-grade" style={{ bottom: '50%' }} aria-hidden="true" />
          <span className="grafico-grade" style={{ bottom: '100%' }} aria-hidden="true" />
          {horas.map((hora) => {
            const h = deHoje.get(hora)
            const a = deAntes.get(hora)
            return (
              <div
                key={hora}
                className={ativa === hora ? 'grafico-hora grafico-hora-ativa' : 'grafico-hora'}
                onMouseEnter={() => setAtiva(hora)}
                onMouseLeave={() => setAtiva(null)}
              >
                <div className="grafico-par">
                  {rotuloAntes && <span className="grafico-barra grafico-antes" style={{ height: altura(a?.total ?? 0) }} />}
                  <span className="grafico-barra grafico-hoje" style={{ height: altura(h?.total ?? 0) }} />
                </div>
                {ativa === hora && (
                  <div className="grafico-dica" role="status">
                    <strong>{hora}h</strong>
                    <span>
                      <i className="grafico-chave grafico-hoje" aria-hidden="true" /> {formatarMoeda(h?.total ?? 0)} ·{' '}
                      {h?.quantidade ?? 0} venda{h?.quantidade === 1 ? '' : 's'}
                    </span>
                    {rotuloAntes && (
                      <span>
                        <i className="grafico-chave grafico-antes" aria-hidden="true" /> {formatarMoeda(a?.total ?? 0)}
                      </span>
                    )}
                  </div>
                )}
              </div>
            )
          })}
        </div>
        <div className="grafico-horas" aria-hidden="true">
          {horas.map((hora) => (
            <span key={hora}>{hora % 2 === 0 ? `${hora}h` : ''}</span>
          ))}
        </div>
      </div>

      <details className="grafico-tabela">
        <summary>Ver em tabela</summary>
        <div className="tabela-rolagem">
          <table className="tabela">
            <thead>
              <tr>
                <th scope="col">Hora</th>
                <th scope="col" className="numero">
                  {rotuloHoje}
                </th>
                <th scope="col" className="numero">
                  Vendas
                </th>
                {rotuloAntes && (
                  <th scope="col" className="numero">
                    {rotuloAntes}
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {horas
                .filter((hora) => deHoje.has(hora) || deAntes.has(hora))
                .map((hora) => (
                  <tr key={hora}>
                    <td>{hora}h</td>
                    <td className="numero">{formatarMoeda(deHoje.get(hora)?.total ?? 0)}</td>
                    <td className="numero">{deHoje.get(hora)?.quantidade ?? 0}</td>
                    {rotuloAntes && <td className="numero">{formatarMoeda(deAntes.get(hora)?.total ?? 0)}</td>}
                  </tr>
                ))}
            </tbody>
          </table>
        </div>
      </details>
    </div>
  )
}
