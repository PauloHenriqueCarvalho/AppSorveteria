import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import type { Perfil } from '../../api/tipos.ts'
import { CHAVE_USUARIOS, criarUsuario } from '../../api/usuarios.ts'
import CampoSenha from './CampoSenha.tsx'
import { validarSenha } from './politicaSenha.ts'

/** /usuarios/novo — cria um atendente (PIN) ou outro administrador (senha). */
export default function NovoUsuario() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [nome, setNome] = useState('')
  const [login, setLogin] = useState('')
  const [perfil, setPerfil] = useState<Perfil>('Atendente')
  const [senha, setSenha] = useState('')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const salvar = useMutation({
    mutationFn: criarUsuario,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_USUARIOS })
      navigate('/usuarios')
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const erro = validarSenha(perfil, senha)
    setErroCampo(erro)
    if (erro) {
      return
    }
    salvar.mutate({ nome: nome.trim(), login: login.trim().toLowerCase(), senha, perfil })
  }

  return (
    <>
      <p>
        <Link to="/usuarios">← Usuários</Link>
      </p>

      <form className="cartao formulario" onSubmit={enviar}>
        <h1>Novo usuário</h1>

        <label className="campo">
          <span>Nome</span>
          <input required minLength={2} maxLength={100} value={nome} onChange={(e) => setNome(e.target.value)} />
        </label>

        <label className="campo">
          <span>Login</span>
          <input
            required
            minLength={3}
            maxLength={50}
            autoCapitalize="none"
            autoCorrect="off"
            spellCheck={false}
            pattern="[a-zA-Z0-9._]+"
            title="Só letras, números, ponto e sublinhado, sem espaços."
            value={login}
            onChange={(e) => setLogin(e.target.value)}
          />
          <span className="suave dica">É o que a pessoa digita para entrar. Só letras, números, ponto e sublinhado.</span>
        </label>

        <fieldset className="campo">
          <legend>Perfil</legend>
          <label className="campo-marcar">
            <input type="radio" name="perfil" checked={perfil === 'Atendente'} onChange={() => setPerfil('Atendente')} />
            <span>
              Atendente <span className="suave">(usa o app do balcão, entra com PIN)</span>
            </span>
          </label>
          <label className="campo-marcar">
            <input type="radio" name="perfil" checked={perfil === 'Admin'} onChange={() => setPerfil('Admin')} />
            <span>
              Administrador <span className="suave">(acessa este painel, entra com senha)</span>
            </span>
          </label>
        </fieldset>

        <CampoSenha perfil={perfil} valor={senha} aoMudar={setSenha} />

        {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
        {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}

        <button type="submit" className="botao" disabled={salvar.isPending}>
          {salvar.isPending ? 'Salvando…' : 'Salvar'}
        </button>
      </form>
    </>
  )
}
