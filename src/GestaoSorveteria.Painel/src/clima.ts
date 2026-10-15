// Clima × vendas (docs/02, seção 9b). Só formata e posiciona o que a API devolveu: médias e totais vêm prontos (ADR 018).
import type { ClimaVendasDto } from './api/tipos.ts'
import { celulaDinheiro, type Celula } from './exportar.ts'
import { descreverTrecho, nomeDoDiaDaSemana } from './relatorios.ts'

/** Ícones do Visual Crossing ("rain", "clear-day"…) → símbolo decorativo; o texto da condição vem da API. */
const SIMBOLOS: Record<string, string> = {
  'clear-day': '☀️',
  'clear-night': '🌙',
  'partly-cloudy-day': '⛅',
  'partly-cloudy-night': '☁️',
  cloudy: '☁️',
  rain: '🌧️',
  'showers-day': '🌦️',
  'showers-night': '🌧️',
  'thunder-rain': '⛈️',
  'thunder-showers-day': '⛈️',
  'thunder-showers-night': '⛈️',
  fog: '🌫️',
  wind: '💨',
  snow: '❄️',
}

export function simboloDoClima(icone: string | null): string {
  return (icone && SIMBOLOS[icone]) || '🌡️'
}

const umaCasa = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1 })

/** 28 → "28 °C"; 28.5 → "28,5 °C". */
export function formatarTemperatura(valor: number): string {
  return `${umaCasa.format(valor)} °C`
}

/** null → "—"; 0 → "sem chuva"; 3.2 → "3,2 mm". */
export function formatarChuva(mm: number | null): string {
  if (mm === null) {
    return '—'
  }
  return mm === 0 ? 'sem chuva' : `${umaCasa.format(mm)} mm`
}

/** Chance de chuva da previsão: 30 → "30%"; null → "". */
export function formatarChance(pct: number | null): string {
  return pct === null ? '' : `${Math.round(pct)}%`
}

/** "qua 07/10". */
export function descreverDia(dia: string): string {
  return descreverTrecho({ de: dia, ate: dia }, 'Dia')
}

/** RN-CL-02: de onde veio o clima do dia, em palavras para a dona. */
export function descreverOrigem(origem: string, definitivo: boolean): string {
  const fonte = origem === 'Observado' ? 'medido por estação' : origem === 'Combinado' ? 'medido e completado por satélite' : 'previsão'
  return definitivo ? fonte : `${fonte} · ainda pode ser corrigido`
}

/** Menos que isso numa média: pouca confiança (o painel avisa). */
export const POUCOS_DIAS = 3

/** Teto "redondo" do eixo de dinheiro: 137 → 150, 1.284 → 1.500. Só para desenhar. */
export function tetoRedondo(maximo: number): number {
  if (maximo <= 0) {
    return 100
  }
  const potencia = 10 ** Math.floor(Math.log10(maximo))
  const passo = [1, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10].find((m) => m * potencia >= maximo) ?? 10
  return passo * potencia
}

/** Eixo da temperatura: de 2 em 2 °C, com folga, nunca menos de 10 °C de largura. Ex.: 18–33 → 16–34. */
export function eixoTemperatura(valores: number[]): { de: number; ate: number } {
  if (valores.length === 0) {
    return { de: 15, ate: 35 }
  }
  let de = Math.floor((Math.min(...valores) - 1) / 2) * 2
  let ate = Math.ceil((Math.max(...valores) + 1) / 2) * 2
  while (ate - de < 10) {
    de -= 2
    ate += 2
  }
  return { de, ate }
}

/** Posição de 0 a 100 (%) de um valor no eixo. */
export function posicao(valor: number, de: number, ate: number): number {
  return ate === de ? 50 : Math.min(100, Math.max(0, ((valor - de) / (ate - de)) * 100))
}

/**
 * A planilha do Clima × vendas: um dia por linha, com o clima, as vendas e uma coluna por categoria (todas as que aparecem
 * no período). É a base que vai para a estimativa de vendas; os números são os da API.
 */
export function linhasDaPlanilha(r: ClimaVendasDto): Celula[][] {
  const categorias = [...new Set(r.dias.flatMap((d) => d.porCategoria.map((c) => c.categoria)))].sort((a, b) => a.localeCompare(b, 'pt-BR'))
  const cabecalho: Celula[] = [
    'Data',
    'Dia da semana',
    'Feriado',
    'Fim de semana ou feriado',
    'Máxima (°C)',
    'Mínima (°C)',
    'Média (°C)',
    'Sensação máxima (°C)',
    'Chuva (mm)',
    'Parte do dia com chuva (%)',
    'Umidade (%)',
    'Nuvens (%)',
    'Condição',
    'Origem do clima',
    'Faturamento (R$)',
    'Vendas',
    'Ticket médio (R$)',
    'Avisos urgentes',
    'Entra nas médias',
    ...categorias.flatMap((c) => [`${c} (qtd.)`, `${c} (R$)`]),
  ]
  const numero = (valor: number | null | undefined) => (valor === null || valor === undefined ? '' : umaCasa.format(valor))
  const linhas = r.dias.map((d): Celula[] => {
    const c = d.clima
    const porCategoria = new Map(d.porCategoria.map((x) => [x.categoria, x]))
    return [
      d.data.split('-').reverse().join('/'),
      nomeDoDiaDaSemana(d.diaDaSemana),
      d.feriado ? `${d.feriado}${d.feriadoFacultativo ? ' (ponto facultativo)' : ''}` : '',
      d.fimDeSemanaOuFeriado ? 'sim' : 'não',
      numero(c?.tempMax),
      numero(c?.tempMin),
      numero(c?.tempMedia),
      numero(c?.sensacaoMax),
      numero(c?.chuvaMm),
      numero(c?.chuvaHorasPct),
      numero(c?.umidadePct),
      numero(c?.nuvensPct),
      c?.condicao ?? '',
      c ? descreverOrigem(c.origem, c.definitivo) : 'sem clima',
      celulaDinheiro(d.faturamento),
      d.vendas,
      celulaDinheiro(d.ticketMedio),
      d.avisosUrgentes,
      d.entraNasMedias ? 'sim' : 'não',
      ...categorias.flatMap((nome) => {
        const x = porCategoria.get(nome)
        return [x?.quantidade ?? 0, celulaDinheiro(x?.total ?? 0)]
      }),
    ]
  })
  return [cabecalho, ...linhas]
}
