import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { CHAVE_COMANDAS, listarComandas } from '../api/comandas.ts'
import { CHAVE_RELATORIOS, obterResumoDoDia } from '../api/relatorios.ts'
import type { ComandaResumoDto, ResumoDiaDto } from '../api/tipos.ts'
import Consulta from '../componentes/Consulta.tsx'
import { ConferenciaCaixa, ResultadoCaixa } from '../componentes/ConferenciaCaixa.tsx'
import GraficoHoras from '../componentes/GraficoHoras.tsx'
import Icone from '../componentes/Icone.tsx'
import { Barra, CabecalhoPagina, Esqueleto, Indicador, Secao, Selo, type Tom } from '../componentes/Ui.tsx'
import {
  formatarDataHora,
  formatarDia,
  formatarDiaPorExtenso,
  formatarHora,
  formatarMoeda,
  formatarVariacao,
  hojeNaLoja,
  nomeDaForma,
  nomeDoStatus,
  primeiroNome,
  saudacao,
} from '../formatacao.ts'
import { useSessao } from '../sessao/contexto.ts'

// Enquanto a tela está aberta, atualiza sozinha (só com a aba visível).
const ATUALIZAR_A_CADA_MS = 2 * 60_000
const ULTIMAS_VENDAS = 8

/** Dashboard do dia: quanto vendeu, como pagaram, se o caixa está batendo e o que pede atenção. */
export default function Inicio() {
  const { usuario } = useSessao()
  const [dia, setDia] = useState(() => hojeNaLoja())
  const hoje = dia === hojeNaLoja()

  const resumo = useQuery({
    queryKey: [...CHAVE_RELATORIOS, 'dia', dia],
    queryFn: () => obterResumoDoDia(dia),
    refetchInterval: hoje ? ATUALIZAR_A_CADA_MS : false,
  })

  return (
    <>
      <CabecalhoPagina
        titulo={hoje ? `${saudacao()}, ${primeiroNome(usuario?.nome ?? '')}` : formatarDia(dia)}
        subtitulo={hoje ? `Resumo de hoje, ${formatarDiaPorExtenso(dia)}` : `Resumo de ${formatarDiaPorExtenso(dia)}`}
        acoes={
          <>
            <label className="campo-inline">
              <span>Dia</span>
              <input
                type="date"
                value={dia}
                max={hojeNaLoja()}
                onChange={(e) => setDia(e.target.value || hojeNaLoja())}
              />
            </label>
            <Link to="/vendas" className="botao botao-secundario">
              Ver vendas
            </Link>
          </>
        }
      />
      <Consulta consulta={resumo}>{(r) => <Painel resumo={r} hoje={hoje} dia={dia} />}</Consulta>
    </>
  )
}

