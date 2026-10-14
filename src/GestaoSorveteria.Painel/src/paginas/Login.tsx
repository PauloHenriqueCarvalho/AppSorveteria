import { useMutation } from '@tanstack/react-query'
import { useEffect, useState, type FormEvent } from 'react'
import { Navigate } from 'react-router'
import { entrar as entrarNaApi } from '../api/auth.ts'
import { deveTentarDeNovo, ErroApi, ESPERA_ENTRE_TENTATIVAS_MS, mensagemDoErro } from '../api/erros.ts'
import { PERFIL_ADMIN, type LoginRequest } from '../api/tipos.ts'
import ConectandoServidor from '../componentes/ConectandoServidor.tsx'
import { LOGO_LOJA, NOME_LOJA } from '../marca.ts'
import { MENSAGEM_SO_ADMIN, useSessao } from '../sessao/contexto.ts'

// Depois deste tempo sem resposta, avisa que a API pode estar acordando.
const AVISAR_DEMORA_MS = 3_000

async function entrarComoAdmin(dados: LoginRequest) {
  const resposta = await entrarNaApi(dados)
  if (resposta.usuario.perfil !== PERFIL_ADMIN) {
    throw new ErroApi(403, MENSAGEM_SO_ADMIN)
  }
  return resposta
}

export default function Login() {
  const { usuario, aviso, entrar } = useSessao()
  const [login, setLogin] = useState('')
  const [senha, setSenha] = useState('')
  const [demorou, setDemorou] = useState(false)

  const mutacao = useMutation({
    mutationFn: entrarComoAdmin,
    retry: deveTentarDeNovo,
    retryDelay: ESPERA_ENTRE_TENTATIVAS_MS,
    onSuccess: entrar,
  })

  useEffect(() => {
    if (!mutacao.isPending) {
      return
    }
    const id = setTimeout(() => setDemorou(true), AVISAR_DEMORA_MS)
    return () => {
      clearTimeout(id)
      setDemorou(false)
    }
  }, [mutacao.isPending])

  if (usuario) {
    return <Navigate to="/" replace />
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    mutacao.mutate({ login: login.trim(), senha })
  }

  const conectando = mutacao.isPending && (demorou || mutacao.failureCount > 0)

  return (
    <main className="tela-login">
      <form className="cartao formulario login" onSubmit={enviar}>
        <div className="login-marca">
          <img src={LOGO_LOJA} alt="" width={64} height={74} />
          <div>
            <h1>{NOME_LOJA}</h1>
            <p className="suave">Painel da administração</p>
          </div>
        </div>

        {aviso && !mutacao.isPending && <p className="alerta alerta-aviso">{aviso}</p>}

        <label className="campo">
          <span>Usuário</span>
          <input
            name="login"
            autoComplete="username"
            autoCapitalize="none"
            required
            minLength={3}
            maxLength={50}
            value={login}
            onChange={(e) => setLogin(e.target.value)}
          />
        </label>

        <label className="campo">
          <span>Senha</span>
          <input
            name="senha"
            type="password"
            autoComplete="current-password"
            required
            maxLength={100}
            value={senha}
            onChange={(e) => setSenha(e.target.value)}
          />
        </label>

        {conectando && <ConectandoServidor falhas={mutacao.failureCount} />}

        {mutacao.isError && <p className="alerta alerta-erro">{mensagemDoErro(mutacao.error)}</p>}

        <button type="submit" className="botao" disabled={mutacao.isPending}>
          {mutacao.isPending ? 'Entrando…' : 'Entrar'}
        </button>
      </form>
    </main>
  )
}
