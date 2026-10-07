import { describe, expect, it } from 'vitest'
import { validarSenha } from './politicaSenha.ts'

// RN-US-03: PIN de 4 a 6 dígitos para atendente; senha de 8+ caracteres para Admin.
describe('validarSenha', () => {
  it.each(['1234', '123456', '0000'])('aceita PIN de atendente %s', (pin) => {
    expect(validarSenha('Atendente', pin)).toBeNull()
  })

  it.each(['123', '1234567', '12a4', '', ' 1234'])('recusa PIN de atendente "%s"', (pin) => {
    expect(validarSenha('Atendente', pin)).toMatch(/PIN/)
  })

  it('aceita senha de administrador com 8 ou mais caracteres', () => {
    expect(validarSenha('Admin', 'senha123')).toBeNull()
  })

  it.each(['curta', '       ', '1234567'])('recusa senha de administrador "%s"', (senha) => {
    expect(validarSenha('Admin', senha)).toMatch(/8 caracteres/)
  })
})
