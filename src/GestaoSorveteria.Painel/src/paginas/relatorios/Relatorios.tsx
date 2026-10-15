import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { CHAVE_RELATORIOS, obterRelatorioPeriodo } from '../../api/relatorios.ts'
import type { AgrupamentoRelatorio, RelatorioPeriodoDto } from '../../api/tipos.ts'
import Comparacao from '../../componentes/Comparacao.tsx'
import Consulta from '../../componentes/Consulta.tsx'
import FiltroPeriodo, { type Atalho, type Periodo } from '../../componentes/FiltroPeriodo.tsx'
import GraficoHoras from '../../componentes/GraficoHoras.tsx'
import GraficoTrechos from '../../componentes/GraficoTrechos.tsx'
import { Barra, CabecalhoPagina, Indicador, Secao } from '../../componentes/Ui.tsx'
import { baixarCsv, celulaDinheiro, montarCsv, type Celula } from '../../exportar.ts'
import { formatarMoeda, hojeNaLoja, inicioDoMes, nomeDaForma, somarDias } from '../../formatacao.ts'
import { NOME_LOJA } from '../../marca.ts'
import { descreverPeriodo, descreverPeriodoCurto, descreverTrecho, fimDoMesAnterior, inicioDoMesAntes, nomeDoDiaDaSemana } from '../../relatorios.ts'

/** RN-RL-08: até 366 dias; por dia, até 93. */
const MAXIMO_DIAS = 366
const MAXIMO_DIAS_POR_DIA = 93
const PRODUTOS_NA_LISTA = 15

const ATALHOS: Atalho[] = [
  { texto: '7 dias', periodo: (hoje) => ({ de: somarDias(hoje, -6), ate: hoje }) },
  { texto: '30 dias', periodo: (hoje) => ({ de: somarDias(hoje, -29), ate: hoje }) },
  { texto: 'Este mês', periodo: (hoje) => ({ de: inicioDoMes(hoje), ate: hoje }) },
  { texto: 'Mês passado', periodo: (hoje) => ({ de: inicioDoMesAntes(hoje, 1), ate: fimDoMesAnterior(hoje) }) },
  { texto: '3 meses', periodo: (hoje) => ({ de: inicioDoMesAntes(hoje, 2), ate: hoje }) },
  { texto: 'Este ano', periodo: (hoje) => ({ de: `${hoje.slice(0, 4)}-01-01`, ate: hoje }) },
]

const AGRUPAMENTOS: { valor: AgrupamentoRelatorio; texto: string }[] = [
  { valor: 'Dia', texto: 'Por dia' },
  { valor: 'Semana', texto: 'Por semana' },
  { valor: 'Mes', texto: 'Por mês' },
]

function diasNoPeriodo({ de, ate }: Periodo): number {
  return Math.round((Date.parse(`${ate}T12:00:00Z`) - Date.parse(`${de}T12:00:00Z`)) / 86_400_000) + 1
}

/**
 * /relatorios — Sprint 8: quanto vendeu no período e como foi em relação ao anterior, os horários e dias que mais vendem e
 * o ranking de produtos, categorias e sabores. Tudo calculado pela API (RN-RL-01/03/08/09/10); a tela só mostra.
 */
