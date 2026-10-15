import { describe, expect, it } from 'vitest'
import {
  descreverContado,
  descreverEmbalagens,
  diaDaSemana,
  entregaPrevistaDe,
  enviadoHoje,
  formatarPercentual,
  passoDeRecebimento,
  proximoDiaDePedido,
  situacaoNaConferencia,
} from './pedidos.ts'

describe('diaDaSemana', () => {
  it('lê o dia comercial sem depender do fuso do aparelho', () => {
    expect(diaDaSemana('2026-10-19')).toBe('Monday')
    expect(diaDaSemana('2026-10-18')).toBe('Sunday')
  })
})

describe('proximoDiaDePedido', () => {
  it('hoje conta; senão o próximo da semana', () => {
    expect(proximoDiaDePedido(['Monday'], '2026-10-19')).toBe('2026-10-19')
    expect(proximoDiaDePedido(['Monday', 'Thursday'], '2026-10-20')).toBe('2026-10-22')
    expect(proximoDiaDePedido(['Monday'], '2026-10-20')).toBe('2026-10-26')
  })

  it('sem dia de pedido, nulo', () => {
    expect(proximoDiaDePedido([], '2026-10-19')).toBeNull()
  })
})

describe('entregaPrevistaDe e enviadoHoje', () => {
  it('usa o dia da loja, não o UTC', () => {
    // 01:30 UTC do dia 20 ainda é dia 19 em Brasília.
    expect(entregaPrevistaDe('2026-10-20T01:30:00Z', 2)).toBe('2026-10-21')
    expect(enviadoHoje('2026-10-20T01:30:00Z', '2026-10-19')).toBe(true)
    expect(enviadoHoje(null, '2026-10-19')).toBe(false)
  })
})

describe('passoDeRecebimento', () => {
  it('meia caixa só para caixa', () => {
    expect(passoDeRecebimento('Caixa')).toBe(0.5)
    expect(passoDeRecebimento('Balde')).toBe(1)
  })
})

describe('descrições', () => {
  it('embalagens no singular e plural', () => {
    expect(descreverEmbalagens(2.5, 'Caixa')).toBe('2,5 caixas')
    expect(descreverEmbalagens(1, 'Balde')).toBe('1 balde')
  })

  it('contado em unidades para caixa e na embalagem para balde', () => {
    expect(descreverContado(14, 28, 'Caixa')).toBe('14 un.')
    expect(descreverContado(3, 1, 'Balde')).toBe('3 baldes')
    expect(descreverContado(null, 28, 'Caixa')).toBe('—')
  })

  it('percentual com uma casa', () => {
    expect(formatarPercentual(83.33)).toBe('83,3%')
    expect(formatarPercentual(100)).toBe('100%')
    expect(formatarPercentual(null)).toBe('—')
  })
})

describe('situacaoNaConferencia', () => {
  it('compara o que veio com o pedido', () => {
    expect(situacaoNaConferencia(3, 3)).toBe('Certo')
    expect(situacaoNaConferencia(3, 2.5)).toBe('Faltou')
    expect(situacaoNaConferencia(3, 4)).toBe('VeioAMais')
    expect(situacaoNaConferencia(0, 1)).toBe('NaoPedido')
  })
})
