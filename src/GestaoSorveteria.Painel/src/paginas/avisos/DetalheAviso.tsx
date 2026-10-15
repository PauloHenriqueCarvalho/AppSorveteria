import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { aindaFalta, CHAVE_AVISOS, fecharAviso, listarAvisos } from '../../api/avisos.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { AvisoDto, PrioridadeAviso } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Secao, Selo } from '../../componentes/Ui.tsx'
import { descreverQuantidade, nomeDaPrioridade, PRIORIDADES, quando, TOM_DA_PRIORIDADE } from '../../estoque.ts'

const ORDEM: PrioridadeAviso[] = ['Media', 'Alta', 'Urgente']
const MOTIVOS = ['Comprei no mercado', 'Chegou fora do pedido', 'Não precisa mais']

/** /avisos/:id — detalhe, "Ainda falta" (prioridade só sobe, RN-AV-03) e fechar com motivo (RN-AV-05). */
export default function DetalheAviso() {
  const { id } = useParams()
  const avisos = useQuery({ queryKey: [...CHAVE_AVISOS, 'fechados'], queryFn: () => listarAvisos(true) })
  return (
    <Consulta consulta={avisos}>
      {(lista) => {
        const aviso = lista.find((a) => a.id === id)
        if (!aviso) {
          return (
            <section className="secao">
              <p className="alerta alerta-erro">Aviso não encontrado (avisos fechados há mais de 30 dias não aparecem).</p>
              <Link to="/avisos">Voltar para os avisos</Link>
            </section>
          )
        }
        return <Detalhe key={`${aviso.id}-${aviso.atualizadoEm}-${aviso.status}`} aviso={aviso} />
      }}
    </Consulta>
  )
}

