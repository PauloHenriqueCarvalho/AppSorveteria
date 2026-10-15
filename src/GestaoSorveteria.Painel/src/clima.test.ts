import { describe, expect, it } from 'vitest'
import type { ClimaVendasDto, DiaClimaVendasDto } from './api/tipos.ts'
import {
  descreverDia,
  descreverOrigem,
  eixoTemperatura,
  formatarChance,
  formatarChuva,
  formatarTemperatura,
  linhasDaPlanilha,
  posicao,
  simboloDoClima,
  tetoRedondo,
} from './clima.ts'

describe('formatação do clima', () => {
  it('temperatura com no máximo 1 casa', () => {
    expect(formatarTemperatura(28)).toBe('28 °C')
    expect(formatarTemperatura(28.5)).toBe('28,5 °C')
  })

  it('chuva: sem informação, sem chuva e em mm', () => {
    expect(formatarChuva(null)).toBe('—')
    expect(formatarChuva(0)).toBe('sem chuva')
    expect(formatarChuva(3.2)).toBe('3,2 mm')
    expect(formatarChance(30)).toBe('30%')
    expect(formatarChance(null)).toBe('')
  })

  it('símbolo pelo ícone do serviço, com reserva para o desconhecido', () => {
    expect(simboloDoClima('rain')).toBe('🌧️')
    expect(simboloDoClima('algo-novo')).toBe('🌡️')
    expect(simboloDoClima(null)).toBe('🌡️')
  })

  it('dia e origem em palavras', () => {
    expect(descreverDia('2026-10-07')).toBe('qua 07/10')
    expect(descreverOrigem('Observado', true)).toBe('medido por estação')
    expect(descreverOrigem('Combinado', false)).toBe('medido e completado por satélite · ainda pode ser corrigido')
  })
})

describe('eixos do gráfico', () => {
  it('teto redondo do dinheiro', () => {
    expect(tetoRedondo(137)).toBe(150)
    expect(tetoRedondo(1284)).toBe(1500)
    expect(tetoRedondo(0)).toBe(100)
  })

  it('temperatura de 2 em 2 °C, com folga e pelo menos 10 °C', () => {
    expect(eixoTemperatura([18, 33])).toEqual({ de: 16, ate: 34 })
    expect(eixoTemperatura([28, 29])).toEqual({ de: 22, ate: 34 })
    expect(eixoTemperatura([])).toEqual({ de: 15, ate: 35 })
  })

  it('posição presa entre 0 e 100', () => {
    expect(posicao(25, 20, 30)).toBe(50)
    expect(posicao(40, 20, 30)).toBe(100)
    expect(posicao(5, 5, 5)).toBe(50)
  })
})

describe('planilha do Clima × vendas', () => {
  const dia = (parcial: Partial<DiaClimaVendasDto>): DiaClimaVendasDto => ({
    data: '2026-10-12',
    diaDaSemana: 1,
    feriado: null,
    feriadoFacultativo: false,
    fimDeSemanaOuFeriado: false,
    clima: null,
    faturamento: 0,
    vendas: 0,
    ticketMedio: 0,
    porCategoria: [],
    avisosUrgentes: 0,
    entraNasMedias: false,
    ...parcial,
  })
  const relatorio = (dias: DiaClimaVendasDto[]): ClimaVendasDto => ({
    de: '2026-10-12',
    ate: '2026-10-13',
    local: null,
    dias,
    porTemperatura: [],
    porChuva: [],
    proximosDias: [],
    diasSemClima: 0,
    atribuicao: 'Dados do clima: Visual Crossing',
    atribuicaoLink: 'https://www.visualcrossing.com/',
  })

  it('um dia por linha, clima em vírgula decimal e uma coluna por categoria do período', () => {
    const linhas = linhasDaPlanilha(
      relatorio([
        dia({
          feriado: 'Nossa Senhora Aparecida',
          fimDeSemanaOuFeriado: true,
          faturamento: 400,
          vendas: 2,
          ticketMedio: 200,
          entraNasMedias: true,
          porCategoria: [{ categoria: 'Picolés', quantidade: 10, total: 50 }],
          clima: {
            data: '2026-10-12',
            tempMax: 29.5,
            tempMin: 19,
            tempMedia: 24.2,
            sensacaoMax: null,
            chuvaMm: 2,
            chuvaHorasPct: 10,
            nuvensPct: 40,
            umidadePct: 60,
            icone: 'rain',
            condicao: 'Chuva',
            origem: 'Observado',
            definitivo: true,
          },
        }),
        dia({ data: '2026-10-13', diaDaSemana: 2, porCategoria: [{ categoria: 'Açaí', quantidade: 1, total: 18 }] }),
      ]),
    )

    expect(linhas).toHaveLength(3)
    expect(linhas[0].slice(-4)).toEqual(['Açaí (qtd.)', 'Açaí (R$)', 'Picolés (qtd.)', 'Picolés (R$)'])
    const feriado = linhas[1]
    expect(feriado.slice(0, 5)).toEqual(['12/10/2026', 'Segunda', 'Nossa Senhora Aparecida', 'sim', '29,5'])
    expect(feriado[7]).toBe('')
    expect(feriado[14]).toBe('400,00')
    expect(feriado.slice(-4)).toEqual([0, '0,00', 10, '50,00'])
    expect(linhas[2][13]).toBe('sem clima')
  })
})
