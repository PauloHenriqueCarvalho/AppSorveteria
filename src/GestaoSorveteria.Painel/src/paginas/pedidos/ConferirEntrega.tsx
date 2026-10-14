import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { CHAVE_AVISOS } from '../../api/avisos.ts'
import { CHAVE_CATALOGO_ESTOQUE } from '../../api/estoque.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import { CHAVE_PEDIDOS, obterPedido, receberPedido } from '../../api/pedidos.ts'
import type { LinhaPedidoDto, LinhaRecebidaRequest, NovoCustoRequest, PedidoDto, ReceberPedidoResponse } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Secao, Selo } from '../../componentes/Ui.tsx'
import { lerPreco } from '../../entrada.ts'
import { embalagemNoPlural, lerQuantidade, quantidadeParaCampo, respeitaPasso } from '../../estoque.ts'
import { formatarDia, formatarMoeda } from '../../formatacao.ts'
import { descreverEmbalagens, passoDeRecebimento, SITUACAO_RECEBIMENTO, situacaoNaConferencia } from '../../pedidos.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

/**
 * /pedidos/:id/conferir — conferência da entrega (RN-RC-01…05). Parte de "veio = pedido": "Chegou tudo certo" confirma de
 * uma vez; senão a dona corrige só as linhas diferentes, marca o sabor que veio trocado e o preço novo da nota.
 */
export default function ConferirEntrega() {
  const { id = '' } = useParams()
  const pedido = useQuery({ queryKey: [...CHAVE_PEDIDOS, id], queryFn: () => obterPedido(id) })
  const [resultado, setResultado] = useState<ReceberPedidoResponse | null>(null)

  return (
    <>
      <p className="voltar">
        <Link to={`/pedidos/${id}`}>← Pedido</Link>
      </p>
      {resultado ? (
        <Resultado resultado={resultado} />
      ) : (
        <Consulta consulta={pedido}>
          {(p) =>
            p.status === 'Enviado' ? (
              <Conferencia key={p.id} pedido={p} aoConferir={setResultado} />
            ) : (
              <section className="secao vazio">
                <h2>Este pedido não está aguardando entrega</h2>
                <p className="suave">
                  {p.status === 'Rascunho' ? 'Envie o pedido antes de conferir a entrega.' : `O pedido nº ${p.numero} está ${p.status.toLowerCase()}.`}
                </p>
                <div className="acoes">
                  <Link to={`/pedidos/${p.id}`} className="botao">
                    Ver o pedido
                  </Link>
                </div>
              </section>
            )
          }
        </Consulta>
      )}
    </>
  )
}

interface Campo {
  veio: string
  pedirDeNovo: boolean
  preco: string
}

