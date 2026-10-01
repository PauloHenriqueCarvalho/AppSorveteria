import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { CHAVE_COMANDAS, estornarComanda, obterComanda } from '../../api/comandas.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { ComandaDetalheDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { formatarDataHora, formatarMoeda, nomeDaForma, nomeDoStatus } from '../../formatacao.ts'

export default function DetalheVenda() {
  const { id = '' } = useParams()
  const venda = useQuery({ queryKey: [...CHAVE_COMANDAS, id], queryFn: () => obterComanda(id) })

  return (
    <>
      <p>
        <Link to="/vendas">← Vendas</Link>
      </p>
      <Consulta consulta={venda}>{(v) => <Venda venda={v} />}</Consulta>
    </>
  )
}

function Venda({ venda }: { venda: ComandaDetalheDto }) {
  return (
    <>
      <section className="cartao">
        <div className="cabecalho-pagina">
          <h1>Comanda {venda.numero}</h1>
          <span className={venda.status === 'Fechada' ? 'etiqueta etiqueta-ok' : 'etiqueta'}>{nomeDoStatus(venda.status)}</span>
        </div>
        <p className="suave">
          {formatarDataHora(venda.fechadaEm ?? venda.criadaEm)} · {venda.atendenteNome}
          {venda.tipo === 'Delivery' && ' · delivery'}
        </p>
        {venda.observacao && <p>Observação: {venda.observacao}</p>}

        <ul className="lista lista-simples">
          {venda.itens.map((i) => (
            <li key={i.id} className="lista-item">
              <span className="lista-principal">
                <strong>
                  {i.quantidade} × {i.descricao}
                </strong>
                {i.quantidade > 1 && <span className="suave">{formatarMoeda(i.precoUnitario)} cada</span>}
              </span>
              <span className="lista-lateral">{formatarMoeda(i.subtotal)}</span>
            </li>
          ))}
        </ul>
        <dl className="valores">
          <dt>
            <strong>Total</strong>
          </dt>
          <dd>{formatarMoeda(venda.total)}</dd>
        </dl>
      </section>

      {venda.pagamentos.length > 0 && (
        <section className="cartao">
          <h2>Pagamento</h2>
          <dl className="valores">
            {venda.pagamentos.map((p) => (
              <div key={p.id} className="valores-linha">
                <dt>
                  {nomeDaForma(p.forma)}
                  {p.troco > 0 && (
                    <span className="suave">
                      {' '}
                      (recebeu {formatarMoeda(p.valorRecebido)}, troco {formatarMoeda(p.troco)})
                    </span>
                  )}
                </dt>
                <dd>{formatarMoeda(p.valor)}</dd>
              </div>
            ))}
          </dl>
        </section>
      )}

      {venda.status === 'Cancelada' && (
        <p className="alerta alerta-aviso">
          Cancelada no balcão em {formatarDataHora(venda.canceladaEm!)}
          {venda.motivoCancelamento && `: ${venda.motivoCancelamento}`}.
        </p>
      )}
      {venda.status === 'Estornada' && (
        <p className="alerta alerta-aviso">
          Estornada em {formatarDataHora(venda.estornadaEm!)} por {venda.estornadaPorNome}: {venda.motivoEstorno}.
        </p>
      )}
      {venda.recebidaAposFechamentoCaixa && (
        <p className="alerta alerta-aviso">Esta venda chegou do celular depois que o caixa dela foi fechado.</p>
      )}

      {venda.status === 'Fechada' && <Estorno venda={venda} />}
    </>
  )
}

/** RN-CM-09: estorno com motivo, em dois passos para não estornar por engano. */
function Estorno({ venda }: { venda: ComandaDetalheDto }) {
  const queryClient = useQueryClient()
  const [aberto, setAberto] = useState(false)
  const [motivo, setMotivo] = useState('')

  const estornar = useMutation({
    mutationFn: () => estornarComanda(venda.id, motivo.trim()),
    onSuccess: (atualizada) => {
      queryClient.setQueryData([...CHAVE_COMANDAS, atualizada.id], atualizada)
      void queryClient.invalidateQueries({ queryKey: [...CHAVE_COMANDAS, 'lista'] })
      void queryClient.invalidateQueries({ queryKey: ['relatorios'] })
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    estornar.mutate()
  }

  if (!aberto) {
    return (
      <section className="cartao">
        <button type="button" className="botao botao-secundario" onClick={() => setAberto(true)}>
          Estornar esta venda
        </button>
      </section>
    )
  }

  return (
    <form className="cartao formulario" onSubmit={enviar}>
      <h2>Estornar a comanda {venda.numero}</h2>
      <p>
        A venda de {formatarMoeda(venda.total)} sai do total vendido do dia e fica marcada como estornada. Nada é apagado.
      </p>
      <p className="suave">
        O caixa não muda. Se você devolveu dinheiro ao cliente, registre uma retirada (sangria) no celular do balcão.
      </p>
      <label className="campo">
        <span>Motivo</span>
        <textarea
          required
          minLength={3}
          maxLength={300}
          rows={3}
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          placeholder="Ex.: cobrado duas vezes"
        />
      </label>
      {estornar.isError && <p className="alerta alerta-erro">{mensagemDoErro(estornar.error)}</p>}
      <div className="acoes">
        <button type="button" className="botao botao-secundario" disabled={estornar.isPending} onClick={() => setAberto(false)}>
          Voltar
        </button>
        <button type="submit" className="botao botao-perigo" disabled={estornar.isPending}>
          {estornar.isPending ? 'Estornando…' : 'Confirmar estorno'}
        </button>
      </div>
    </form>
  )
}
