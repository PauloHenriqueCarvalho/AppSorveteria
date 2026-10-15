import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { CHAVE_PEDIDOS, listarPedidos, montarPedido } from '../../api/pedidos.ts'
import type { CatalogoEstoqueResponse, FornecedorDto, PedidoResumoDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Secao, Selo } from '../../componentes/Ui.tsx'
import { descreverDias, quando } from '../../estoque.ts'
import { formatarDia, formatarMoeda, hojeNaLoja } from '../../formatacao.ts'
import { entregaPrevistaDe, enviadoHoje, formatarPercentual, proximoDiaDePedido, STATUS_PEDIDO } from '../../pedidos.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

/**
 * /pedidos — pedido por fornecedor (RN-PF-02…08) e conferência da entrega (RN-RC-01…06). No dia de pedido do fornecedor o
 * rascunho é montado sozinho ao abrir esta tela (RN-PF-04); a dona ajusta, imprime ou manda pelo WhatsApp.
 */
export default function ListaPedidos() {
  const catalogo = useCatalogoEstoque()
  const pedidos = useQuery({ queryKey: CHAVE_PEDIDOS, queryFn: listarPedidos })

  return (
    <>
      <CabecalhoPagina
        titulo="Pedidos aos fornecedores"
        subtitulo="A sugestão sai da contagem, dos avisos e do que faltou na última entrega. Você ajusta, imprime ou manda pelo WhatsApp e confere quando chegar."
      />
      <Consulta consulta={catalogo}>
        {(dadosCatalogo) => <Consulta consulta={pedidos}>{(lista) => <Conteudo catalogo={dadosCatalogo} pedidos={lista} />}</Consulta>}
      </Consulta>
    </>
  )
}

function Conteudo({ catalogo, pedidos }: { catalogo: CatalogoEstoqueResponse; pedidos: PedidoResumoDto[] }) {
  const hoje = hojeNaLoja()
  const fornecedores = catalogo.fornecedores
    .filter((f) => f.ativo && catalogo.itens.some((i) => i.ativo && i.fornecedorId === f.id))
    .map((f) => ({ fornecedor: f, proximo: proximoDiaDePedido(f.diasPedido, hoje) }))
    .sort((a, b) => (a.proximo ?? '9999').localeCompare(b.proximo ?? '9999') || a.fornecedor.nome.localeCompare(b.fornecedor.nome))
  const { montados, falharam } = useMontarSozinho(
    fornecedores.filter((f) => f.proximo === hoje).map((f) => f.fornecedor),
    pedidos,
    hoje,
  )

  return (
    <>
      {montados.length > 0 && (
        <p className="alerta alerta-aviso">
          Hoje é dia de pedido: o rascunho de {montados.join(' e ')} já está montado com a sugestão de hoje. Confira e envie.
        </p>
      )}
      {falharam.length > 0 && (
        <p className="alerta alerta-erro">
          Não consegui montar o pedido de hoje de {falharam.join(' e ')} (o servidor pode estar acordando). Use “Montar pedido” no
          fornecedor.
        </p>
      )}

      {fornecedores.length === 0 ? (
        <section className="secao vazio">
          <h2>Nenhum fornecedor com itens</h2>
          <p className="suave">Cadastre os fornecedores e importe a planilha no catálogo para o pedido sair sozinho.</p>
          <div className="acoes acoes-inicio">
            <Link to="/estoque/importar" className="botao">
              Importar planilha
            </Link>
            <Link to="/fornecedores" className="botao botao-secundario">
              Fornecedores
            </Link>
          </div>
        </section>
      ) : (
        <div className="grade-fornecedores">
          {fornecedores.map(({ fornecedor, proximo }) => (
            <CartaoFornecedor
              key={fornecedor.id}
              fornecedor={fornecedor}
              proximo={proximo}
              hoje={hoje}
              pedidos={pedidos.filter((p) => p.fornecedorId === fornecedor.id)}
            />
          ))}
        </div>
      )}

      <TabelaPedidos pedidos={pedidos} />
    </>
  )
}