function Painel({ resumo, hoje, dia }: { resumo: ResumoDiaDto; hoje: boolean; dia: string }) {
  const caixa = hoje ? resumo.caixaAtual : null
  const antes = resumo.semanaPassada
  const diaAntes = formatarDia(antes.data).slice(0, 5)
  const contexto = antes.ateMesmoHorario ? `vs. ${diaAntes} no mesmo horário` : `vs. ${diaAntes}`

  return (
    <div className="painel">
      <section className="grade-indicadores" aria-label="Indicadores do dia">
        <Indicador
          rotulo="Faturamento"
          valor={formatarMoeda(resumo.totalVendido)}
          detalhe={<Comparacao variacao={antes.variacaoTotalPercentual} contexto={contexto} anterior={formatarMoeda(antes.totalVendido)} />}
          tom="marca"
        />
        <Indicador
          rotulo="Vendas"
          valor={resumo.quantidadeComandas}
          detalhe={<Comparacao variacao={antes.variacaoQuantidadePercentual} contexto={contexto} anterior={String(antes.quantidadeComandas)} />}
        />
        <Indicador
          rotulo="Ticket médio"
          valor={formatarMoeda(resumo.ticketMedio)}
          detalhe={<Comparacao variacao={antes.variacaoTicketPercentual} contexto={contexto} anterior={formatarMoeda(antes.ticketMedio)} />}
        />
        {caixa ? (
          <Indicador
            rotulo="Dinheiro na gaveta"
            valor={caixa.valorEsperado === null ? '—' : formatarMoeda(caixa.valorEsperado)}
            detalhe={`esperado agora · caixa aberto às ${formatarHora(caixa.abertoEm)} por ${caixa.abertoPorNome}`}
          />
        ) : (
          <Indicador
            rotulo="Caixa"
            valor={hoje ? 'Fechado' : '—'}
            detalhe={hoje ? 'nenhum caixa aberto agora' : 'veja os caixas do dia'}
            tom={hoje ? 'atencao' : 'neutro'}
          />
        )}
      </section>

      <div className="grade-painel">
        <Secao titulo="Vendas por hora" className="grade-largo">
          <GraficoHoras
            hoje={resumo.porHora}
            antes={antes.porHora}
            rotuloHoje={hoje ? 'Hoje' : formatarDia(dia).slice(0, 5)}
            rotuloAntes={antes.ateMesmoHorario ? `${diaAntes} (até este horário)` : diaAntes}
          />
        </Secao>

        <Secao titulo="Formas de pagamento">
          {resumo.porFormaPagamento.length === 0 ? (
            <p className="suave">Nenhuma venda paga {hoje ? 'ainda hoje' : 'neste dia'}.</p>
          ) : (
            <ul className="lista-barras">
              {resumo.porFormaPagamento.map((f) => {
                // Proporção só para a largura da barra e o %; os valores em R$ vêm prontos da API.
                const fracao = resumo.totalVendido > 0 ? f.total / resumo.totalVendido : 0
                return (
                  <li key={f.forma}>
                    <div className="lista-barras-linha">
                      <span>
                        {nomeDaForma(f.forma)}{' '}
                        <span className="suave">
                          · {f.quantidade} venda{f.quantidade === 1 ? '' : 's'}
                        </span>
                      </span>
                      <span className="numero">
                        <strong>{formatarMoeda(f.total)}</strong> <span className="suave">{Math.round(fracao * 100)}%</span>
                      </span>
                    </div>
                    <Barra fracao={fracao} />
                  </li>
                )
              })}
            </ul>
          )}
        </Secao>
      </div>

      <div className="grade-painel">
        <UltimasVendas dia={dia} hoje={hoje} />
        <Atencao resumo={resumo} hoje={hoje} />
      </div>

      <div className="grade-painel">
        <MaisVendidos resumo={resumo} hoje={hoje} />

        {hoje && (
          <Secao
            titulo="Caixa"
            acao={caixa ? <ResultadoCaixa caixa={caixa} /> : <Selo tom="atencao">Fechado</Selo>}
          >
            {caixa ? (
              <>
                <p className="suave">
                  Aberto em {formatarDataHora(caixa.abertoEm)} por {caixa.abertoPorNome}. Mostra o que o tablet do balcão já
                  enviou.
                </p>
                <ConferenciaCaixa caixa={caixa} />
                <Link to={`/caixas/${caixa.id}`} className="link-seta">
                  Abrir o caixa <Icone nome="seta" tamanho={16} />
                </Link>
              </>
            ) : (
              <p className="suave">
                Nenhum caixa aberto agora. <Link to="/caixas">Ver os caixas fechados</Link>
              </p>
            )}
          </Secao>
        )}
      </div>
    </div>
  )
}

/** "▲ +12,4% vs. 07/10 no mesmo horário". Variação calculada pela API (RN-RL-07); sem base → só o contexto. */
function Comparacao({ variacao, contexto, anterior }: { variacao: number | null; contexto: string; anterior: string }) {
  if (variacao === null) {
    return <span>sem vendas para comparar {contexto.replace('vs.', 'em')}</span>
  }
  const classe = variacao > 0 ? 'variacao variacao-sobe' : variacao < 0 ? 'variacao variacao-desce' : 'variacao'
  const seta = variacao > 0 ? '▲' : variacao < 0 ? '▼' : '='
  return (
    <span title={`Antes: ${anterior}`}>
      <span className={classe}>
        <span aria-hidden="true">{seta}</span> {formatarVariacao(variacao)}
      </span>{' '}
      {contexto}
    </span>
  )
}

function MaisVendidos({ resumo, hoje }: { resumo: ResumoDiaDto; hoje: boolean }) {
  const lider = resumo.maisVendidos[0]?.total ?? 0
  return (
    <Secao titulo="Mais vendidos" className="grade-largo" acao={<span className="suave pequeno">por faturamento</span>}>
      {resumo.maisVendidos.length === 0 ? (
        <p className="suave">Nenhum produto vendido {hoje ? 'ainda hoje' : 'neste dia'}.</p>
      ) : (
        <ol className="ranking">
          {resumo.maisVendidos.map((p, i) => (
            <li key={p.produtoId ?? 'livre'}>
              <span className="ranking-posicao">{i + 1}º</span>
              <div className="ranking-corpo">
                <div className="lista-barras-linha">
                  <span>
                    <strong>{p.descricao}</strong>{' '}
                    <span className="suave">
                      · {p.quantidade} {p.quantidade === 1 ? 'unidade' : 'unidades'}
                    </span>
                  </span>
                  <strong className="numero">{formatarMoeda(p.total)}</strong>
                </div>
                <Barra fracao={lider > 0 ? p.total / lider : 0} tom="neutro" />
              </div>
            </li>
          ))}
        </ol>
      )}
    </Secao>
  )
}

interface ItemAtencao {
  tom: Tom
  titulo: string
  detalhe: string
  para: string
}