export default function Relatorios() {
  const [periodo, setPeriodo] = useState<Periodo>(() => ({ de: inicioDoMes(hojeNaLoja()), ate: hojeNaLoja() }))
  const [agrupar, setAgrupar] = useState<AgrupamentoRelatorio | undefined>(undefined)
  const dias = diasNoPeriodo(periodo)
  const longoDemais = dias > MAXIMO_DIAS
  // Por dia só até 93 dias: num período maior, volta para a escolha automática.
  const agrupamento = agrupar === 'Dia' && dias > MAXIMO_DIAS_POR_DIA ? undefined : agrupar

  const relatorio = useQuery({
    queryKey: [...CHAVE_RELATORIOS, 'periodo', periodo.de, periodo.ate, agrupamento],
    queryFn: () => obterRelatorioPeriodo(periodo.de, periodo.ate, agrupamento),
    placeholderData: keepPreviousData,
    enabled: !longoDemais,
  })

  return (
    <>
      <p className="so-impressao">
        {NOME_LOJA} · Relatório de vendas de {descreverPeriodo(periodo.de, periodo.ate)}
      </p>
      <CabecalhoPagina
        titulo="Relatórios"
        subtitulo={
          relatorio.data
            ? `Vendas de ${descreverPeriodo(relatorio.data.de, relatorio.data.ate)} · comparado com ${descreverPeriodo(relatorio.data.anterior.de, relatorio.data.anterior.ate)}`
            : `Vendas de ${descreverPeriodo(periodo.de, periodo.ate)}`
        }
        acoes={
          <>
            <button type="button" className="botao botao-secundario" onClick={() => window.print()}>
              Imprimir
            </button>
            <button
              type="button"
              className="botao botao-secundario"
              onClick={() => relatorio.data && exportar(relatorio.data)}
              disabled={!relatorio.data}
            >
              Exportar Excel
            </button>
          </>
        }
      />

      <div className="filtros">
        <FiltroPeriodo periodo={periodo} aoMudar={setPeriodo} atalhos={ATALHOS} />
        <div className="abas" role="group" aria-label="Agrupar">
          {AGRUPAMENTOS.map((a) => {
            const ativo = (relatorio.data?.agrupamento ?? agrupamento) === a.valor
            return (
              <button
                key={a.valor}
                type="button"
                className={ativo ? 'aba aba-ativa' : 'aba'}
                aria-pressed={ativo}
                disabled={a.valor === 'Dia' && dias > MAXIMO_DIAS_POR_DIA}
                title={a.valor === 'Dia' && dias > MAXIMO_DIAS_POR_DIA ? `Por dia, até ${MAXIMO_DIAS_POR_DIA} dias` : undefined}
                onClick={() => setAgrupar(a.valor)}
              >
                {a.texto}
              </button>
            )
          })}
        </div>
      </div>

      {longoDemais ? (
        <p className="alerta alerta-aviso">Escolha um período de até {MAXIMO_DIAS} dias (um ano).</p>
      ) : (
        <Consulta consulta={relatorio}>{(r) => <Conteudo relatorio={r} />}</Consulta>
      )}
    </>
  )
}

function Conteudo({ relatorio }: { relatorio: RelatorioPeriodoDto }) {
  const antes = relatorio.anterior
  const periodoAntes = descreverPeriodo(antes.de, antes.ate)
  const curto = descreverPeriodoCurto(antes.de, antes.ate)
  const contexto = antes.ateMesmoHorario ? `vs. ${curto} no mesmo horário` : `vs. ${curto}`
  const nomeTrecho = relatorio.agrupamento === 'Dia' ? 'dia' : relatorio.agrupamento === 'Semana' ? 'semana' : 'mês'

  return (
    <div className="painel">
      <section className="grade-indicadores" aria-label="Indicadores do período">
        <Indicador
          rotulo="Faturamento"
          valor={formatarMoeda(relatorio.totalVendido)}
          detalhe={<Comparacao variacao={antes.variacaoTotalPercentual} contexto={contexto} anterior={formatarMoeda(antes.totalVendido)} />}
          tom="marca"
        />
        <Indicador
          rotulo="Vendas"
          valor={relatorio.quantidadeComandas}
          detalhe={<Comparacao variacao={antes.variacaoQuantidadePercentual} contexto={contexto} anterior={String(antes.quantidadeComandas)} />}
        />
        <Indicador
          rotulo="Ticket médio"
          valor={formatarMoeda(relatorio.ticketMedio)}
          detalhe={<Comparacao variacao={antes.variacaoTicketPercentual} contexto={contexto} anterior={formatarMoeda(antes.ticketMedio)} />}
        />
        <IndicadorMargem relatorio={relatorio} />
      </section>
      {relatorio.quantidadeEstornos > 0 && (
        <p className="suave pequeno">
          {relatorio.quantidadeEstornos} venda{relatorio.quantidadeEstornos === 1 ? '' : 's'} estornada
          {relatorio.quantidadeEstornos === 1 ? '' : 's'} ({formatarMoeda(relatorio.totalEstornado)}) fora do faturamento.
        </p>
      )}

      <Secao titulo={`Faturamento por ${nomeTrecho}`}>
        <GraficoTrechos
          atual={relatorio.porTrecho}
          antes={antes.porTrecho}
          agrupamento={relatorio.agrupamento}
          rotuloAtual={descreverPeriodo(relatorio.de, relatorio.ate)}
          rotuloAntes={antes.ateMesmoHorario ? `${periodoAntes} (até este horário)` : periodoAntes}
        />
      </Secao>

      <div className="grade-painel">
        <Secao titulo="Horários de pico" className="grade-largo" acao={<span className="suave pequeno">soma do período</span>}>
          <GraficoHoras hoje={relatorio.porHora} antes={[]} rotuloHoje="Vendas pagas por hora" rotuloAntes="" />
        </Secao>
        <DiasDaSemana relatorio={relatorio} />
      </div>

      <div className="grade-painel">
        <Produtos relatorio={relatorio} />
        <div className="coluna-secoes">
          <ListaComBarras
            titulo="Categorias"
            vazio="Nenhum produto vendido no período."
            linhas={relatorio.categorias.map((c) => ({
              chave: c.categoria,
              nome: c.categoria,
              detalhe: `${c.quantidade} ${c.quantidade === 1 ? 'unidade' : 'unidades'}`,
              total: c.total,
              percentual: c.percentual,
            }))}
          />
          <ListaComBarras
            titulo="Formas de pagamento"
            vazio="Nenhuma venda paga no período."
            linhas={relatorio.porFormaPagamento.map((f) => ({
              chave: f.forma,
              nome: nomeDaForma(f.forma),
              detalhe: `${f.quantidade} pagamento${f.quantidade === 1 ? '' : 's'}`,
              total: f.total,
              // Só para a barra: a parte de cada forma no que foi pago (os valores em R$ vêm da API).
              percentual: relatorio.totalVendido > 0 ? Math.round((f.total / relatorio.totalVendido) * 1000) / 10 : 0,
            }))}
          />
        </div>
      </div>

      {relatorio.sabores.length > 0 && <Sabores relatorio={relatorio} />}
    </div>
  )
}