/**
 * RN-PF-04: no dia de pedido, ao abrir a tela, monta (ou refaz, mantendo o que a dona mudou) o rascunho do fornecedor com a
 * sugestão de hoje — rascunho de outro dia ficaria com a contagem e os avisos velhos. Pula se o rascunho já é de hoje ou se
 * o pedido de hoje já saiu; a API também confere (modo automático) e devolve o enviado em vez de montar outro.
 * Uma vez por fornecedor e dia neste navegador, para não remontar a cada visita.
 */
function useMontarSozinho(
  doDia: FornecedorDto[],
  pedidos: PedidoResumoDto[],
  hoje: string,
): { montados: string[]; falharam: string[] } {
  const queryClient = useQueryClient()
  const [montados, setMontados] = useState<string[]>([])
  const [falharam, setFalharam] = useState<string[]>([])
  const tentados = useRef(new Set<string>())

  useEffect(() => {
    const pendentes = doDia.filter(
      (f) =>
        !tentados.current.has(f.id) &&
        !jaMontadoHoje(f.id, hoje) &&
        !pedidos.some(
          (p) =>
            p.fornecedorId === f.id &&
            ((p.status === 'Rascunho' && hojeNaLoja(new Date(p.criadoEm)) === hoje) || enviadoHoje(p.enviadoEm, hoje)),
        ),
    )
    if (pendentes.length === 0) {
      return
    }
    pendentes.forEach((f) => tentados.current.add(f.id))
    void Promise.allSettled(
      pendentes.map(async (f) => {
        const pedido = await montarPedido(f.id, true)
        marcarMontadoHoje(f.id, hoje)
        return pedido.status === 'Rascunho' ? f.nome : null
      }),
    ).then(async (resultados) => {
      const nomes = resultados.flatMap((r) => (r.status === 'fulfilled' && r.value ? [r.value] : []))
      const comErro = resultados.flatMap((r, i) => (r.status === 'rejected' ? [pendentes[i].nome] : []))
      if (nomes.length > 0) {
        setMontados((atuais) => [...atuais, ...nomes])
      }
      if (comErro.length > 0) {
        setFalharam((atuais) => [...atuais, ...comErro])
      }
      await queryClient.invalidateQueries({ queryKey: CHAVE_PEDIDOS })
    })
  }, [doDia, pedidos, hoje, queryClient])

  return { montados, falharam }
}

function chaveMontado(fornecedorId: string, hoje: string) {
  return `pedido-montado:${fornecedorId}:${hoje}`
}

function jaMontadoHoje(fornecedorId: string, hoje: string): boolean {
  try {
    return window.localStorage.getItem(chaveMontado(fornecedorId, hoje)) !== null
  } catch {
    return false
  }
}

function marcarMontadoHoje(fornecedorId: string, hoje: string) {
  try {
    window.localStorage.setItem(chaveMontado(fornecedorId, hoje), '1')
  } catch {
    // sem armazenamento: a API em modo automático evita o pedido em dobro
  }
}