function Conferencia({ pedido, aoConferir }: { pedido: PedidoDto; aoConferir: (r: ReceberPedidoResponse) => void }) {
  const queryClient = useQueryClient()
  const catalogo = useCatalogoEstoque()
  const [campos, setCampos] = useState<Record<string, Campo>>(() =>
    Object.fromEntries(pedido.linhas.map((l) => [l.itemEstoqueId, { veio: quantidadeParaCampo(l.pedidoEmbalagens), pedirDeNovo: true, preco: '' }])),
  )
  /** Sabores que vieram sem estar no pedido (ou com 0 no pedido): id → campo. */
  const [extras, setExtras] = useState<Record<string, Campo>>({})
  const [extraId, setExtraId] = useState('')
  const [confirmar, setConfirmar] = useState(false)
  const [erro, setErro] = useState<string | null>(null)
  /** Preço novo fica escondido até a dona dizer que a nota veio com preço diferente. */
  const [precoMudou, setPrecoMudou] = useState(false)

  const receber = useMutation({
    mutationFn: (dados: { diferencas: LinhaRecebidaRequest[]; naoPedidos: LinhaRecebidaRequest[]; novosCustos: NovoCustoRequest[] }) =>
      receberPedido(pedido.id, dados),
    onSuccess: (resposta) => {
      // Mostra o resultado primeiro; com o cache já "Recebido", esta tela diria "não está aguardando entrega".
      aoConferir(resposta)
      queryClient.setQueryData([...CHAVE_PEDIDOS, pedido.id], resposta.pedido)
      void Promise.all([
        queryClient.invalidateQueries({ queryKey: CHAVE_PEDIDOS }),
        queryClient.invalidateQueries({ queryKey: CHAVE_AVISOS }),
        queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE }),
      ])
    },
  })

  const pedidas = pedido.linhas.filter((l) => l.pedidoEmbalagens > 0)
  const catalogoDoFornecedor = (catalogo.data?.itens ?? []).filter((i) => i.fornecedorId === pedido.fornecedorId)
  const opcoesExtra = catalogoDoFornecedor
    .filter((i) => i.ativo && !pedidas.some((l) => l.itemEstoqueId === i.id) && !(i.id in extras))
    .sort((a, b) => a.grupoNome.localeCompare(b.grupoNome) || a.ordem - b.ordem || a.nome.localeCompare(b.nome))

  function alterar(itemId: string, mudanca: Partial<Campo>, extra = false) {
    const atualizar = (atuais: Record<string, Campo>) => ({ ...atuais, [itemId]: { ...atuais[itemId], ...mudanca } })
    if (extra) {
      setExtras(atualizar)
    } else {
      setCampos(atualizar)
    }
    setConfirmar(false)
  }

  function somar(itemId: string, passo: number, delta: number, extra = false) {
    const atual = lerQuantidade((extra ? extras : campos)[itemId]?.veio ?? '') ?? 0
    alterar(itemId, { veio: quantidadeParaCampo(Math.max(0, atual + delta * passo)) }, extra)
  }

  /** Monta o pedido da API; null (com `erro`) se algum campo estiver errado. */
  function montar() {
    setErro(null)
    const diferencas: LinhaRecebidaRequest[] = []
    const naoPedidos: LinhaRecebidaRequest[] = []
    const novosCustos: NovoCustoRequest[] = []
    const passos = new Map(pedido.linhas.map((l) => [l.itemEstoqueId, passoDeRecebimento(l.unidade)]))

    const ler = (nome: string, campo: Campo, passo: number) => {
      const veio = lerQuantidade(campo.veio)
      if (veio === null || !respeitaPasso(veio, passo)) {
        throw new Error(`${nome}: ${passo < 1 ? 'use caixas inteiras ou meia (ex.: 2,5)' : 'use número inteiro'}.`)
      }
      const texto = precoMudou ? campo.preco.trim() : ''
      const preco = texto === '' ? null : lerPreco(texto)
      if (texto !== '' && (preco === null || preco <= 0)) {
        throw new Error(`${nome}: preço novo inválido (ex.: 26,40).`)
      }
      return { veio, preco }
    }

    try {
      for (const linha of pedidas) {
        const { veio, preco } = ler(linha.itemNome, campos[linha.itemEstoqueId], passos.get(linha.itemEstoqueId) ?? 1)
        if (veio !== linha.pedidoEmbalagens) {
          // "Pedir de novo" só vale quando faltou; desmarcado e depois corrigido para o certo não conta.
          const pedirDeNovo = veio < linha.pedidoEmbalagens ? campos[linha.itemEstoqueId].pedirDeNovo : true
          diferencas.push({ itemEstoqueId: linha.itemEstoqueId, recebidoEmbalagens: veio, pedirDeNovo })
        }
        if (preco !== null) {
          if (veio === 0) {
            throw new Error(`${linha.itemNome}: não veio, então não tem preço novo.`)
          }
          novosCustos.push({ itemEstoqueId: linha.itemEstoqueId, custoEmbalagem: preco })
        }
      }
      for (const [itemId, campo] of Object.entries(extras)) {
        const item = catalogoDoFornecedor.find((i) => i.id === itemId)
        const nome = item?.nome ?? 'Item'
        const { veio, preco } = ler(nome, campo, passoDeRecebimento(item?.unidade ?? 'Unidade'))
        if (veio === 0) {
          continue
        }
        // Item que está no pedido com 0 é corrigido na própria linha; o resto entra como "não pedido".
        const linhaComZero = pedido.linhas.some((l) => l.itemEstoqueId === itemId)
        ;(linhaComZero ? diferencas : naoPedidos).push({ itemEstoqueId: itemId, recebidoEmbalagens: veio })
        if (preco !== null) {
          novosCustos.push({ itemEstoqueId: itemId, custoEmbalagem: preco })
        }
      }
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Confira os campos.')
      return null
    }
    return { diferencas, naoPedidos, novosCustos }
  }

  const quantasDiferencas =
    pedidas.filter((l) => lerQuantidade(campos[l.itemEstoqueId]?.veio ?? '') !== l.pedidoEmbalagens).length +
    Object.values(extras).filter((c) => (lerQuantidade(c.veio) ?? 0) > 0).length

  function pedirConfirmacao() {
    if (montar() !== null) {
      setConfirmar(true)
    }
  }

  function enviar() {
    const dados = montar()
    if (dados) {
      receber.mutate(dados)
    }
  }

  return (
    <>
      <CabecalhoPagina
        titulo={`Conferir a entrega — pedido nº ${pedido.numero}`}
        subtitulo={`${pedido.fornecedorNome} · entrega prevista ${formatarDia(pedido.entregaPrevista).slice(0, 5)}`}
        acoes={
          <Link to={`/pedidos/${pedido.id}/imprimir`} className="botao botao-secundario">
            Imprimir a folha
          </Link>
        }
      />

      <p className="alerta alerta-aviso">
        Já vem preenchido com o que foi pedido. Se chegou tudo, é só confirmar. Se não, mude só o que veio diferente (caixa vai de meia em
        meia).
      </p>

      <label className="campo-marcar">
        <input type="checkbox" checked={precoMudou} onChange={(e) => setPrecoMudou(e.target.checked)} disabled={receber.isPending} />
        A nota veio com preço diferente (o preço novo vale para os próximos pedidos)
      </label>

      <section className="secao secao-tabela">
        <div className="tabela-caixa">
          <table className="tabela tabela-contagem tabela-pedido">
            <thead>
              <tr>
                <th scope="col">Item</th>
                <th scope="col" className="numero so-largo">
                  Pedido
                </th>
                <th scope="col" className="numero">
                  Veio
                </th>
                {precoMudou && (
                  <th scope="col" className="so-largo">
                    Preço novo
                  </th>
                )}
              </tr>
            </thead>
            <tbody>
              {pedidas.map((l) => (
                <LinhaConferencia
                  key={l.itemEstoqueId}
                  nome={l.itemNome}
                  grupo={l.grupoNome}
                  linha={l}
                  campo={campos[l.itemEstoqueId]}
                  comPreco={precoMudou}
                  ocupado={receber.isPending}
                  aoMudar={(m) => alterar(l.itemEstoqueId, m)}
                  aoSomar={(delta) => somar(l.itemEstoqueId, passoDeRecebimento(l.unidade), delta)}
                />
              ))}
              {Object.entries(extras).map(([itemId, campo]) => {
                const item = catalogoDoFornecedor.find((i) => i.id === itemId)
                return (
                  <LinhaConferencia
                    key={itemId}
                    nome={item?.nome ?? 'Item'}
                    grupo={item?.grupoNome ?? ''}
                    unidade={item?.unidade ?? 'Unidade'}
                    custo={item?.custoEmbalagem ?? 0}
                    campo={campo}
                    comPreco={precoMudou}
                    ocupado={receber.isPending}
                    aoMudar={(m) => alterar(itemId, m, true)}
                    aoSomar={(delta) => somar(itemId, passoDeRecebimento(item?.unidade ?? 'Unidade'), delta, true)}
                    aoTirar={() => {
                      setExtras((atuais) => {
                        const copia = { ...atuais }
                        delete copia[itemId]
                        return copia
                      })
                      setConfirmar(false)
                    }}
                  />
                )
              })}
            </tbody>
          </table>
        </div>
        {opcoesExtra.length > 0 && (
          <div className="linha-extra">
            <label className="campo-inline">
              Veio algo que não foi pedido?
              <select value={extraId} onChange={(e) => setExtraId(e.target.value)} disabled={receber.isPending}>
                <option value="">Escolha o sabor…</option>
                {opcoesExtra.map((i) => (
                  <option key={i.id} value={i.id}>
                    {i.grupoNome} — {i.nome}
                  </option>
                ))}
              </select>
            </label>
            <button
              type="button"
              className="botao botao-secundario"
              disabled={!extraId || receber.isPending}
              onClick={() => {
                setExtras((atuais) => ({ ...atuais, [extraId]: { veio: '1', pedirDeNovo: false, preco: '' } }))
                setExtraId('')
                setConfirmar(false)
              }}
            >
              Acrescentar
            </button>
          </div>
        )}
      </section>

      <section className="secao rodape-acao rodape-fixo">
        {erro && <p className="alerta alerta-erro">{erro}</p>}
        {receber.isError && <p className="alerta alerta-erro">{mensagemDoErro(receber.error)}</p>}
        {confirmar ? (
          <>
            <p>
              <strong>{quantasDiferencas === 0 ? 'Chegou tudo certo?' : `Confirmar a conferência com ${quantasDiferencas} ${quantasDiferencas === 1 ? 'diferença' : 'diferenças'}?`}</strong>{' '}
              <span className="suave">Depois de conferida, a entrega não muda mais. Os avisos dos itens que chegaram fecham sozinhos.</span>
            </p>
            <span className="pagina-acoes">
              <button type="button" className="botao botao-secundario" disabled={receber.isPending} onClick={() => setConfirmar(false)}>
                Voltar
              </button>
              <button type="button" className="botao" disabled={receber.isPending} onClick={enviar}>
                {receber.isPending ? 'Conferindo…' : 'Confirmar'}
              </button>
            </span>
          </>
        ) : (
          <>
            <p className="suave">
              {quantasDiferencas === 0
                ? 'Nada diferente do pedido até agora.'
                : `${quantasDiferencas} ${quantasDiferencas === 1 ? 'item veio diferente' : 'itens vieram diferentes'} do pedido.`}
            </p>
            <span className="pagina-acoes">
              <button type="button" className="botao" disabled={receber.isPending} onClick={pedirConfirmacao}>
                {quantasDiferencas === 0 ? 'Chegou tudo certo' : 'Conferir com as diferenças'}
              </button>
            </span>
          </>
        )}
      </section>
    </>
  )
}