/** O que pede uma ação da dona, mais grave primeiro. Tudo vem do resumo da API. */
function Atencao({ resumo, hoje }: { resumo: ResumoDiaDto; hoje: boolean }) {
  const itens: ItemAtencao[] = []
  const caixa = resumo.caixaAtual

  if (resumo.vendasRecebidasAposFechamento > 0) {
    itens.push({
      tom: 'ruim',
      titulo:
        resumo.vendasRecebidasAposFechamento === 1
          ? '1 venda chegou depois do fechamento do caixa'
          : `${resumo.vendasRecebidasAposFechamento} vendas chegaram depois do fechamento do caixa`,
      detalhe: 'Confira a conferência do caixa',
      para: '/caixas',
    })
  }
  if (hoje && caixa?.divergenciaSincronizacao) {
    itens.push({
      tom: 'atencao',
      titulo: 'O caixa do tablet não bateu com o do servidor',
      detalhe: 'Abra o caixa para ver os dois valores',
      para: `/caixas/${caixa.id}`,
    })
  }
  if (resumo.quantidadeEstornos > 0) {
    itens.push({
      tom: 'atencao',
      titulo: `${resumo.quantidadeEstornos} venda${resumo.quantidadeEstornos === 1 ? '' : 's'} estornada${resumo.quantidadeEstornos === 1 ? '' : 's'}`,
      detalhe: `${formatarMoeda(resumo.totalEstornado)} fora do faturamento`,
      para: '/vendas',
    })
  }
  if (hoje && !caixa) {
    itens.push({
      tom: 'neutro',
      titulo: 'Nenhum caixa aberto',
      detalhe: 'O tablet precisa abrir o caixa para vender',
      para: '/caixas',
    })
  }

  return (
    <Secao titulo="Precisa da sua atenção" acao={itens.length > 0 ? <Selo tom="ruim">{itens.length}</Selo> : undefined}>
      {itens.length === 0 ? (
        <p className="tudo-certo">
          <Selo tom="bom">Tudo em ordem</Selo> Nada pendente {hoje ? 'hoje' : 'neste dia'}.
        </p>
      ) : (
        <ul className="lista-atencao">
          {itens.map((item) => (
            <li key={item.titulo}>
              <Link to={item.para} className={`atencao atencao-${item.tom}`}>
                <span className="atencao-ponto" aria-hidden="true" />
                <span>
                  <strong>{item.titulo}</strong>
                  <small>{item.detalhe}</small>
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Secao>
  )
}

const TOM_DO_STATUS: Record<string, Tom> = { Fechada: 'bom', Cancelada: 'neutro', Estornada: 'ruim', Aberta: 'atencao' }

function UltimasVendas({ dia, hoje }: { dia: string; hoje: boolean }) {
  const navegar = useNavigate()
  const vendas = useQuery({
    queryKey: [...CHAVE_COMANDAS, 'lista', dia, undefined, 1],
    queryFn: () => listarComandas({ dia }),
    refetchInterval: hoje ? ATUALIZAR_A_CADA_MS : false,
  })

  return (
    <Secao
      titulo="Últimas vendas"
      className="grade-largo"
      acao={
        <Link to="/vendas" className="link-seta">
          Ver todas <Icone nome="seta" tamanho={16} />
        </Link>
      }
    >
      {vendas.isPending ? (
        <Esqueleto linhas={5} />
      ) : vendas.isError ? (
        <p className="suave">Não foi possível carregar as vendas agora. Elas aparecem em Vendas.</p>
      ) : vendas.data.itens.length === 0 ? (
        <p className="suave">Nenhuma venda {hoje ? 'ainda hoje' : 'neste dia'}.</p>
      ) : (
        <div className="tabela-caixa">
          <table className="tabela">
            <thead>
              <tr>
                <th scope="col">Nº</th>
                <th scope="col">Hora</th>
                <th scope="col" className="so-largo">
                  Atendente
                </th>
                <th scope="col" className="so-largo">
                  Pagamento
                </th>
                <th scope="col">Situação</th>
                <th scope="col" className="numero">
                  Total
                </th>
              </tr>
            </thead>
            <tbody>
              {vendas.data.itens.slice(0, ULTIMAS_VENDAS).map((v: ComandaResumoDto) => (
                <tr key={v.id} className="linha-clicavel" onClick={() => navegar(`/vendas/${v.id}`)}>
                  <td>
                    <Link to={`/vendas/${v.id}`} className="link-linha">
                      {v.numero}
                    </Link>
                  </td>
                  <td className="suave">{formatarHora(v.fechadaEm ?? v.criadaEm)}</td>
                  <td className="so-largo">{v.atendenteNome}</td>
                  <td className="so-largo">
                    {v.formasPagamento.map(nomeDaForma).join(' + ') || '—'}
                    {v.tipo === 'Delivery' && <span className="suave"> · delivery</span>}
                  </td>
                  <td>
                    <Selo tom={TOM_DO_STATUS[v.status] ?? 'neutro'}>{nomeDoStatus(v.status)}</Selo>
                  </td>
                  <td className={v.status === 'Fechada' ? 'numero forte' : 'numero riscado'}>{formatarMoeda(v.total)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Secao>
  )
}
