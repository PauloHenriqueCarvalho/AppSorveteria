import { describe, expect, it } from 'vitest'
import { lerPreco, precoParaCampo } from './entrada.ts'
import { formatarMoeda } from './formatacao.ts'

describe('lerPreco', () => {
  it.each([
    ['12,50', 12.5],
    ['12,5', 12.5],
    ['12.50', 12.5],
    ['12.5', 12.5],
    ['12', 12],
    ['0', 0],
    ['0,00', 0],
    ['1.234,56', 1234.56],
    ['1.234', 1234],
    ['R$ 7,90', 7.9],
    [' 7,90 ', 7.9],
    ['99.999,99', 99999.99],
  ])('lerPreco_%s_Le', (texto, esperado) => {
    expect(lerPreco(texto)).toBe(esperado)
  })

  it.each(['', '  ', 'abc', '12,345', '12.345,6.7', '-1', '1,2,3', '100.000,00', '12,'])(
    'lerPreco_%s_Invalido_RetornaNull',
    (texto) => {
      expect(lerPreco(texto)).toBeNull()
    },
  )
})

describe('precoParaCampo', () => {
  it('precoParaCampo_DuasCasas_UsaVirgula', () => {
    expect(precoParaCampo(12.5)).toBe('12,50')
    expect(precoParaCampo(0)).toBe('0,00')
  })

  it('precoParaCampo_IdaEVolta_MantemValor', () => {
    expect(lerPreco(precoParaCampo(1234.56))).toBe(1234.56)
  })
})

describe('formatarMoeda', () => {
  it('formatarMoeda_Valor_FormataEmReais', () => {
    // Intl usa espaço não separável entre "R$" e o número.
    expect(formatarMoeda(1234.5).replace(/\s/g, ' ')).toBe('R$ 1.234,50')
  })
})