function Detalhe({ aviso }: { aviso: AvisoDto }) {
  const queryClient = useQueryClient()
  const { state } = useLocation() as { state: { jaExistia?: boolean } | null }
  const aberto = aviso.status === 'Aberto'

  const [quantidade, setQuantidade] = useState(String(aviso.quantidadeAtual))
  const [prioridade, setPrioridade] = useState<PrioridadeAviso>(aviso.prioridade)
  const [motivo, setMotivo] = useState('')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const atualizar = useMutation({
    mutationFn: () => aindaFalta(aviso.id, { quantidadeAtual: Number(quantidade), prioridade, observacao: null }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CHAVE_AVISOS }),
  })
  const fechar = useMutation({
    mutationFn: () => fecharAviso(aviso.id, motivo.trim()),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CHAVE_AVISOS }),
  })

  function enviarAindaFalta(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const valor = Number(quantidade)
    if (quantidade.trim() === '' || !Number.isInteger(valor) || valor < 0) {
      setErroCampo('Informe quanto tem agora (0 = acabou).')
      return
    }
    setErroCampo(null)
    atualizar.mutate()
  }

  const permitidas = PRIORIDADES.filter((p) => ORDEM.indexOf(p.valor) >= ORDEM.indexOf(aviso.prioridade))
  const ocupado = atualizar.isPending || fechar.isPending

  return (
    <>
      <p className="voltar">
        <Link to="/avisos">← Avisos de estoque</Link>
      </p>
      <CabecalhoPagina
        titulo={aviso.descricao}
        subtitulo={[aviso.grupoNome, aviso.fornecedorNome].filter(Boolean).join(' · ') || 'Item fora do catálogo'}
        acoes={
          <span className="selos">
            <Selo tom={aberto ? TOM_DA_PRIORIDADE[aviso.prioridade] : 'neutro'}>{nomeDaPrioridade(aviso.prioridade)}</Selo>
            <Selo tom={aberto ? 'atencao' : 'bom'}>{aberto ? 'Aberto' : 'Fechado'}</Selo>
          </span>
        }
      />

      {state?.jaExistia && (
        <p className="alerta alerta-aviso">Este item já tinha aviso aberto: ele foi atualizado em vez de criar outro.</p>
      )}

      <Secao titulo="Situação">
        <dl className="ficha">
          <div className="ficha-linha">
            <dt>Agora</dt>
            <dd className="forte">{descreverQuantidade(aviso.quantidadeAtual)}</dd>
          </div>
          <div className="ficha-linha">
            <dt>Avisou</dt>
            <dd>
              {aviso.criadoPorNome} · {quando(aviso.criadoEm)}
            </dd>
          </div>
          {aviso.atualizadoPorNome && (
            <div className="ficha-linha">
              <dt>Ainda falta</dt>
              <dd>
                {aviso.atualizadoPorNome} · {quando(aviso.atualizadoEm)}
              </dd>
            </div>
          )}
          {aviso.observacao && (
            <div className="ficha-linha">
              <dt>Observação</dt>
              <dd>{aviso.observacao}</dd>
            </div>
          )}
          {aviso.prioridade === 'Urgente' && (
            <div className="ficha-linha">
              <dt>WhatsApp</dt>
              <dd>{aviso.whatsAppAbertoEm ? `aberto para envio ${quando(aviso.whatsAppAbertoEm)}` : 'ainda não foi aberto'}</dd>
            </div>
          )}
          {!aberto && (
            <div className="ficha-linha">
              <dt>Fechado</dt>
              <dd>
                {aviso.fechadoPorNome ?? 'Pela entrega'} · {quando(aviso.fechadoEm!)} — {aviso.motivoFechamento}
              </dd>
            </div>
          )}
        </dl>
      </Secao>

      {aberto && (
        <>
          <form className="secao formulario" onSubmit={enviarAindaFalta}>
            <h2>Ainda falta</h2>
            <p className="suave">Atualize quanto tem agora. A prioridade só pode subir.</p>
            <div className="grade-campos">
              <label className="campo">
                <span>Quanto tem agora</span>
                <input type="number" inputMode="numeric" min={0} max={9999} step={1} value={quantidade} onChange={(e) => setQuantidade(e.target.value)} />
              </label>
              <label className="campo">
                <span>Prioridade</span>
                <select value={prioridade} onChange={(e) => setPrioridade(e.target.value as PrioridadeAviso)}>
                  {permitidas.map((p) => (
                    <option key={p.valor} value={p.valor}>
                      {p.nome} — {p.quando}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
            {atualizar.isError && <p className="alerta alerta-erro">{mensagemDoErro(atualizar.error)}</p>}
            {atualizar.isSuccess && <p className="alerta alerta-sucesso">Aviso atualizado.</p>}
            <div className="rodape-formulario">
              <button type="submit" className="botao botao-secundario" disabled={ocupado}>
                {atualizar.isPending ? 'Salvando…' : 'Atualizar'}
              </button>
            </div>
          </form>

          <form
            className="secao formulario"
            onSubmit={(e) => {
              e.preventDefault()
              fechar.mutate()
            }}
          >
            <h2>Fechar aviso</h2>
            <p className="suave">
              O aviso fecha sozinho quando a entrega com este item for conferida. Feche à mão se resolveu de outro jeito.
            </p>
            <div className="chips" role="group" aria-label="Motivos comuns">
              {MOTIVOS.map((m) => (
                <button key={m} type="button" className={motivo === m ? 'aba aba-ativa' : 'aba'} onClick={() => setMotivo(m)}>
                  {m}
                </button>
              ))}
            </div>
            <label className="campo">
              <span>Motivo</span>
              <input required minLength={3} maxLength={200} value={motivo} onChange={(e) => setMotivo(e.target.value)} />
            </label>
            {fechar.isError && <p className="alerta alerta-erro">{mensagemDoErro(fechar.error)}</p>}
            <div className="rodape-formulario">
              <button type="submit" className="botao" disabled={ocupado}>
                {fechar.isPending ? 'Fechando…' : 'Fechar aviso'}
              </button>
            </div>
          </form>
        </>
      )}
    </>
  )
}
