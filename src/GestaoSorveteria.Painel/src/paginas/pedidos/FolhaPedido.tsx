import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { Link, useParams } from 'react-router'
import { CHAVE_CONFIGURACAO, obterConfiguracao } from '../../api/avisos.ts'
import { CHAVE_PEDIDOS, obterPedido } from '../../api/pedidos.ts'
import type { LinhaPedidoDto, PedidoDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { formatarData, formatarDia, formatarMoeda } from '../../formatacao.ts'
import { LOGO_LOJA, NOME_LOJA } from '../../marca.ts'
import { descreverEmbalagens } from '../../pedidos.ts'

/**
 * /pedidos/:id/imprimir — RN-PF-07: folha A4 para os pais da dona (a fábrica prefere impresso). Letra grande, itens com
 * quantidade agrupados na ordem da planilha, colunas Sabor · Pedido · Veio · Obs. ("Veio" e "Obs." em branco para preencher
 * na entrega). Fica fora do menu: só a folha e os botões, que não saem na impressão.
 */
export default function FolhaPedido() {
  const { id = '' } = useParams()
  const pedido = useQuery({ queryKey: [...CHAVE_PEDIDOS, id], queryFn: () => obterPedido(id) })
  const configuracao = useQuery({ queryKey: CHAVE_CONFIGURACAO, queryFn: obterConfiguracao })

  return (
    <div className="folha-pagina">
      <Consulta consulta={pedido}>{(p) => <Folha pedido={p} nomeLoja={configuracao.data?.nomeLoja ?? NOME_LOJA} />}</Consulta>
    </div>
  )
}

function Folha({ pedido, nomeLoja }: { pedido: PedidoDto; nomeLoja: string }) {
  const linhas = pedido.linhas.filter((l) => l.pedidoEmbalagens > 0)
  const grupos = new Map<string, [string, LinhaPedidoDto[]]>()
  for (const linha of linhas) {
    const grupo = grupos.get(linha.grupoId) ?? [linha.grupoNome, []]
    grupo[1].push(linha)
    grupos.set(linha.grupoId, grupo)
  }
  const data = pedido.enviadoEm ? formatarData(pedido.enviadoEm) : formatarData(new Date().toISOString())

  useEffect(() => {
    document.title = `Pedido nº ${pedido.numero} — ${pedido.fornecedorNome}`
  }, [pedido.numero, pedido.fornecedorNome])

  return (
    <>
      <div className="folha-barra so-tela">
        <Link to={`/pedidos/${pedido.id}`} className="botao botao-secundario">
          ← Voltar ao pedido
        </Link>
        <button type="button" className="botao" onClick={() => window.print()}>
          Imprimir
        </button>
      </div>

      <article className="folha" aria-label={`Folha do pedido nº ${pedido.numero}`}>
        {pedido.status === 'Rascunho' && <p className="folha-rascunho">Rascunho — ainda não enviado</p>}
        {pedido.status === 'Cancelado' && <p className="folha-rascunho">Cancelado</p>}

        <header className="folha-cabecalho">
          <img src={LOGO_LOJA} alt="" width={56} height={64} />
          <div>
            <h1>{nomeLoja}</h1>
            <p>
              Pedido nº <strong>{pedido.numero}</strong> · {data}
            </p>
          </div>
          <dl className="folha-dados">
            <div>
              <dt>Para</dt>
              <dd>{pedido.fornecedorNome}</dd>
            </div>
            <div>
              <dt>Entrega</dt>
              <dd>{formatarDia(pedido.entregaPrevista)}</dd>
            </div>
          </dl>
        </header>

        <table className="folha-tabela">
          <colgroup>
            <col className="folha-col-sabor" />
            <col className="folha-col-pedido" />
            <col className="folha-col-veio" />
            <col className="folha-col-obs" />
          </colgroup>
          <thead>
            <tr>
              <th scope="col">Sabor</th>
              <th scope="col">Pedido</th>
              <th scope="col">Veio</th>
              <th scope="col">Obs.</th>
            </tr>
          </thead>
          {[...grupos.entries()].map(([grupoId, [grupoNome, doGrupo]]) => (
            <tbody key={grupoId}>
              <tr className="folha-grupo">
                <th scope="rowgroup" colSpan={4}>
                  {grupoNome}
                </th>
              </tr>
              {doGrupo.map((l) => (
                <tr key={l.itemEstoqueId}>
                  <td>{l.itemNome}</td>
                  <td className="folha-quantidade">{descreverEmbalagens(l.pedidoEmbalagens, l.unidade)}</td>
                  <td />
                  <td />
                </tr>
              ))}
            </tbody>
          ))}
        </table>

        <footer className="folha-rodape">
          {pedido.mostrarPrecos && (
            <p>
              Total estimado: <strong>{formatarMoeda(pedido.totalPedido)}</strong>
            </p>
          )}
          <p className="folha-assinatura">
            Conferido por: <span /> Data: <span className="folha-curto" />
          </p>
        </footer>
      </article>
    </>
  )
}