function LinhaConferencia({
  nome,
  grupo,
  linha,
  unidade = linha?.unidade ?? 'Unidade',
  custo = linha?.custoEmbalagem ?? 0,
  campo,
  comPreco,
  ocupado,
  aoMudar,
  aoSomar,
  aoTirar,
}: {
  nome: string
  grupo: string
  linha?: LinhaPedidoDto
  unidade?: LinhaPedidoDto['unidade']
  custo?: number
  campo: Campo
  comPreco: boolean
  ocupado: boolean
  aoMudar: (mudanca: Partial<Campo>) => void
  aoSomar: (delta: number) => void
  aoTirar?: () => void
}) {
  const pedido = linha?.pedidoEmbalagens ?? 0
  const veio = lerQuantidade(campo.veio)
  const situacao = veio === null ? null : situacaoNaConferencia(pedido, veio)
  const marca = situacao && situacao !== 'Certo' ? SITUACAO_RECEBIMENTO[situacao] : null

  return (
    <tr className={marca ? 'linha-destaque' : undefined}>
      <td>
        <span className="suave pequeno bloco">{grupo}</span>
        <strong>{nome}</strong> {marca && <Selo tom={marca.tom}>{marca.nome}</Selo>}
        {situacao === 'Faltou' && (
          <label className="campo-marcar pequeno">
            <input type="checkbox" checked={campo.pedirDeNovo} disabled={ocupado} onChange={(e) => aoMudar({ pedirDeNovo: e.target.checked })} />
            Pedir de novo no próximo pedido
          </label>
        )}
        {aoTirar && (
          <button type="button" className="botao-link pequeno" onClick={aoTirar} disabled={ocupado}>
            tirar
          </button>
        )}
        <span className="suave bloco pequeno so-estreito">{linha ? `Pedido: ${descreverEmbalagens(pedido, unidade)}` : 'Não estava no pedido'}</span>
        {comPreco && (
          <label className="so-estreito campo-preco">
            <span className="suave pequeno">Preço novo (só se mudou)</span>
            <input inputMode="decimal" placeholder={formatarMoeda(custo)} value={campo.preco} disabled={ocupado} onChange={(e) => aoMudar({ preco: e.target.value })} />
          </label>
        )}
      </td>
      <td className="numero so-largo">{linha ? descreverEmbalagens(pedido, unidade) : <span className="suave">não pedido</span>}</td>
      <td className="numero">
        <span className="contador-campo">
          <button type="button" className="botao botao-secundario" aria-label={`Menos ${nome}`} disabled={ocupado} onClick={() => aoSomar(-1)}>
            −
          </button>
          <input
            inputMode="decimal"
            aria-label={`${nome}: quanto veio, em ${embalagemNoPlural(unidade)}`}
            value={campo.veio}
            disabled={ocupado}
            onChange={(e) => aoMudar({ veio: e.target.value })}
          />
          <button type="button" className="botao botao-secundario" aria-label={`Mais ${nome}`} disabled={ocupado} onClick={() => aoSomar(1)}>
            +
          </button>
        </span>
      </td>
      {comPreco && (
        <td className="so-largo">
          <input
            className="campo-tabela"
          inputMode="decimal"
          aria-label={`Preço novo de ${nome}`}
          placeholder={formatarMoeda(custo)}
            value={campo.preco}
            disabled={ocupado}
            onChange={(e) => aoMudar({ preco: e.target.value })}
          />
        </td>
      )}
    </tr>
  )
}

