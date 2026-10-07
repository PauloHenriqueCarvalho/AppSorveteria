import type { Perfil } from '../../api/tipos.ts'

/** Como o perfil aparece para a dona. */
export const TEXTO_PERFIL: Record<Perfil, string> = {
  Admin: 'Administrador',
  Atendente: 'Atendente',
}
