import { describe, expect, it } from 'vitest'
import { celulaDinheiro, montarCsv } from './exportar.ts'

describe('montarCsv', () => {
  it('usa ponto e vírgula, quebra de linha do Windows e BOM para o Excel', () => {
    const csv = montarCsv(['Nº', 'Total'], [[63, celulaDinheiro(34)]])
    expect(csv).toBe('\uFEFFNº;Total\r\n63;34,00\r\n')
  })

  it('protege texto com ponto e vírgula, aspas ou quebra de linha', () => {
    const csv = montarCsv(['Motivo'], [['troco; cliente "voltou"'], ['linha\nnova']])
    expect(csv).toBe('\uFEFFMotivo\r\n"troco; cliente ""voltou"""\r\n"linha\nnova"\r\n')
  })

  it('deixa célula vazia para valor ausente', () => {
    expect(montarCsv(['A', 'B'], [[null, undefined]])).toBe('\uFEFFA;B\r\n;\r\n')
  })
})

describe('celulaDinheiro', () => {
  it('formata com vírgula decimal e sem separador de milhar', () => {
    expect(celulaDinheiro(1234.5)).toBe('1234,50')
    expect(celulaDinheiro(-12.5)).toBe('-12,50')
    expect(celulaDinheiro(null)).toBe('')
  })
})
