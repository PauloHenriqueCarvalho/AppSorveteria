import { requisicao } from './cliente.ts'
import type { LoginRequest, LoginResponse, UsuarioDto } from './tipos.ts'

/** POST /api/auth/login */
export function entrar(dados: LoginRequest): Promise<LoginResponse> {
  return requisicao<LoginResponse>('/api/auth/login', { metodo: 'POST', corpo: dados, autenticado: false })
}

/** GET /api/auth/me — confere se o token ainda vale e quem é o usuário. */
export function obterUsuarioAtual(): Promise<UsuarioDto> {
  return requisicao<UsuarioDto>('/api/auth/me')
}
