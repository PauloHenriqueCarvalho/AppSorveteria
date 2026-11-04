import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import {
  alterarStatusSugestao,
  anexarImagem,
  atualizarSugestao,
  baixarImagem,
  CHAVE_SUGESTOES,
  comentarSugestao,
  obterSugestao,
  removerImagem,
  type StatusSugestao,
  type SugestaoDto,
  type SugestaoImagemDto,
} from '../../api/sugestoes.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Secao, Selo } from '../../componentes/Ui.tsx'
import { formatarDataHora } from '../../formatacao.ts'
import { formatarTamanho, nomeArea, nomeImportancia, nomeTipo, prepararImagem, status, STATUS } from '../../sugestoes.ts'
import EscolherImagens from './EscolherImagens.tsx'
import FormularioSugestao from './FormularioSugestao.tsx'

/** /sugestoes/:id — a sugestão, as imagens, a conversa e o andamento (RN-SG-02..05). */
export default function DetalheSugestao() {
  const { id = '' } = useParams()
  const sugestao = useQuery({ queryKey: [...CHAVE_SUGESTOES, id], queryFn: () => obterSugestao(id) })
  return <Consulta consulta={sugestao}>{(dados) => <Conteudo s={dados} />}</Consulta>
}

function useAtualizar(id: string) {
  const queryClient = useQueryClient()
  return async (dto: SugestaoDto) => {
    queryClient.setQueryData([...CHAVE_SUGESTOES, id], dto)
    await queryClient.invalidateQueries({ queryKey: CHAVE_SUGESTOES, exact: true })
  }
}

function Conteudo({ s }: { s: SugestaoDto }) {
  const local = useLocation()
  const falhasImagens = (local.state as { falhasImagens?: string[] } | null)?.falhasImagens ?? []
  const atualizar = useAtualizar(s.id)
  const [editando, setEditando] = useState(false)
  const st = status(s.status)

  const editar = useMutation({
    mutationFn: (dados: Parameters<typeof atualizarSugestao>[1]) => atualizarSugestao(s.id, dados),
    onSuccess: async (dto) => {
      await atualizar(dto)
      setEditando(false)
    },
  })

  return (
    <>
      <p className="voltar">
        <Link to="/sugestoes">← Sugestões</Link>
      </p>
      <CabecalhoPagina
        titulo={s.titulo}
        subtitulo={`Pedida por ${s.criadaPorNome} em ${formatarDataHora(s.criadaEm)}`}
        acoes={
          !s.encerrada &&
          !editando && (
            <button type="button" className="botao botao-secundario" onClick={() => setEditando(true)}>
              Editar
            </button>
          )
        }
      />

      {falhasImagens.length > 0 && (
        <p className="alerta alerta-erro">
          A sugestão foi salva, mas estas imagens não foram: {falhasImagens.join(' · ')}. Tente anexar de novo abaixo.
        </p>
      )}

      {editando ? (
        <FormularioSugestao
          inicial={{ titulo: s.titulo, descricao: s.descricao, tipo: s.tipo, area: s.area, importancia: s.importancia }}
          salvando={editar.isPending}
          erro={editar.isError ? mensagemDoErro(editar.error) : null}
          textoBotao="Salvar alterações"
          aoSalvar={(dados) => editar.mutate(dados)}
          aoCancelar={() => setEditando(false)}
        />
      ) : (
        <Secao
          titulo="Sugestão"
          acao={<Selo tom={st.tom}>{st.nome}</Selo>}
        >
          <p className="sugestao-selos">
            <Selo>{nomeTipo(s.tipo)}</Selo> <Selo>{nomeArea(s.area)}</Selo> <Selo>Importância {nomeImportancia(s.importancia).toLowerCase()}</Selo>
          </p>
          <p className="sugestao-texto">{s.descricao}</p>
        </Secao>
      )}

      <Imagens s={s} />
      <Conversa s={s} />
      <Andamento s={s} />
    </>
  )
}

function Imagens({ s }: { s: SugestaoDto }) {
  const atualizar = useAtualizar(s.id)
  const [novas, setNovas] = useState<File[]>([])

  const enviar = useMutation({
    mutationFn: async () => {
      let dto = s
      for (const arquivo of novas) {
        const pronta = await prepararImagem(arquivo)
        dto = await anexarImagem(s.id, pronta.conteudo, pronta.nome)
      }
      return dto
    },
    onSuccess: async (dto) => {
      await atualizar(dto)
      setNovas([])
    },
  })

  const tirar = useMutation({
    mutationFn: (imagemId: string) => removerImagem(s.id, imagemId),
    onSuccess: atualizar,
  })

  if (s.encerrada && s.imagens.length === 0) {
    return null
  }

  return (
    <Secao titulo={`Imagens (${s.imagens.length})`}>
      {s.imagens.length > 0 && (
        <ul className="sugestao-imagens">
          {s.imagens.map((imagem) => (
            <Miniatura
              key={imagem.id}
              sugestaoId={s.id}
              imagem={imagem}
              aoTirar={s.encerrada ? undefined : () => tirar.mutate(imagem.id)}
              tirando={tirar.isPending && tirar.variables === imagem.id}
            />
          ))}
        </ul>
      )}
      {tirar.isError && <p className="alerta alerta-erro">{mensagemDoErro(tirar.error)}</p>}
      {!s.encerrada && (
        <>
          <EscolherImagens arquivos={novas} aoMudar={setNovas} jaAnexadas={s.imagens.length} />
          {novas.length > 0 && (
            <div className="acoes acoes-inicio">
              <button type="button" className="botao" disabled={enviar.isPending} onClick={() => enviar.mutate()}>
                {enviar.isPending ? 'Enviando…' : `Anexar ${novas.length === 1 ? 'imagem' : `${novas.length} imagens`}`}
              </button>
            </div>
          )}
          {enviar.isError && <p className="alerta alerta-erro">{mensagemDoErro(enviar.error)}</p>}
        </>
      )}
    </Secao>
  )
}

