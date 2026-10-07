import type { Perfil } from '../../api/tipos.ts'

// Espelho da PoliticaSenha do Domain (RN-US-03) só para avisar antes de enviar; quem decide é a API.
export const PIN_MINIMO = 4
export const PIN_MAXIMO = 6
export const SENHA_ADMIN_MINIMO = 8

/** Mensagem de erro para a tela, ou null quando a senha/PIN respeita a política do perfil. */
export function validarSenha(perfil: Perfil, senha: string): string | null {
  if (perfil === 'Atendente') {
    if (!/^\d+$/.test(senha) || senha.length < PIN_MINIMO || senha.length > PIN_MAXIMO) {
      return `O PIN deve ter de ${PIN_MINIMO} a ${PIN_MAXIMO} números.`
    }
    return null
  }
  if (senha.trim().length < SENHA_ADMIN_MINIMO) {
    return `A senha do administrador deve ter no mínimo ${SENHA_ADMIN_MINIMO} caracteres.`
  }
  return null
}
