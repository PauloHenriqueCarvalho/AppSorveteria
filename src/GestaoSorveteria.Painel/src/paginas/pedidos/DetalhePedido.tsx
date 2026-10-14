import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import {
  alterarItensPedido,
  cancelarPedido,
  CHAVE_PEDIDOS,
  enviarPedido,
  montarPedido,
  obterMensagensPedido,
  obterPedido,
} from '../../api/pedidos.ts'
import type { LinhaPedidoDto, PedidoDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Indicador, Secao, Selo } from '../../componentes/Ui.tsx'
import { embalagemNoPlural, lerQuantidade, quando, quantidadeParaCampo, respeitaPasso } from '../../estoque.ts'
import { formatarDia, formatarMoeda } from '../../formatacao.ts'
import {
  descreverContado,
  descreverEmbalagens,
  formatarPercentual,
  ORIGEM_LINHA,
  SITUACAO_RECEBIMENTO,
  STATUS_PEDIDO,
} from '../../pedidos.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

/** /pedidos/:id — rascunho com +/− (RN-PF-04/05), envio (RN-PF-06), pedido enviado/recebido/cancelado e WhatsApp. */
export default function DetalhePedido() {
  const { id = '' } = useParams()
  const pedido = useQuery({ queryKey: [...CHAVE_PEDIDOS, id], queryFn: () => obterPedido(id) })
  return (
    <>
      <p className="voltar">
        <Link to="/pedidos">← Pedidos</Link>
      </p>
      <Consulta consulta={pedido}>
        {(p) => (p.status === 'Rascunho' ? <Rascunho key={`${p.id}-${p.criadoEm}`} pedido={p} /> : <PedidoFechado pedido={p} />)}
      </Consulta>
    </>
  )
}

/** Atualiza a tela e a lista com o pedido que a API devolveu. */
function useGuardarPedido() {
  const queryClient = useQueryClient()
  return async (pedido: PedidoDto) => {
    queryClient.setQueryData([...CHAVE_PEDIDOS, pedido.id], pedido)
    await queryClient.invalidateQueries({ queryKey: CHAVE_PEDIDOS, exact: true })
  }
}

function Cabecalho({ pedido }: { pedido: PedidoDto }) {
  const status = STATUS_PEDIDO[pedido.status]
  const subtitulo =
    pedido.status === 'Rascunho'
      ? `Montado por ${pedido.criadoPorNome} ${quando(pedido.criadoEm)} · se enviar hoje, chega ${formatarDia(pedido.entregaPrevista).slice(0, 5)}`
      : pedido.status === 'Enviado'
        ? `Enviado ${quando(pedido.enviadoEm!)} · entrega prevista ${formatarDia(pedido.entregaPrevista).slice(0, 5)}`
        : pedido.status === 'Recebido'
          ? `Conferido por ${pedido.recebidoPorNome ?? '—'} ${quando(pedido.recebidoEm!)}`
          : `Cancelado por ${pedido.canceladoPorNome ?? '—'} ${quando(pedido.canceladoEm!)}: ${pedido.motivoCancelamento ?? ''}`
  return (
    <CabecalhoPagina
      titulo={`Pedido nº ${pedido.numero} — ${pedido.fornecedorNome}`}
      subtitulo={subtitulo}
      acoes={
        <>
          <Selo tom={status.tom}>{status.nome}</Selo>
          {pedido.status !== 'Cancelado' && (
            <Link to={`/pedidos/${pedido.id}/imprimir`} className="botao botao-secundario">
              Imprimir
            </Link>
          )}
          {pedido.status === 'Enviado' && (
            <Link to={`/pedidos/${pedido.id}/conferir`} className="botao">
              Conferir entrega
            </Link>
          )}
        </>
      }
    />
  )
}

// ---------------------------------------------------------------- rascunho

