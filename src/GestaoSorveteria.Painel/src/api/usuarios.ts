import { requisicao } from './cliente.ts'
import type { CriarUsuarioRequest, RedefinirSenhaRequest, UsuarioDto } from './tipos.ts'

/** Chave do TanStack Query para a lista; o usuário único usa [...CHAVE_USUARIOS, id]. */
export const CHAVE_USUARIOS = ['usuarios'] as const

/** GET /api/usuarios (Admin) — todos, inclusive os desativados. */
export function listarUsuarios(): Promise<UsuarioDto[]> {
  return requisicao<UsuarioDto[]>('/api/usuarios')
}

/** GET /api/usuarios/{id} (Admin) */
export function obterUsuario(id: string): Promise<UsuarioDto> {
  return requisicao<UsuarioDto>(`/api/usuarios/${encodeURIComponent(id)}`)
}

/** POST /api/usuarios (Admin) — atendente com PIN ou outro Admin com senha (RN-US-03). */
export function criarUsuario(dados: CriarUsuarioRequest): Promise<UsuarioDto> {
  return requisicao<UsuarioDto>('/api/usuarios', { metodo: 'POST', corpo: dados })
}

/** POST /api/usuarios/{id}/redefinir-senha (Admin, RN-US-08). A senha antiga não é pedida. */
export function redefinirSenha(id: string, dados: RedefinirSenhaRequest): Promise<UsuarioDto> {
  return requisicao<UsuarioDto>(`/api/usuarios/${encodeURIComponent(id)}/redefinir-senha`, {
    metodo: 'POST',
    corpo: dados,
  })
}

/** POST /api/usuarios/{id}/ativar ou /desativar (Admin). Usuário nunca é apagado (RN-US-07). */
export function alterarAtivoUsuario(id: string, ativo: boolean): Promise<UsuarioDto> {
  const acao = ativo ? 'ativar' : 'desativar'
  return requisicao<UsuarioDto>(`/api/usuarios/${encodeURIComponent(id)}/${acao}`, { metodo: 'POST' })
}