function CartaoFornecedor({
  fornecedor,
  proximo,
  hoje,
  pedidos,
}: {
  fornecedor: FornecedorDto
  proximo: string | null
  hoje: string
  pedidos: PedidoResumoDto[]
}) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const rascunho = pedidos.find((p) => p.status === 'Rascunho')
  const aCaminho = pedidos.filter((p) => p.status === 'Enviado').sort((a, b) => a.numero - b.numero)
  const montar = useMutation({
    mutationFn: () => montarPedido(fornecedor.id),
    onSuccess: async (pedido) => {
      queryClient.setQueryData([...CHAVE_PEDIDOS, pedido.id], pedido)
      await queryClient.invalidateQueries({ queryKey: CHAVE_PEDIDOS })
      navegar(`/pedidos/${pedido.id}`)
    },
  })

  return (
    <section className="secao cartao-fornecedor">
      <div className="secao-cabecalho">
        <h2>
          <Link to={`/fornecedores/${fornecedor.id}`} className="link-discreto">
            {fornecedor.nome}
          </Link>
        </h2>
        {proximo === hoje ? <Selo tom="marca">Hoje é dia de pedido</Selo> : proximo && <Selo>Próximo: {formatarDia(proximo).slice(0, 5)}</Selo>}
      </div>
      <p className="suave pequeno">
        Pede {descreverDias(fornecedor.diasPedido).toLowerCase()} · entrega em {fornecedor.prazoEntregaDias}{' '}
        {fornecedor.prazoEntregaDias === 1 ? 'dia' : 'dias'}
      </p>

      <ul className="lista-simples">
        {aCaminho.map((p) => {
          const entrega = p.enviadoEm ? entregaPrevistaDe(p.enviadoEm, fornecedor.prazoEntregaDias) : hoje
          const conferir = entrega <= hoje
          return (
            <li key={p.id}>
              <Link to={`/pedidos/${p.id}`}>Pedido nº {p.numero}</Link>{' '}
              {conferir ? (
                <>
                  <Selo tom="atencao">Chegou?</Selo>{' '}
                  <Link to={`/pedidos/${p.id}/conferir`} className="botao botao-pequeno">
                    Conferir entrega
                  </Link>
                </>
              ) : (
                <span className="suave">a caminho · chega {formatarDia(entrega).slice(0, 5)}</span>
              )}
            </li>
          )
        })}
        {rascunho && (
          <li>
            <Link to={`/pedidos/${rascunho.id}`}>Rascunho nº {rascunho.numero}</Link>{' '}
            <span className="suave">
              · {rascunho.itensComQuantidade} {rascunho.itensComQuantidade === 1 ? 'item' : 'itens'} · {formatarMoeda(rascunho.totalPedido)}
            </span>
          </li>
        )}
      </ul>

      {montar.isError && <p className="alerta alerta-erro">{mensagemDoErro(montar.error)}</p>}
      <div className="acoes acoes-inicio">
        {rascunho ? (
          <Link to={`/pedidos/${rascunho.id}`} className="botao">
            Continuar o rascunho
          </Link>
        ) : (
          <button type="button" className={proximo === hoje ? 'botao' : 'botao botao-secundario'} disabled={montar.isPending} onClick={() => montar.mutate()}>
            {montar.isPending ? 'Montando…' : 'Montar pedido'}
          </button>
        )}
      </div>
    </section>
  )
}

function TabelaPedidos({ pedidos }: { pedidos: PedidoResumoDto[] }) {
  const navegar = useNavigate()
  if (pedidos.length === 0) {
    return (
      <section className="secao vazio">
        <h2>Nenhum pedido ainda</h2>
        <p className="suave">Use “Montar pedido” no fornecedor. A sugestão melhora a cada contagem semanal.</p>
      </section>
    )
  }

  return (
    <Secao titulo="Pedidos" className="secao-tabela">
      <div className="tabela-caixa">
        <table className="tabela">
          <thead>
            <tr>
              <th scope="col">Nº</th>
              <th scope="col">Fornecedor</th>
              <th scope="col">Situação</th>
              <th scope="col" className="so-largo">
                Quando
              </th>
              <th scope="col" className="numero so-largo">
                Itens
              </th>
              <th scope="col" className="numero">
                Total
              </th>
              <th scope="col" className="numero so-largo">
                Atendido
              </th>
            </tr>
          </thead>
          <tbody>
            {pedidos.map((p) => (
              <tr key={p.id} className="linha-clicavel" onClick={() => navegar(`/pedidos/${p.id}`)}>
                <td>
                  <Link to={`/pedidos/${p.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
                    {p.numero}
                  </Link>
                </td>
                <td>{p.fornecedorNome}</td>
                <td>
                  <Selo tom={STATUS_PEDIDO[p.status].tom}>{STATUS_PEDIDO[p.status].nome}</Selo>
                </td>
                <td className="so-largo suave">{quando(p.recebidoEm ?? p.canceladoEm ?? p.enviadoEm ?? p.criadoEm)}</td>
                <td className="numero so-largo">{p.itensComQuantidade}</td>
                <td className="numero">
                  {formatarMoeda(p.totalPedido)}
                  {p.totalRecebido !== null && <span className="suave bloco pequeno">veio {formatarMoeda(p.totalRecebido)}</span>}
                </td>
                <td className="numero so-largo">{formatarPercentual(p.percentualAtendido)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Secao>
  )
}
