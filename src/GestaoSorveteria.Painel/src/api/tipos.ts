// Espelho em TypeScript dos DTOs de GestaoSorveteria.Contracts (JSON em camelCase).
// Mudou um DTO na API → atualize aqui.

/** Contracts/Auth/LoginRequest */
export interface LoginRequest {
  login: string
  senha: string
}

/** Contracts/Auth/LoginResponse */
export interface LoginResponse {
  token: string
  /** Data/hora UTC em ISO 8601 em que o token deixa de valer. */
  expiraEmUtc: string
  usuario: UsuarioDto
}

/** Contracts/Auth/UsuarioDto */
export interface UsuarioDto {
  id: string
  nome: string
  login: string
  perfil: Perfil
  ativo: boolean
}

export type Perfil = 'Admin' | 'Atendente'

/** Só a administração da loja entra no painel. */
export const PERFIL_ADMIN: Perfil = 'Admin'
