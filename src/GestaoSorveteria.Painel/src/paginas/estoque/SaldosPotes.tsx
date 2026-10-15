import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { CHAVE_SALDOS, listarSaldosPotes } from '../../api/estoque.ts'
import type { SaldoPoteDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Indicador, Selo, type Tom } from '../../componentes/Ui.tsx'
import { quando } from '../../estoque.ts'

/**
 * /potes — RN-ES-08: saldo de cada pote (grupos com saldo pela venda) = última contagem + entregas − vendas depois dela.
 * Só mostra o que a API calcula; a situação compara o saldo com o mínimo (quantidades, não dinheiro).
 */
export default function SaldosPotes() {
  const saldos = useQuery({ queryKey: CHAVE_SALDOS, queryFn: listarSaldosPotes, refetchInterval: 120_000 })

  return (
    <>
      <CabecalhoPagina
        titulo="Saldo dos potes"
        subtitulo="Parte da última contagem; a entrega conferida soma e cada venda com sabor desconta. A contagem semanal confere."
        acoes={
          <Link to="/contagens/nova" className="botao botao-secundario">
            Contar agora
          </Link>
        }
      />
      <Consulta consulta={saldos}>{(lista) => <Conteudo lista={lista} />}</Consulta>
    </>
  )
}

function situacao(s: SaldoPoteDto): { nome: string; tom: Tom } {
  if (s.saldoUnidades < 0) {
    return { nome: 'Conferir', tom: 'ruim' }
  }
  if (s.saldoUnidades === 0) {
    return { nome: 'Acabou', tom: 'ruim' }
  }
  if (s.minimoUnidades !== null && s.saldoUnidades <= s.minimoUnidades) {
    return { nome: 'No mínimo', tom: 'atencao' }
  }
  return { nome: 'Ok', tom: 'bom' }
}

function Conteudo({ lista }: { lista: SaldoPoteDto[] }) {
  if (lista.length === 0) {
    return (
      <section className="secao vazio">
        <h2>Nenhum grupo com saldo pela venda</h2>
        <p className="suave">
          No catálogo, mude o grupo dos potes para "Saldo pela venda", faça uma contagem dos potes e ligue os produtos de pote ao grupo
          (Produtos → produto → Estoque).
        </p>
        <div className="acoes acoes-inicio">
          <Link to="/estoque" className="botao">
            Catálogo
          </Link>
        </div>
      </section>
    )
  }

  const acabou = lista.filter((s) => s.saldoUnidades <= 0).length
  const noMinimo = lista.filter((s) => s.saldoUnidades > 0 && s.minimoUnidades !== null && s.saldoUnidades <= s.minimoUnidades).length
  const semContagem = lista.filter((s) => s.ultimaContagemEm === null).length
  const grupos = [...new Map(lista.map((s) => [s.grupoId, s.grupoNome])).entries()]

  return (
    <>
      <div className="grade-indicadores grade-indicadores-3">
        <Indicador rotulo="Acabaram" valor={acabou} detalhe="ou com saldo para conferir" tom={acabou > 0 ? 'ruim' : 'bom'} />
        <Indicador rotulo="No mínimo" valor={noMinimo} detalhe="entram no resumo do dia" tom={noMinimo > 0 ? 'atencao' : 'bom'} />
        <Indicador rotulo="Sem contagem" valor={semContagem} detalhe="o saldo só vale depois da primeira" tom={semContagem > 0 ? 'atencao' : 'neutro'} />
      </div>

      <section className="secao secao-tabela">
        <div className="tabela-caixa">
          <table className="tabela">
            <thead>
              <tr>
                <th scope="col">Sabor</th>
                <th scope="col" className="numero">
                  Saldo
                </th>
                <th scope="col" className="numero so-largo">
                  Mínimo
                </th>
                <th scope="col" className="numero so-largo">
                  Vendidos 7 dias
                </th>
                <th scope="col" className="so-largo">
                  Última contagem
                </th>
                <th scope="col">Situação</th>
              </tr>
            </thead>
            {grupos.map(([grupoId, grupoNome]) => (
              <tbody key={grupoId}>
                <tr className="linha-grupo">
                  <th scope="rowgroup" colSpan={6}>
                    <span className="linha-grupo-conteudo">
                      <strong>{grupoNome}</strong>
                    </span>
                  </th>
                </tr>
                {lista
                  .filter((s) => s.grupoId === grupoId)
                  .map((s) => {
                    const sit = situacao(s)
                    return (
                      <tr key={s.itemEstoqueId} className={s.ativo ? undefined : 'linha-apagada'}>
                        <td>
                          <strong>{s.itemNome}</strong>
                          {!s.ativo && <span className="suave pequeno"> · desativado</span>}
                          <span className="suave bloco pequeno so-estreito">
                            {s.ultimaContagemEm ? `Contado ${s.ultimaContagemUnidades} ${quando(s.ultimaContagemEm)}` : 'Nunca contado'} ·{' '}
                            {s.vendidosNaSemana} vendidos na semana
                          </span>
                        </td>
                        <td className="numero forte">{s.saldoUnidades}</td>
                        <td className="numero so-largo suave">{s.minimoUnidades ?? '—'}</td>
                        <td className="numero so-largo">{s.vendidosNaSemana}</td>
                        <td className="so-largo suave">
                          {s.ultimaContagemEm ? `${s.ultimaContagemUnidades} · ${quando(s.ultimaContagemEm)}` : 'nunca contado'}
                        </td>
                        <td>
                          <Selo tom={sit.tom}>{sit.nome}</Selo>
                        </td>
                      </tr>
                    )
                  })}
              </tbody>
            ))}
          </table>
        </div>
      </section>
    </>
  )
}
