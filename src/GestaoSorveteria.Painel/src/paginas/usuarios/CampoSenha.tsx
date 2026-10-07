import type { Perfil } from '../../api/tipos.ts'
import { PIN_MAXIMO, PIN_MINIMO, SENHA_ADMIN_MINIMO } from './politicaSenha.ts'

interface Props {
  perfil: Perfil
  valor: string
  aoMudar: (valor: string) => void
  rotulo?: string
}

/** Campo de PIN (atendente) ou senha (administrador), com a dica certa para cada perfil. */
export default function CampoSenha({ perfil, valor, aoMudar, rotulo }: Props) {
  const pin = perfil === 'Atendente'
  return (
    <label className="campo">
      <span>{rotulo ?? (pin ? 'PIN' : 'Senha')}</span>
      <input
        type={pin ? 'text' : 'password'}
        inputMode={pin ? 'numeric' : undefined}
        autoComplete="new-password"
        required
        minLength={pin ? PIN_MINIMO : SENHA_ADMIN_MINIMO}
        maxLength={pin ? PIN_MAXIMO : 100}
        pattern={pin ? '[0-9]*' : undefined}
        value={valor}
        onChange={(e) => aoMudar(e.target.value)}
      />
      <span className="suave dica">
        {pin
          ? `De ${PIN_MINIMO} a ${PIN_MAXIMO} números. O atendente digita no celular do balcão.`
          : `No mínimo ${SENHA_ADMIN_MINIMO} caracteres. Usada para entrar neste painel.`}
      </span>
    </label>
  )
}