/** "n/d" para o que não tem custo (RN-RL-04): nunca mostra zero. */
function dinheiroOuNd(valor: number | null): string {
  return valor === null ? 'n/d' : formatarMoeda(valor)
}

function percentualOuNada(valor: number | null): string {
  return valor === null ? '' : `${valor.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%`
}

/** RN-RL-04: margem só com o que tem custo; o que ficou de fora aparece no detalhe. */
function IndicadorMargem({ relatorio }: { relatorio: RelatorioPeriodoDto }) {
  const m = relatorio.margem
  if (m.margemEstimada === null) {
    return (
      <Indicador
        rotulo="Margem estimada"
        valor="n/d"
        detalhe={relatorio.totalVendido > 0 ? 'nenhum produto vendido tem custo cadastrado' : 'sem vendas no período'}
      />
    )
  }
  const detalhe = [
    `${percentualOuNada(m.margemPercentual)} sobre ${formatarMoeda(m.faturamentoComCusto)}`,
    m.faturamentoSemCusto > 0 ? `${formatarMoeda(m.faturamentoSemCusto)} sem custo` : null,
  ]
    .filter(Boolean)
    .join(' · ')
  return <Indicador rotulo="Margem estimada" valor={dinheiroOuNd(m.margemEstimada)} detalhe={detalhe} />
}

function DiasDaSemana({ relatorio }: { relatorio: RelatorioPeriodoDto }) {
  const maior = Math.max(0, ...relatorio.porDiaDaSemana.map((d) => d.mediaPorDia))
  return (
    <Secao titulo="Dias da semana" acao={<span className="suave pequeno">média por dia</span>}>
      <ul className="lista-barras">
        {relatorio.porDiaDaSemana.map((d) => (
          <li key={d.diaDaSemana}>
            <div className="lista-barras-linha">
              <span>
                {nomeDoDiaDaSemana(d.diaDaSemana)}{' '}
                <span className="suave">
                  · {d.dias} {d.dias === 1 ? 'dia' : 'dias'}, {d.quantidade} venda{d.quantidade === 1 ? '' : 's'}
                </span>
              </span>
              <span className="numero" title={`Total: ${formatarMoeda(d.total)}`}>
                <strong>{formatarMoeda(d.mediaPorDia)}</strong>
              </span>
            </div>
            <Barra fracao={maior > 0 ? d.mediaPorDia / maior : 0} tom="neutro" />
          </li>
        ))}
      </ul>
    </Secao>
  )
}

type Ordem = 'valor' | 'quantidade' | 'margem'

const ORDENS: { valor: Ordem; texto: string }[] = [
  { valor: 'valor', texto: 'Por valor' },
  { valor: 'quantidade', texto: 'Por quantidade' },
  { valor: 'margem', texto: 'Por margem' },
]

