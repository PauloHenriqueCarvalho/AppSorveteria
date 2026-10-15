import { useState } from 'react'
import type { DiaClimaVendasDto } from '../api/tipos.ts'
import { descreverDia, eixoTemperatura, formatarChuva, formatarTemperatura, posicao, tetoRedondo } from '../clima.ts'
import { formatarMoeda } from '../formatacao.ts'

function compacto(valor: number): string {
  return valor >= 1000 ? `${(valor / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 2 })} mil` : String(valor)
}

/**
 * RN-CL-06: cada dia que entra nas médias vira um ponto — máxima do dia na horizontal, faturamento na vertical (um eixo só).
 * Dia útil (círculo azul) e fim de semana ou feriado (quadrado na cor da marca), porque o fim de semana vende diferente.
 * Passar o mouse ou focar no ponto mostra o dia; a tabela "Dia a dia" da página tem todos os números.
 */
export default function GraficoClimaVendas({ dias }: { dias: DiaClimaVendasDto[] }) {
  const [ativo, setAtivo] = useState<string | null>(null)
  const pontos = dias.filter((d) => d.entraNasMedias && d.clima !== null)
  if (pontos.length === 0) {
    return <p className="suave">Ainda não há dias com clima e venda para comparar neste período.</p>
  }

  const eixoX = eixoTemperatura(pontos.map((d) => d.clima!.tempMax))
  const teto = tetoRedondo(Math.max(...pontos.map((d) => d.faturamento)))
  // Marcas de 2 em 2 °C; num eixo largo, de 4 em 4 para os rótulos não encavalarem no celular.
  const passoX = (eixoX.ate - eixoX.de) / 2 + 1 <= 7 ? 2 : 4
  const marcasX = Array.from({ length: Math.floor((eixoX.ate - eixoX.de) / passoX) + 1 }, (_, i) => eixoX.de + i * passoX)

  return (
    <div className="grafico dispersao">
      <div className="grafico-legenda" aria-hidden="true">
        <span>
          <i className="dispersao-chave dispersao-util" /> Dia útil
        </span>
        <span>
          <i className="dispersao-chave dispersao-fds" /> Fim de semana ou feriado
        </span>
      </div>

      <div className="dispersao-area">
        <div className="grafico-eixo" aria-hidden="true">
          <span>R$ {compacto(teto)}</span>
          <span>R$ {compacto(teto / 2)}</span>
          <span>R$ 0</span>
        </div>
        <div className="dispersao-plano" role="group" aria-label="Faturamento de cada dia pela temperatura máxima">
          <span className="grafico-grade" style={{ bottom: '50%' }} aria-hidden="true" />
          <span className="grafico-grade" style={{ bottom: '100%' }} aria-hidden="true" />
          {pontos.map((d) => {
            const clima = d.clima!
            const x = posicao(clima.tempMax, eixoX.de, eixoX.ate)
            const y = posicao(d.faturamento, 0, teto)
            const descricao = `${descreverDia(d.data)}${d.feriado ? ` (${d.feriado})` : ''}: máxima ${formatarTemperatura(clima.tempMax)}, ${formatarChuva(clima.chuvaMm)}; ${formatarMoeda(d.faturamento)} em ${d.vendas} venda${d.vendas === 1 ? '' : 's'}`
            return (
              <button
                key={d.data}
                type="button"
                className={`dispersao-ponto ${d.fimDeSemanaOuFeriado ? 'dispersao-fds' : 'dispersao-util'}${ativo === d.data ? ' dispersao-ponto-ativo' : ''}`}
                style={{ left: `${x}%`, bottom: `${y}%` }}
                aria-label={descricao}
                onMouseEnter={() => setAtivo(d.data)}
                onMouseLeave={() => setAtivo(null)}
                onFocus={() => setAtivo(d.data)}
                onBlur={() => setAtivo(null)}
              >
                {ativo === d.data && (
                  <span
                    className={`grafico-dica dispersao-dica${x > 60 ? ' dispersao-dica-esquerda' : ''}${y > 70 ? ' dispersao-dica-baixo' : ''}`}
                    aria-hidden="true"
                  >
                    <span>
                      <strong>{descreverDia(d.data)}</strong>
                      {d.feriado && ` · ${d.feriado}`}
                    </span>
                    <span>
                      {formatarTemperatura(clima.tempMax)} · {formatarChuva(clima.chuvaMm)}
                    </span>
                    <span>
                      <strong>{formatarMoeda(d.faturamento)}</strong> · {d.vendas} venda{d.vendas === 1 ? '' : 's'}
                    </span>
                  </span>
                )}
              </button>
            )
          })}
        </div>
        <div className="dispersao-eixo-x" aria-hidden="true">
          {marcasX.map((t) => (
            <span key={t} style={{ left: `${posicao(t, eixoX.de, eixoX.ate)}%` }}>
              {t}°
            </span>
          ))}
        </div>
      </div>
      <p className="suave pequeno dispersao-rodape">Temperatura máxima do dia →</p>
    </div>
  )
}
