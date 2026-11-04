import { describe, expect, it } from 'vitest'
import { dimensaoReduzida, formatarTamanho, nomeEmJpeg, status } from './sugestoes.ts'

describe('sugestões', () => {
  it('reduz a foto grande mantendo a proporção e não aumenta a pequena', () => {
    expect(dimensaoReduzida(4000, 3000)).toEqual({ largura: 1600, altura: 1200 })
    expect(dimensaoReduzida(1080, 2400)).toEqual({ largura: 720, altura: 1600 })
    expect(dimensaoReduzida(800, 600)).toEqual({ largura: 800, altura: 600 })
  })

  it('troca a extensão para .jpg', () => {
    expect(nomeEmJpeg('IMG_0001.HEIC')).toBe('IMG_0001.jpg')
    expect(nomeEmJpeg('print')).toBe('print.jpg')
  })

  it('mostra o tamanho em KB ou MB', () => {
    expect(formatarTamanho(300)).toBe('1 KB')
    expect(formatarTamanho(250 * 1024)).toBe('250 KB')
    expect(formatarTamanho(1.5 * 1024 * 1024)).toBe('1,5 MB')
  })

  it('dá nome e cor ao andamento', () => {
    expect(status('Recusada').nome).toBe('Não vai ser feita')
    expect(status('Feita').tom).toBe('bom')
  })
})