function Produtos({ relatorio }: { relatorio: RelatorioPeriodoDto }) {
  const [ordem, setOrdem] = useState<Ordem>('valor')
  const [todos, setTodos] = useState(false)
  // A API manda por faturamento; por quantidade ou margem é só reordenar a mesma lista (sem custo vai para o fim).
  const ordenados =
    ordem === 'valor'
      ? relatorio.produtos
      : ordem === 'quantidade'
        ? [...relatorio.produtos].sort((a, b) => b.quantidade - a.quantidade || b.total - a.total)
        : [...relatorio.produtos].sort((a, b) => (b.margem ?? -Infinity) - (a.margem ?? -Infinity) || b.total - a.total)
  const visiveis = todos ? ordenados : ordenados.slice(0, PRODUTOS_NA_LISTA)

  return (
    <Secao
      titulo="Produtos"
      className="grade-largo secao-tabela"
      acao={
        <div className="abas abas-pequenas" role="group" aria-label="Ordenar produtos">
          {ORDENS.map((o) => (
            <button
              key={o.valor}
              type="button"
              className={ordem === o.valor ? 'aba aba-ativa' : 'aba'}
              aria-pressed={ordem === o.valor}
              onClick={() => setOrdem(o.valor)}
            >
              {o.texto}
            </button>
          ))}
        </div>
      }
    >
      {relatorio.produtos.length === 0 ? (
        <p className="suave">Nenhum produto vendido no período.</p>
      ) : (
        <>
          <div className="tabela-caixa">
            <table className="tabela">
              <thead>
                <tr>
                  <th scope="col" className="numero">
                    #
                  </th>
                  <th scope="col">Produto</th>
                  <th scope="col" className="numero">
                    Qtd.
                  </th>
                  <th scope="col" className="numero">
                    Total
                  </th>
                  <th scope="col" className="numero so-largo">
                    Parte
                  </th>
                  <th scope="col" className="numero so-largo">
                    Custo
                  </th>
                  <th scope="col" className="numero">
                    Margem
                  </th>
                </tr>
              </thead>
              <tbody>
                {visiveis.map((p, i) => (
                  <tr key={p.produtoId ?? 'livre'}>
                    <td className="numero suave">{i + 1}º</td>
                    <td>
                      <strong>{p.descricao}</strong>
                      {p.produtoId !== null && <span className="suave bloco pequeno">{p.categoria}</span>}
                    </td>
                    <td className="numero">{p.quantidade}</td>
                    <td className="numero forte">{formatarMoeda(p.total)}</td>
                    <td className="numero so-largo suave">{p.percentual.toLocaleString('pt-BR', { minimumFractionDigits: 1 })}%</td>
                    <td className="numero so-largo suave">{dinheiroOuNd(p.custoEstimado)}</td>
                    <td className="numero" title={p.margemPercentual === null ? 'Sem custo cadastrado' : `${percentualOuNada(p.margemPercentual)} do total`}>
                      {p.margem === null ? <span className="suave">n/d</span> : formatarMoeda(p.margem)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {relatorio.produtos.length > PRODUTOS_NA_LISTA && (
            <div className="rodape-tabela so-tela">
              <span className="suave">
                {todos ? relatorio.produtos.length : PRODUTOS_NA_LISTA} de {relatorio.produtos.length} produtos
              </span>
              <button type="button" className="botao botao-secundario" onClick={() => setTodos(!todos)}>
                {todos ? 'Mostrar menos' : 'Ver todos'}
              </button>
            </div>
          )}
        </>
      )}
    </Secao>
  )
}

interface LinhaComBarra {
  chave: string
  nome: string
  detalhe: string
  total: number
  percentual: number
}

function ListaComBarras({ titulo, linhas, vazio }: { titulo: string; linhas: LinhaComBarra[]; vazio: string }) {
  return (
    <Secao titulo={titulo}>
      {linhas.length === 0 ? (
        <p className="suave">{vazio}</p>
      ) : (
        <ul className="lista-barras">
          {linhas.map((l) => (
            <li key={l.chave}>
              <div className="lista-barras-linha">
                <span>
                  {l.nome} <span className="suave">· {l.detalhe}</span>
                </span>
                <span className="numero">
                  <strong>{formatarMoeda(l.total)}</strong>{' '}
                  <span className="suave">{l.percentual.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%</span>
                </span>
              </div>
              <Barra fracao={l.percentual / 100} />
            </li>
          ))}
        </ul>
      )}
    </Secao>
  )
}

function Sabores({ relatorio }: { relatorio: RelatorioPeriodoDto }) {
  const maior = relatorio.sabores[0]?.unidades ?? 0
  return (
    <Secao titulo="Sabores dos potes" acao={<span className="suave pequeno">potes que saíram</span>}>
      <ol className="ranking">
        {relatorio.sabores.map((s, i) => (
          <li key={s.itemEstoqueId}>
            <span className="ranking-posicao">{i + 1}º</span>
            <div className="ranking-corpo">
              <div className="lista-barras-linha">
                <span>
                  <strong>{s.descricao}</strong>{' '}
                  <span className="suave">
                    · {s.unidades} {s.unidades === 1 ? 'pote' : 'potes'}
                    {s.margem !== null && ` · margem ${formatarMoeda(s.margem)}`}
                  </span>
                </span>
                <strong className="numero">{formatarMoeda(s.total)}</strong>
              </div>
              <Barra fracao={maior > 0 ? s.unidades / maior : 0} tom="neutro" />
            </div>
          </li>
        ))}
      </ol>
    </Secao>
  )
}

/** Uma planilha com as seções do relatório, uma embaixo da outra. Só os números que vieram da API (ADR 018). */
function exportar(r: RelatorioPeriodoDto) {
  const nomeTrecho = r.agrupamento === 'Dia' ? 'Dia' : r.agrupamento === 'Semana' ? 'Semana' : 'Mês'
  const linhas: Celula[][] = [
    ['Período', descreverPeriodo(r.de, r.ate)],
    ['Comparado com', descreverPeriodo(r.anterior.de, r.anterior.ate) + (r.anterior.ateMesmoHorario ? ' (até o mesmo horário)' : '')],
    ['Faturamento (R$)', celulaDinheiro(r.totalVendido), celulaDinheiro(r.anterior.totalVendido)],
    ['Vendas', r.quantidadeComandas, r.anterior.quantidadeComandas],
    ['Ticket médio (R$)', celulaDinheiro(r.ticketMedio), celulaDinheiro(r.anterior.ticketMedio)],
    ['Estornos (R$)', celulaDinheiro(r.totalEstornado)],
    r.margem.margemEstimada === null
      ? ['Margem estimada (R$)', 'n/d (nenhum produto vendido tem custo)']
      : ['Margem estimada (R$)', celulaDinheiro(r.margem.margemEstimada), 'sobre (R$)', celulaDinheiro(r.margem.faturamentoComCusto)],
    ['Faturamento sem custo (R$)', celulaDinheiro(r.margem.faturamentoSemCusto)],
    [],
    [nomeTrecho, 'Total (R$)', 'Vendas', 'Anterior', 'Total anterior (R$)'],
    ...Array.from({ length: Math.max(r.porTrecho.length, r.anterior.porTrecho.length) }, (_, i) => {
      const t = r.porTrecho[i]
      const a = r.anterior.porTrecho[i]
      return [
        t ? descreverTrecho(t, r.agrupamento) : '',
        t ? celulaDinheiro(t.total) : '',
        t?.quantidade,
        a ? descreverTrecho(a, r.agrupamento) : '',
        a ? celulaDinheiro(a.total) : '',
      ]
    }),
    [],
    ['Produto', 'Categoria', 'Quantidade', 'Total (R$)', 'Parte (%)', 'Custo estimado (R$)', 'Margem (R$)', 'Margem (%)'],
    ...r.produtos.map((p) => [
      p.descricao,
      p.categoria,
      p.quantidade,
      celulaDinheiro(p.total),
      celulaDinheiro(p.percentual),
      p.custoEstimado === null ? 'n/d' : celulaDinheiro(p.custoEstimado),
      p.margem === null ? 'n/d' : celulaDinheiro(p.margem),
      p.margemPercentual === null ? 'n/d' : celulaDinheiro(p.margemPercentual),
    ]),
    [],
    ['Categoria', 'Quantidade', 'Total (R$)', 'Parte (%)'],
    ...r.categorias.map((c) => [c.categoria, c.quantidade, celulaDinheiro(c.total), celulaDinheiro(c.percentual)]),
    [],
    ['Dia da semana', 'Dias', 'Vendas', 'Total (R$)', 'Média por dia (R$)'],
    ...r.porDiaDaSemana.map((d) => [nomeDoDiaDaSemana(d.diaDaSemana), d.dias, d.quantidade, celulaDinheiro(d.total), celulaDinheiro(d.mediaPorDia)]),
    [],
    ['Hora', 'Vendas', 'Total (R$)'],
    ...r.porHora.map((h) => [`${h.hora}h`, h.quantidade, celulaDinheiro(h.total)]),
  ]
  if (r.sabores.length > 0) {
    linhas.push(
      [],
      ['Sabor', 'Potes', 'Total (R$)', 'Custo estimado (R$)', 'Margem (R$)'],
      ...r.sabores.map((s) => [
        s.descricao,
        s.unidades,
        celulaDinheiro(s.total),
        s.custoEstimado === null ? 'n/d' : celulaDinheiro(s.custoEstimado),
        s.margem === null ? 'n/d' : celulaDinheiro(s.margem),
      ]),
    )
  }
  baixarCsv(`relatorio-${r.de}-a-${r.ate}.csv`, montarCsv([`Relatório de vendas — ${NOME_LOJA}`], linhas))
}
