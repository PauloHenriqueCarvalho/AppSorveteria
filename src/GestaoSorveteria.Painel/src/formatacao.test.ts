import { describe, expect, it } from 'vitest'
import { formatarData, formatarDataHora, formatarDia, formatarHora, hojeNaLoja, nomeDaForma } from './formatacao.ts'

// Intl usa espaço não separável em alguns formatos.
const normal = (texto: string) => texto.replace(/\s/g, ' ')

describe('datas no horário da loja (America/Sao_Paulo)', () => {
  it('formatarDataHora_Utc_ConverteParaBrasilia', () => {
    expect(normal(formatarDataHora('2026-09-30T18:05:00Z'))).toBe('30/09/2026, 15:05')
  })

  it('formatarData_DepoisDaMeiaNoiteUtc_AindaEhODiaAnterior', () => {
    // 01/10 02:30 UTC = 30/09 23:30 em Brasília.
    expect(formatarData('2026-10-01T02:30:00Z')).toBe('30/09/2026')
    expect(formatarHora('2026-10-01T02:30:00Z')).toBe('23:30')
  })

  it('formatarDia_DiaComercial_NaoMudaComFuso', () => {
    expect(formatarDia('2026-09-01')).toBe('01/09/2026')
  })

  it('hojeNaLoja_DepoisDaMeiaNoiteUtc_DiaDeBrasilia', () => {
    expect(hojeNaLoja(new Date('2026-10-01T02:30:00Z'))).toBe('2026-09-30')
  })
})

describe('nomeDaForma', () => {
  it.each([
    ['Dinheiro', 'Dinheiro'],
    ['CartaoDebito', 'Cartão de débito'],
    ['CartaoCredito', 'Cartão de crédito'],
    ['Vale', 'Vale'],
  ])('nomeDaForma_%s', (forma, esperado) => {
    expect(nomeDaForma(forma)).toBe(esperado)
  })
})
