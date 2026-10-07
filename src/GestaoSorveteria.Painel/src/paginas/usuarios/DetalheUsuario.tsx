import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import type { UsuarioDto } from '../../api/tipos.ts'
import { alterarAtivoUsuario, CHAVE_USUARIOS, obterUsuario, redefinirSenha } from '../../api/usuarios.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { useSessao } from '../../sessao/contexto.ts'
import CampoSenha from './CampoSenha.tsx'
import { TEXTO_PERFIL } from './perfil.ts'
import { validarSenha } from './politicaSenha.ts'

/** /usuarios/:id — redefinir PIN/senha, ativar e desativar. Nome e login não mudam (histórico das vendas). */
export default function DetalheUsuario() {
  const { id } = useParams()
  const usuario = useQuery({ queryKey: [...CHAVE_USUARIOS, id], queryFn: () => obterUsuario(id!) })

  return (
    <>
      <p>
        <Link to="/usuarios">← Usuários</Link>
      </p>
      <Consulta consulta={usuario}>{(dados) => <Painel usuario={dados} />}</Consulta>
    </>
  )
}

function Painel({ usuario }: { usuario: UsuarioDto }) {
  const queryClient = useQueryClient()
  const { usuario: logado } = useSessao()
  const souEu = logado?.id === usuario.id

  const [novaSenha, setNovaSenha] = useState('')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  function atualizarCache(atualizado: UsuarioDto) {
    queryClient.setQueryData([...CHAVE_USUARIOS, atualizado.id], atualizado)
    void queryClient.invalidateQueries({ queryKey: CHAVE_USUARIOS, exact: true })
  }

  const redefinir = useMutation({
    mutationFn: (senha: string) => redefinirSenha(usuario.id, { novaSenha: senha }),
    onSuccess: (atualizado) => {
      atualizarCache(atualizado)
      setNovaSenha('')
    },
  })

  const ativar = useMutation({
    mutationFn: (ativo: boolean) => alterarAtivoUsuario(usuario.id, ativo),
    onSuccess: atualizarCache,
  })

  function enviarSenha(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const erro = validarSenha(usuario.perfil, novaSenha)
    setErroCampo(erro)
    if (erro) {
      return
    }
    redefinir.mutate(novaSenha)
  }

  const ocupado = redefinir.isPending || ativar.isPending
  const pin = usuario.perfil === 'Atendente'

  return (
    <>
      <div className="cartao">
        <h1>{usuario.nome}</h1>
        <p className="suave">
          Login <strong>{usuario.login}</strong> · {TEXTO_PERFIL[usuario.perfil]}
          {souEu && ' · você'}
        </p>
        {!usuario.ativo && (
          <p className="alerta alerta-aviso">
            Este usuário está desativado: não consegue entrar no app nem no painel. O histórico dele continua guardado.
          </p>
        )}
      </div>

      <form className="cartao formulario" onSubmit={enviarSenha}>
        <h2>{pin ? 'Redefinir PIN' : 'Redefinir senha'}</h2>
        <p className="suave">
          {pin
            ? 'Use quando o atendente esquecer o PIN. O PIN antigo deixa de valer na hora.'
            : 'A senha antiga deixa de valer na hora.'}
        </p>
        <CampoSenha perfil={usuario.perfil} valor={novaSenha} aoMudar={setNovaSenha} rotulo={pin ? 'Novo PIN' : 'Nova senha'} />
        {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
        {redefinir.isError && <p className="alerta alerta-erro">{mensagemDoErro(redefinir.error)}</p>}
        {redefinir.isSuccess && !redefinir.isPending && (
          <p className="alerta alerta-sucesso">{pin ? 'PIN redefinido.' : 'Senha redefinida.'}</p>
        )}
        <button type="submit" className="botao" disabled={ocupado}>
          {redefinir.isPending ? 'Salvando…' : pin ? 'Salvar novo PIN' : 'Salvar nova senha'}
        </button>
      </form>

      <div className="cartao formulario">
        <h2>{usuario.ativo ? 'Desativar' : 'Ativar'}</h2>
        {souEu ? (
          <p className="suave">Você não pode desativar o seu próprio usuário. Peça para outro administrador.</p>
        ) : (
          <p className="suave">
            {usuario.ativo
              ? 'Desativar impede a pessoa de entrar. As vendas e os caixas dela continuam registrados; nada é apagado.'
              : 'Ativar permite que a pessoa entre de novo com o mesmo login.'}
          </p>
        )}
        {ativar.isError && <p className="alerta alerta-erro">{mensagemDoErro(ativar.error)}</p>}
        {!souEu && (
          <button
            type="button"
            className={usuario.ativo ? 'botao botao-secundario' : 'botao'}
            disabled={ocupado}
            onClick={() => ativar.mutate(!usuario.ativo)}
          >
            {ativar.isPending ? 'Aguarde…' : usuario.ativo ? 'Desativar usuário' : 'Ativar usuário'}
          </button>
        )}
      </div>
    </>
  )
}