function Miniatura({
  sugestaoId,
  imagem,
  aoTirar,
  tirando,
}: {
  sugestaoId: string
  imagem: SugestaoImagemDto
  aoTirar?: () => void
  tirando: boolean
}) {
  const conteudo = useQuery({
    queryKey: [...CHAVE_SUGESTOES, sugestaoId, 'imagem', imagem.id],
    queryFn: () => baixarImagem(sugestaoId, imagem.id),
    staleTime: Infinity,
  })
  const url = useMemo(() => (conteudo.data ? URL.createObjectURL(conteudo.data) : null), [conteudo.data])
  useEffect(() => () => {
    if (url) {
      URL.revokeObjectURL(url)
    }
  }, [url])

  return (
    <li className="sugestao-imagem">
      {url ? (
        <a href={url} target="_blank" rel="noreferrer" title="Abrir a imagem inteira">
          <img src={url} alt={imagem.nomeArquivo} />
        </a>
      ) : (
        <span className="sugestao-imagem-vazia suave">{conteudo.isError ? 'Não carregou' : 'Carregando…'}</span>
      )}
      <span className="suave">
        {imagem.nomeArquivo} · {formatarTamanho(imagem.tamanhoBytes)}
      </span>
      {aoTirar && (
        <button type="button" className="botao botao-secundario botao-pequeno" disabled={tirando} onClick={aoTirar}>
          {tirando ? 'Tirando…' : 'Tirar'}
        </button>
      )}
    </li>
  )
}

function Conversa({ s }: { s: SugestaoDto }) {
  const atualizar = useAtualizar(s.id)
  const [texto, setTexto] = useState('')
  const comentar = useMutation({
    mutationFn: () => comentarSugestao(s.id, texto.trim()),
    onSuccess: async (dto) => {
      await atualizar(dto)
      setTexto('')
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (texto.trim()) {
      comentar.mutate()
    }
  }

  return (
    <Secao titulo="Conversa">
      {s.comentarios.length === 0 ? (
        <p className="suave">Nenhuma mensagem ainda. O Paulo responde por aqui.</p>
      ) : (
        <ol className="sugestao-conversa">
          {s.comentarios.map((c) => (
            <li key={c.id} className={c.usuarioId === s.criadaPorUsuarioId ? 'mensagem mensagem-dona' : 'mensagem'}>
              <span className="mensagem-autor">
                <strong>{c.usuarioNome}</strong> <span className="suave">· {formatarDataHora(c.em)}</span>
              </span>
              <p className="sugestao-texto">{c.texto}</p>
            </li>
          ))}
        </ol>
      )}
      <form className="formulario" onSubmit={enviar}>
        <label className="campo">
          <span>Nova mensagem</span>
          <textarea rows={3} maxLength={2000} value={texto} onChange={(e) => setTexto(e.target.value)} placeholder="Escreva aqui" />
        </label>
        {comentar.isError && <p className="alerta alerta-erro">{mensagemDoErro(comentar.error)}</p>}
        <div className="acoes acoes-inicio">
          <button type="submit" className="botao" disabled={comentar.isPending || !texto.trim()}>
            {comentar.isPending ? 'Enviando…' : 'Enviar mensagem'}
          </button>
        </div>
      </form>
    </Secao>
  )
}

function Andamento({ s }: { s: SugestaoDto }) {
  const atualizar = useAtualizar(s.id)
  const [novo, setNovo] = useState<StatusSugestao>(s.status)
  const [comentario, setComentario] = useState('')
  const mudar = useMutation({
    mutationFn: () => alterarStatusSugestao(s.id, novo, comentario.trim() || null),
    onSuccess: async (dto) => {
      await atualizar(dto)
      setComentario('')
    },
  })

  return (
    <Secao titulo="Andamento">
      <p className="suave">
        Nova → Em análise → Planejada → Feita. Quem cuida do sistema atualiza aqui; cada mudança aparece na conversa.
      </p>
      <form
        className="formulario"
        onSubmit={(e) => {
          e.preventDefault()
          mudar.mutate()
        }}
      >
        <div className="grade-campos">
          <label className="campo">
            <span>Andamento</span>
            <select value={novo} onChange={(e) => setNovo(e.target.value as StatusSugestao)}>
              {STATUS.map((x) => (
                <option key={x.valor} value={x.valor}>
                  {x.nome}
                </option>
              ))}
            </select>
          </label>
          <label className="campo">
            <span>{novo === 'Recusada' ? 'Por que não vai ser feita' : 'Comentário (opcional)'}</span>
            <input
              required={novo === 'Recusada'}
              maxLength={1900}
              value={comentario}
              onChange={(e) => setComentario(e.target.value)}
              placeholder={novo === 'Planejada' ? 'Ex.: entra na versão de dezembro' : ''}
            />
          </label>
        </div>
        {mudar.isError && <p className="alerta alerta-erro">{mensagemDoErro(mudar.error)}</p>}
        <div className="acoes acoes-inicio">
          <button type="submit" className="botao botao-secundario" disabled={mudar.isPending || novo === s.status}>
            {mudar.isPending ? 'Salvando…' : 'Atualizar andamento'}
          </button>
        </div>
      </form>
    </Secao>
  )
}