function Rascunho({ pedido }: { pedido: PedidoDto }) {
  const guardar = useGuardarPedido()
  const [valores, setValores] = useState<Record<string, string>>(() => camposDe(pedido))
  const [soComQuantidade, setSoComQuantidade] = useState(false)
  const [confirmarEnvio, setConfirmarEnvio] = useState(false)
  const [erro, setErro] = useState<string | null>(null)

  const alteradas = pedido.linhas.filter((l) => lerQuantidade(valores[l.itemEstoqueId] ?? '') !== l.pedidoEmbalagens)

  const salvar = useMutation({
    mutationFn: (linhas: { itemEstoqueId: string; embalagens: number }[]) => alterarItensPedido(pedido.id, linhas),
    onSuccess: async (salvo) => {
      setValores(camposDe(salvo))
      await guardar(salvo)
    },
  })

  const remontar = useMutation({
    mutationFn: () => montarPedido(pedido.fornecedorId),
    onSuccess: async (salvo) => {
      setValores(camposDe(salvo))
      await guardar(salvo)
    },
  })

  const enviar = useMutation({
    mutationFn: async () => {
      const linhas = linhasParaSalvar()
      if (linhas === null) {
        throw new Error(erro ?? 'Confira as quantidades.')
      }
      if (linhas.length > 0) {
        await alterarItensPedido(pedido.id, linhas)
      }
      return enviarPedido(pedido.id)
    },
    onSuccess: async (resposta) => {
      await guardar(resposta.pedido)
    },
  })

  /** Linhas mudadas, já conferidas (número e passo). Null se alguma estiver errada (a mensagem vai para `erro`). */
  function linhasParaSalvar(): { itemEstoqueId: string; embalagens: number }[] | null {
    setErro(null)
    const linhas: { itemEstoqueId: string; embalagens: number }[] = []
    for (const linha of pedido.linhas) {
      const valor = lerQuantidade(valores[linha.itemEstoqueId] ?? '')
      if (valor === null || !respeitaPasso(valor, linha.passo)) {
        setErro(`${linha.grupoNome} ${linha.itemNome}: ${linha.passo < 1 ? 'use caixas inteiras ou meia (ex.: 2,5)' : 'use número inteiro'}.`)
        return null
      }
      if (valor !== linha.pedidoEmbalagens) {
        linhas.push({ itemEstoqueId: linha.itemEstoqueId, embalagens: valor })
      }
    }
    return linhas
  }

  function salvarAlteracoes() {
    const linhas = linhasParaSalvar()
    if (linhas && linhas.length > 0) {
      salvar.mutate(linhas)
    }
  }

  function definir(linha: LinhaPedidoDto, valor: string) {
    setValores((atuais) => ({ ...atuais, [linha.itemEstoqueId]: valor }))
  }

  function somar(linha: LinhaPedidoDto, delta: number) {
    const atual = lerQuantidade(valores[linha.itemEstoqueId] ?? '') ?? 0
    definir(linha, quantidadeParaCampo(Math.max(0, atual + delta)))
  }

  const ocupado = salvar.isPending || remontar.isPending || enviar.isPending
  const linhasVisiveis = soComQuantidade
    ? pedido.linhas.filter((l) => l.pedidoEmbalagens > 0 || (lerQuantidade(valores[l.itemEstoqueId] ?? '') ?? 0) > 0)
    : pedido.linhas
  const comQuantidade = pedido.linhas.filter((l) => l.pedidoEmbalagens > 0).length

  return (
    <>
      <Cabecalho pedido={pedido} />

      <p className="alerta alerta-aviso">
        A sugestão vem da última contagem, do que chegou depois dela, dos avisos e do que faltou na entrega anterior. Ajuste com − e
        +; o que você mudar fica, mesmo se remontar. Enviado, o pedido não muda mais.
      </p>

      <div className="grade-indicadores grade-indicadores-3">
        <Indicador rotulo="Total estimado" valor={formatarMoeda(pedido.totalPedido)} detalhe={alteradas.length > 0 ? 'Salve para atualizar' : 'Custo do catálogo'} tom="marca" />
        <Indicador rotulo="Itens no pedido" valor={comQuantidade} detalhe={`de ${pedido.linhas.length} na lista`} />
        <Indicador rotulo="Entrega prevista" valor={formatarDia(pedido.entregaPrevista).slice(0, 5)} detalhe="Se enviar hoje" />
      </div>

      <section className="secao secao-tabela">
        <div className="secao-cabecalho">
          <h2>Itens</h2>
          <div className="chips" role="group" aria-label="Mostrar">
            <button type="button" className={soComQuantidade ? 'aba' : 'aba aba-ativa'} onClick={() => setSoComQuantidade(false)}>
              Todos
            </button>
            <button type="button" className={soComQuantidade ? 'aba aba-ativa' : 'aba'} onClick={() => setSoComQuantidade(true)}>
              Só o que vai
            </button>
          </div>
        </div>
        <div className="tabela-caixa">
          <table className="tabela tabela-contagem tabela-pedido">
            <thead>
              <tr>
                <th scope="col">Item</th>
                <th scope="col" className="numero so-largo">
                  Contado
                </th>
                <th scope="col" className="numero so-largo">
                  Sugerido
                </th>
                <th scope="col" className="numero">
                  Pedido
                </th>
                <th scope="col" className="numero so-largo">
                  Subtotal
                </th>
              </tr>
            </thead>
            {gruposDe(linhasVisiveis).map(([grupoId, grupoNome, linhas]) => (
              <tbody key={grupoId}>
                <tr className="linha-grupo">
                  <th scope="rowgroup" colSpan={5}>
                    <span className="linha-grupo-conteudo">
                      <strong>{grupoNome}</strong>
                    </span>
                  </th>
                </tr>
                {linhas.map((l) => {
                  const valor = valores[l.itemEstoqueId] ?? ''
                  const mudou = lerQuantidade(valor) !== l.pedidoEmbalagens
                  const marca = ORIGEM_LINHA[l.origem]
                  return (
                    <tr key={l.itemEstoqueId} className={(lerQuantidade(valor) ?? 0) === 0 ? 'linha-apagada' : undefined}>
                      <td>
                        <strong>{l.itemNome}</strong> {marca && <Selo tom={marca.tom}>{marca.nome}</Selo>}
                        {l.alteradoPelaDona && l.origem !== 'AdicionadoPelaDona' && <span className="suave pequeno"> · ajustado</span>}
                        {l.observacao && <span className="suave bloco pequeno">{l.observacao}</span>}
                        <span className="suave bloco pequeno so-estreito">
                          Sugerido {descreverEmbalagens(l.sugeridoEmbalagens, l.unidade)} · contado{' '}
                          {descreverContado(l.contadoUnidades, l.unidadesPorEmbalagem, l.unidade)}
                        </span>
                      </td>
                      <td className="numero so-largo suave">{descreverContado(l.contadoUnidades, l.unidadesPorEmbalagem, l.unidade)}</td>
                      <td className="numero so-largo suave">{descreverEmbalagens(l.sugeridoEmbalagens, l.unidade)}</td>
                      <td className="numero">
                        <span className="contador-campo">
                          <button
                            type="button"
                            className="botao botao-secundario"
                            aria-label={`Menos ${l.itemNome}`}
                            disabled={ocupado}
                            onClick={() => somar(l, -l.passo)}
                          >
                            −
                          </button>
                          <input
                            inputMode="decimal"
                            className={mudou ? 'campo-mudou' : undefined}
                            aria-label={`${l.itemNome} em ${embalagemNoPlural(l.unidade)}`}
                            value={valor}
                            disabled={ocupado}
                            onChange={(e) => definir(l, e.target.value)}
                          />
                          <button
                            type="button"
                            className="botao botao-secundario"
                            aria-label={`Mais ${l.itemNome}`}
                            disabled={ocupado}
                            onClick={() => somar(l, l.passo)}
                          >
                            +
                          </button>
                        </span>
                      </td>
                      <td className="numero so-largo">
                        {mudou ? <span className="suave">salve</span> : l.pedidoEmbalagens > 0 ? formatarMoeda(l.subtotal) : <span className="suave">—</span>}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            ))}
          </table>
        </div>
      </section>

      <AdicionarItem pedido={pedido} ocupado={ocupado} aoAdicionar={(salvo) => setValores((atuais) => ({ ...camposDe(salvo), ...atuais, ...novosDe(salvo, atuais) }))} />

      <section className="secao rodape-acao rodape-fixo">
        {erro && <p className="alerta alerta-erro">{erro}</p>}
        {[salvar, remontar, enviar].map((m, i) => m.isError && <p key={i} className="alerta alerta-erro">{mensagemDoErro(m.error)}</p>)}
        {confirmarEnvio ? (
          <>
            <p>
              <strong>Enviar o pedido nº {pedido.numero}?</strong>{' '}
              <span className="suave">
                Depois de enviado ele não muda mais (para corrigir, cancele e monte outro).{' '}
                {pedido.envio !== 'Impresso' ? 'Em seguida abre a mensagem do WhatsApp.' : 'Em seguida, imprima a folha.'}
              </span>
            </p>
            <span className="pagina-acoes">
              <button type="button" className="botao botao-secundario" disabled={ocupado} onClick={() => setConfirmarEnvio(false)}>
                Voltar
              </button>
              <button type="button" className="botao" disabled={ocupado} onClick={() => enviar.mutate()}>
                {enviar.isPending ? 'Enviando…' : 'Confirmar envio'}
              </button>
            </span>
          </>
        ) : (
          <>
            <p className="suave">
              {alteradas.length > 0
                ? `${alteradas.length} ${alteradas.length === 1 ? 'alteração não salva' : 'alterações não salvas'}.`
                : `Tudo salvo · ${comQuantidade} ${comQuantidade === 1 ? 'item' : 'itens'} · ${formatarMoeda(pedido.totalPedido)}`}
            </p>
            <span className="pagina-acoes">
              <button type="button" className="botao botao-secundario" disabled={ocupado || alteradas.length === 0} onClick={salvarAlteracoes}>
                {salvar.isPending ? 'Salvando…' : 'Salvar alterações'}
              </button>
              <button
                type="button"
                className="botao"
                disabled={ocupado || (comQuantidade === 0 && alteradas.length === 0)}
                onClick={() => linhasParaSalvar() !== null && setConfirmarEnvio(true)}
              >
                Enviar pedido
              </button>
            </span>
          </>
        )}
      </section>

      <p className="links-rodape">
        <button type="button" className="botao-link" disabled={ocupado || alteradas.length > 0} onClick={() => remontar.mutate()}>
          {remontar.isPending ? 'Refazendo…' : 'Refazer a sugestão'}
        </button>
        <span className="suave pequeno">
          {alteradas.length > 0 ? 'Salve antes de refazer.' : 'Recalcula com a contagem e os avisos de agora; o que você mudou fica.'}
        </span>
      </p>
      <Cancelar pedido={pedido} />
    </>
  )
}

function camposDe(pedido: PedidoDto): Record<string, string> {
  return Object.fromEntries(pedido.linhas.map((l) => [l.itemEstoqueId, quantidadeParaCampo(l.pedidoEmbalagens)]))
}

/** Linhas que acabaram de entrar no pedido (não estavam nos campos). */
function novosDe(pedido: PedidoDto, atuais: Record<string, string>): Record<string, string> {
  return Object.fromEntries(
    pedido.linhas.filter((l) => !(l.itemEstoqueId in atuais)).map((l) => [l.itemEstoqueId, quantidadeParaCampo(l.pedidoEmbalagens)]),
  )
}

/** Linhas agrupadas na ordem em que a API mandou (ordem da planilha). */
function gruposDe(linhas: LinhaPedidoDto[]): [string, string, LinhaPedidoDto[]][] {
  const grupos = new Map<string, [string, string, LinhaPedidoDto[]]>()
  for (const linha of linhas) {
    const grupo = grupos.get(linha.grupoId) ?? [linha.grupoId, linha.grupoNome, []]
    grupo[2].push(linha)
    grupos.set(linha.grupoId, grupo)
  }
  return [...grupos.values()]
}

/** Acrescenta um item do catálogo deste fornecedor que a sugestão não trouxe (ex.: insumo sem aviso). */
function AdicionarItem({ pedido, ocupado, aoAdicionar }: { pedido: PedidoDto; ocupado: boolean; aoAdicionar: (salvo: PedidoDto) => void }) {
  const catalogo = useCatalogoEstoque()
  const guardar = useGuardarPedido()
  const [itemId, setItemId] = useState('')
  const [quantidade, setQuantidade] = useState('1')
  const [erro, setErro] = useState<string | null>(null)
  const adicionar = useMutation({
    mutationFn: (dados: { itemEstoqueId: string; embalagens: number }) => alterarItensPedido(pedido.id, [dados]),
    onSuccess: async (salvo) => {
      setItemId('')
      setQuantidade('1')
      aoAdicionar(salvo)
      await guardar(salvo)
    },
  })

  const noPedido = new Set(pedido.linhas.map((l) => l.itemEstoqueId))
  const opcoes = (catalogo.data?.itens ?? [])
    .filter((i) => i.ativo && i.fornecedorId === pedido.fornecedorId && !noPedido.has(i.id))
    .sort((a, b) => a.grupoNome.localeCompare(b.grupoNome) || a.ordem - b.ordem || a.nome.localeCompare(b.nome))
  if (opcoes.length === 0) {
    return null
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErro(null)
    const item = opcoes.find((i) => i.id === itemId)
    const valor = lerQuantidade(quantidade)
    if (!item) {
      setErro('Escolha o item.')
      return
    }
    const passo = item.unidade === 'Caixa' && pedido.aceitaMeiaCaixa ? 0.5 : 1
    if (valor === null || valor <= 0 || !respeitaPasso(valor, passo)) {
      setErro(passo < 1 ? 'Use caixas inteiras ou meia (ex.: 2,5).' : 'Use número inteiro maior que zero.')
      return
    }
    adicionar.mutate({ itemEstoqueId: item.id, embalagens: valor })
  }

  return (
    <form className="secao formulario" onSubmit={enviar}>
      <h2>Acrescentar item</h2>
      <p className="suave">Itens do catálogo deste fornecedor que não estão na lista (copos, casquinhas e insumos só entram com aviso).</p>
      <div className="grade-campos">
        <label className="campo">
          <span>Item</span>
          <select value={itemId} onChange={(e) => setItemId(e.target.value)} disabled={ocupado || adicionar.isPending}>
            <option value="">Escolha…</option>
            {opcoes.map((i) => (
              <option key={i.id} value={i.id}>
                {i.grupoNome} — {i.nome}
              </option>
            ))}
          </select>
        </label>
        <label className="campo">
          <span>Quantidade (embalagens)</span>
          <input inputMode="decimal" value={quantidade} onChange={(e) => setQuantidade(e.target.value)} disabled={ocupado || adicionar.isPending} />
        </label>
      </div>
      {erro && <p className="alerta alerta-erro">{erro}</p>}
      {adicionar.isError && <p className="alerta alerta-erro">{mensagemDoErro(adicionar.error)}</p>}
      <div className="rodape-formulario">
        <button type="submit" className="botao botao-secundario" disabled={ocupado || adicionar.isPending}>
          {adicionar.isPending ? 'Acrescentando…' : 'Acrescentar'}
        </button>
      </div>
    </form>
  )
}

const MOTIVOS_CANCELAMENTO = ['Fornecedor não vai entregar', 'Montei errado', 'Pedido feito por telefone']

/** RN-PF-08: rascunho ou enviado, com motivo. Nada é apagado. */
function Cancelar({ pedido }: { pedido: PedidoDto }) {
  const guardar = useGuardarPedido()
  const [aberto, setAberto] = useState(false)
  const [motivo, setMotivo] = useState('')
  const cancelar = useMutation({
    mutationFn: () => cancelarPedido(pedido.id, motivo.trim()),
    onSuccess: guardar,
  })

  if (!aberto) {
    return (
      <p className="suave pequeno">
        <button type="button" className="botao-link" onClick={() => setAberto(true)}>
          Cancelar este pedido…
        </button>
      </p>
    )
  }

  return (
    <form
      className="secao formulario"
      onSubmit={(e) => {
        e.preventDefault()
        cancelar.mutate()
      }}
    >
      <h2>Cancelar pedido nº {pedido.numero}</h2>
      <p className="suave">
        {pedido.status === 'Enviado'
          ? 'Avise o fornecedor que o pedido foi cancelado. O pedido continua na lista, como cancelado.'
          : 'O rascunho continua na lista, como cancelado. Você pode montar outro depois.'}
      </p>
      <div className="chips" role="group" aria-label="Motivos comuns">
        {MOTIVOS_CANCELAMENTO.map((m) => (
          <button key={m} type="button" className={motivo === m ? 'aba aba-ativa' : 'aba'} onClick={() => setMotivo(m)}>
            {m}
          </button>
        ))}
      </div>
      <label className="campo">
        <span>Motivo</span>
        <input required minLength={3} maxLength={200} value={motivo} onChange={(e) => setMotivo(e.target.value)} />
      </label>
      {cancelar.isError && <p className="alerta alerta-erro">{mensagemDoErro(cancelar.error)}</p>}
      <div className="rodape-formulario">
        <button type="button" className="botao botao-secundario" onClick={() => setAberto(false)} disabled={cancelar.isPending}>
          Voltar
        </button>
        <button type="submit" className="botao botao-perigo" disabled={cancelar.isPending}>
          {cancelar.isPending ? 'Cancelando…' : 'Cancelar pedido'}
        </button>
      </div>
    </form>
  )
}

// ---------------------------------------------------------------- enviado, recebido, cancelado

function PedidoFechado({ pedido }: { pedido: PedidoDto }) {
  const recebido = pedido.status === 'Recebido'
  const linhas = pedido.linhas.filter((l) => l.pedidoEmbalagens > 0 || (l.recebidoEmbalagens ?? 0) > 0)
  const colunas = recebido ? 6 : 4

  return (
    <>
      <Cabecalho pedido={pedido} />

      {recebido ? (
        <div className="grade-indicadores grade-indicadores-3">
          <Indicador rotulo="Pedido" valor={formatarMoeda(pedido.totalPedido)} detalhe={`${linhas.filter((l) => l.pedidoEmbalagens > 0).length} itens`} />
          <Indicador rotulo="Veio" valor={formatarMoeda(pedido.totalRecebido ?? 0)} detalhe="Pelo custo do pedido" tom="marca" />
          <Indicador
            rotulo="Atendido"
            valor={formatarPercentual(pedido.percentualAtendido)}
            detalhe="Do que foi pedido"
            tom={(pedido.percentualAtendido ?? 0) >= 100 ? 'bom' : 'atencao'}
          />
        </div>
      ) : (
        pedido.status === 'Enviado' && (
          <div className="grade-indicadores grade-indicadores-3">
            <Indicador rotulo="Total estimado" valor={formatarMoeda(pedido.totalPedido)} tom="marca" />
            <Indicador rotulo="Itens" valor={linhas.length} />
            <Indicador rotulo="Entrega prevista" valor={formatarDia(pedido.entregaPrevista).slice(0, 5)} />
          </div>
        )
      )}

      {pedido.status !== 'Cancelado' && <MensagensWhatsApp pedido={pedido} />}

      <section className="secao secao-tabela">
        <div className="tabela-caixa">
          <table className="tabela tabela-pedido">
            <thead>
              <tr>
                <th scope="col">Item</th>
                <th scope="col" className="numero">
                  Pedido
                </th>
                {recebido && (
                  <>
                    <th scope="col" className="numero">
                      Veio
                    </th>
                    <th scope="col" className="so-largo">
                      Situação
                    </th>
                  </>
                )}
                <th scope="col" className="numero so-largo">
                  Custo
                </th>
                <th scope="col" className="numero">
                  {recebido ? 'Diferença' : 'Subtotal'}
                </th>
              </tr>
            </thead>
            {gruposDe(linhas).map(([grupoId, grupoNome, doGrupo]) => (
              <tbody key={grupoId}>
                <tr className="linha-grupo">
                  <th scope="rowgroup" colSpan={colunas}>
                    <span className="linha-grupo-conteudo">
                      <strong>{grupoNome}</strong>
                    </span>
                  </th>
                </tr>
                {doGrupo.map((l) => {
                  const situacao = l.situacao ? SITUACAO_RECEBIMENTO[l.situacao] : null
                  return (
                    <tr key={l.itemEstoqueId}>
                      <td>
                        <strong>{l.itemNome}</strong>
                        {situacao && l.situacao !== 'Certo' && (
                          <span className="so-estreito">
                            {' '}
                            <Selo tom={situacao.tom}>{situacao.nome}</Selo>
                          </span>
                        )}
                        {l.observacao && <span className="suave bloco pequeno">{l.observacao}</span>}
                        {l.situacao === 'Faltou' && !l.pedirDeNovo && <span className="suave bloco pequeno">Não volta no próximo pedido</span>}
                      </td>
                      <td className="numero">{descreverEmbalagens(l.pedidoEmbalagens, l.unidade)}</td>
                      {recebido && (
                        <>
                          <td className="numero forte">{descreverEmbalagens(l.recebidoEmbalagens ?? 0, l.unidade)}</td>
                          <td className="so-largo">{situacao && <Selo tom={situacao.tom}>{situacao.nome}</Selo>}</td>
                        </>
                      )}
                      <td className="numero so-largo suave">{formatarMoeda(l.custoEmbalagem)}</td>
                      <td className="numero">
                        {recebido ? (
                          l.diferencaValor ? (
                            <span className={l.diferencaValor < 0 ? 'valor-negativo' : undefined}>{formatarMoeda(l.diferencaValor)}</span>
                          ) : (
                            <span className="suave">—</span>
                          )
                        ) : (
                          formatarMoeda(l.subtotal)
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            ))}
          </table>
        </div>
      </section>

      {pedido.status === 'Enviado' && <Cancelar pedido={pedido} />}
    </>
  )
}

/** WhatsApp do pedido (enviado) ou do que veio diferente (recebido), para abrir ou copiar de novo. */
function MensagensWhatsApp({ pedido }: { pedido: PedidoDto }) {
  const mensagens = useQuery({
    queryKey: [...CHAVE_PEDIDOS, pedido.id, 'mensagens', pedido.status],
    queryFn: () => obterMensagensPedido(pedido.id),
  })
  const [copiado, setCopiado] = useState(false)

  async function copiar(texto: string) {
    try {
      await navigator.clipboard.writeText(texto)
      setCopiado(true)
      window.setTimeout(() => setCopiado(false), 2500)
    } catch {
      setCopiado(false)
    }
  }

  const dados = mensagens.data
  if (!dados) {
    return null
  }
  const recebido = pedido.status === 'Recebido'
  const texto = recebido ? dados.divergencias : pedido.envio === 'Impresso' ? null : dados.pedido
  const link = recebido ? dados.linkDivergencias : dados.linkPedido
  if (!texto || !link) {
    return null
  }

  return (
    <Secao titulo={recebido ? 'Avisar o fornecedor do que veio diferente' : 'Mensagem do pedido'}>
      <pre className="mensagem-whatsapp">{texto}</pre>
      <div className="acoes acoes-inicio">
        <a className="botao" href={link} target="_blank" rel="noreferrer">
          Abrir no WhatsApp
        </a>
        <button type="button" className="botao botao-secundario" onClick={() => void copiar(texto)}>
          {copiado ? 'Copiado' : 'Copiar texto'}
        </button>
      </div>
    </Secao>
  )
}
