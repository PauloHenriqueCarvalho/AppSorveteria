import { describe, expect, it } from 'vitest'
import { descreverPeriodo, descreverPeriodoCurto, descreverTrecho, fimDoMesAnterior, inicioDoMesAntes, nomeDoDiaDaSemana, rotuloCurto } from './relatorios.ts'

describe('descreverTrecho', () => {
  it('dia com o dia da semana', () => {
    expect(descreverTrecho({ de: '2026-10-14', ate: '2026-10-14' }, 'Dia')).toBe('qua 14/10')
  })

  it('semana de segunda a domingo', () => {
    expect(descreverTrecho({ de: '2026-10-05', ate: '2026-10-11' }, 'Semana')).toBe('05/10 a 11/10')
  })

  it('mês inteiro e mês recortado', () => {
    expect(descreverTrecho({ de: '2026-09-01', ate: '2026-09-30' }, 'Mes')).toBe('set/26')
    expect(descreverTrecho({ de: '2026-10-01', ate: '2026-10-14' }, 'Mes')).toBe('out/26 (01 a 14)')
    expect(descreverTrecho({ de: '2028-02-01', ate: '2028-02-29' }, 'Mes')).toBe('fev/28')
  })
})

describe('rótulos do período', () => {
  it('período com e sem ano repetido', () => {
    expect(descreverPeriodo('2026-10-01', '2026-10-14')).toBe('01/10 a 14/10/2026')
    expect(descreverPeriodo('2025-12-01', '2026-01-31')).toBe('01/12/2025 a 31/01/2026')
    expect(descreverPeriodo('2026-10-14', '2026-10-14')).toBe('14/10/2026')
    expect(descreverPeriodoCurto('2026-09-01', '2026-09-14')).toBe('01/09 a 14/09')
  })

  it('rótulo curto da coluna e dia da semana', () => {
    expect(rotuloCurto({ de: '2026-10-05' }, 'Semana')).toBe('05/10')
    expect(rotuloCurto({ de: '2026-10-01' }, 'Mes')).toBe('out')
    expect(nomeDoDiaDaSemana(0)).toBe('Domingo')
  })
})

describe('atalhos de mês', () => {
  it('mês passado inteiro, inclusive na virada do ano', () => {
    expect(inicioDoMesAntes('2026-10-14', 1)).toBe('2026-09-01')
    expect(fimDoMesAnterior('2026-10-14')).toBe('2026-09-30')
    expect(inicioDoMesAntes('2026-01-10', 1)).toBe('2025-12-01')
    expect(fimDoMesAnterior('2026-03-31')).toBe('2026-02-28')
  })

  it('três meses começa no primeiro dia de dois meses antes', () => {
    expect(inicioDoMesAntes('2026-10-14', 2)).toBe('2026-08-01')
  })
})
