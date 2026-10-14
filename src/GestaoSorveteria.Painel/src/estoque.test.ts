import { describe, expect, it } from 'vitest'
import {
  decodificarCsv,
  descreverDias,
  descreverEmbalagem,
  descreverQuantidade,
  formatarWhatsApp,
  lerQuantidade,
  quando,
} from './estoque.ts'

describe('descreverEmbalagem', () => {
  it('mostra quantas vêm na caixa e omite o 1', () => {
    expect(descreverEmbalagem('Caixa', 28)).toBe('Caixa com 28')
    expect(descreverEmbalagem('Balde', 1)).toBe('Balde')
  })
})

describe('descreverDias', () => {
  it('ordena de segunda a domingo e abrevia quando são vários', () => {
    expect(descreverDias(['Thursday', 'Monday'])).toBe('Seg e Qui')
    expect(descreverDias(['Sunday', 'Monday', 'Wednesday'])).toBe('Seg, Qua e Dom')
  })

  it('escreve por extenso quando é um dia só', () => {
    expect(descreverDias(['Monday'])).toBe('Segunda')
  })
})

describe('formatarWhatsApp', () => {
  it('formata celular e fixo com DDD', () => {
    expect(formatarWhatsApp('11987654321')).toBe('(11) 98765-4321')
    expect(formatarWhatsApp('1134567890')).toBe('(11) 3456-7890')
  })

  it('mostra o código do país quando vier junto', () => {
    expect(formatarWhatsApp('5511987654321')).toBe('+55 (11) 98765-4321')
  })
})

describe('lerQuantidade', () => {
  it('aceita meia caixa com vírgula ou ponto', () => {
    expect(lerQuantidade('2,5')).toBe(2.5)
    expect(lerQuantidade(' 3 ')).toBe(3)
  })

  it('recusa negativo e texto', () => {
    expect(lerQuantidade('-1')).toBeNull()
    expect(lerQuantidade('duas')).toBeNull()
    expect(lerQuantidade('')).toBeNull()
  })
})

describe('decodificarCsv', () => {
  it('lê UTF-8', () => {
    const bytes = new TextEncoder().encode('Picolé de fruta;Limão')
    expect(decodificarCsv(bytes.buffer as ArrayBuffer)).toBe('Picolé de fruta;Limão')
  })

  it('lê o CSV comum do Excel no Windows (Windows-1252) sem estragar os acentos', () => {
    // "Picolé;Limão" em Windows-1252: é = 0xE9, ã = 0xE3.
    const bytes = new Uint8Array([0x50, 0x69, 0x63, 0x6f, 0x6c, 0xe9, 0x3b, 0x4c, 0x69, 0x6d, 0xe3, 0x6f])
    expect(decodificarCsv(bytes.buffer)).toBe('Picolé;Limão')
  })
})

describe('quando', () => {
  // Quarta 14/10/2026, 15:42 em Brasília.
  const agora = new Date('2026-10-14T18:42:00Z')

  it('diz hoje e ontem pelo dia da loja, não pelo UTC', () => {
    expect(quando('2026-10-14T18:42:00Z', agora)).toBe('hoje 15:42')
    // 01:30 UTC do dia 15 ainda é dia 14 em Brasília.
    expect(quando('2026-10-15T01:30:00Z', new Date('2026-10-15T02:00:00Z'))).toBe('hoje 22:30')
    expect(quando('2026-10-13T12:10:00Z', agora)).toBe('ontem 09:10')
  })

  it('mostra dia/mês para datas mais antigas', () => {
    expect(quando('2026-10-12T17:00:00Z', agora)).toBe('12/10 14:00')
  })
})

describe('descreverQuantidade', () => {
  it('zero é acabou', () => {
    expect(descreverQuantidade(0)).toBe('Acabou')
    expect(descreverQuantidade(3)).toBe('Restam 3')
  })
})