function Resultado({ resultado }: { resultado: ReceberPedidoResponse }) {
  const { pedido } = resultado
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

  return (
    <>
      <CabecalhoPagina
        titulo={`Entrega do pedido nº ${pedido.numero} conferida`}
        subtitulo={`${pedido.fornecedorNome} · ${resultado.avisosFechados === 0 ? 'nenhum aviso fechou' : `${resultado.avisosFechados} ${resultado.avisosFechados === 1 ? 'aviso fechou' : 'avisos fecharam'} com a entrega`}`}
        acoes={<Selo tom="bom">Recebido</Selo>}
      />
      {resultado.mensagemFornecedor && resultado.linkWhatsApp ? (
        <Secao titulo="Avisar o fornecedor do que veio diferente">
          <p className="suave">O que faltou volta sozinho no próximo pedido (menos o que você desmarcou).</p>
          <pre className="mensagem-whatsapp">{resultado.mensagemFornecedor}</pre>
          <div className="acoes acoes-inicio">
            <a className="botao" href={resultado.linkWhatsApp} target="_blank" rel="noreferrer">
              Abrir no WhatsApp
            </a>
            <button type="button" className="botao botao-secundario" onClick={() => void copiar(resultado.mensagemFornecedor!)}>
              {copiado ? 'Copiado' : 'Copiar texto'}
            </button>
          </div>
        </Secao>
      ) : (
        <p className="alerta alerta-sucesso">Chegou tudo como foi pedido.</p>
      )}
      <div className="acoes acoes-inicio">
        <Link to={`/pedidos/${pedido.id}`} className="botao botao-secundario">
          Ver o pedido conferido
        </Link>
        <Link to="/pedidos" className="botao botao-secundario">
          Voltar aos pedidos
        </Link>
      </div>
    </>
  )
}
