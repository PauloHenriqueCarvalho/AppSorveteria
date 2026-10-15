import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { obterClimaVendas } from '../../api/clima.ts'
import { CHAVE_RELATORIOS } from '../../api/relatorios.ts'
import type { ClimaVendasDto, MediaDiasDto } from '../../api/tipos.ts'
import { descreverDia, descreverOrigem, formatarChance, formatarChuva, formatarTemperatura, linhasDaPlanilha, POUCOS_DIAS, simboloDoClima } from '../../clima.ts'
import Consulta from '../../componentes/Consulta.tsx'
import FiltroPeriodo, { type Atalho, type Periodo } from '../../componentes/FiltroPeriodo.tsx'
import GraficoClimaVendas from '../../componentes/GraficoClimaVendas.tsx'
import { Barra, CabecalhoPagina, Secao, Selo } from '../../componentes/Ui.tsx'
import { baixarCsv, montarCsv } from '../../exportar.ts'
import { formatarDia, formatarMoeda, hojeNaLoja, inicioDoMes, somarDias } from '../../formatacao.ts'
import { NOME_LOJA } from '../../marca.ts'
import { descreverPeriodo, fimDoMesAnterior, inicioDoMesAntes } from '../../relatorios.ts'

/** RN-CL-06: até um ano por consulta. */
const MAXIMO_DIAS = 366

const ATALHOS: Atalho[] = [
  { texto: '7 dias', periodo: (hoje) => ({ de: somarDias(hoje, -6), ate: hoje }) },
  { texto: '30 dias', periodo: (hoje) => ({ de: somarDias(hoje, -29), ate: hoje }) },
  { texto: 'Este mês', periodo: (hoje) => ({ de: inicioDoMes(hoje), ate: hoje }) },
  { texto: 'Mês passado', periodo: (hoje) => ({ de: inicioDoMesAntes(hoje, 1), ate: fimDoMesAnterior(hoje) }) },
  { texto: '3 meses', periodo: (hoje) => ({ de: inicioDoMesAntes(hoje, 2), ate: hoje }) },
  { texto: 'Este ano', periodo: (hoje) => ({ de: `${hoje.slice(0, 4)}-01-01`, ate: hoje }) },
]

function diasNoPeriodo({ de, ate }: Periodo): number {
  return Math.round((Date.parse(`${ate}T12:00:00Z`) - Date.parse(`${de}T12:00:00Z`)) / 86_400_000) + 1
}

/**
 * /clima-vendas — base para vender mais (docs/02, seção 9b): o clima de cada dia ao lado das vendas, as médias por faixa de
 * temperatura e com/sem chuva e a previsão dos próximos dias. Tudo calculado pela API; a tela só mostra.
 */
export default function ClimaVendas() {
  const [periodo, setPeriodo] = useState<Periodo>(() => ({ de: somarDias(hojeNaLoja(), -29), ate: hojeNaLoja() }))
  const longoDemais = diasNoPeriodo(periodo) > MAXIMO_DIAS

  const consulta = useQuery({
    queryKey: [...CHAVE_RELATORIOS, 'clima-vendas', periodo.de, periodo.ate],
    queryFn: () => obterClimaVendas(periodo.de, periodo.ate),
    placeholderData: keepPreviousData,
    enabled: !longoDemais,
  })
  const r = consulta.data

  return (
    <>
      <p className="so-impressao">
        {NOME_LOJA} · Clima × vendas de {descreverPeriodo(periodo.de, periodo.ate)}
      </p>
      <CabecalhoPagina
        titulo="Clima × vendas"
        subtitulo={
          r?.local
            ? `Vendas de ${descreverPeriodo(r.de, r.ate)} com o clima de ${r.local.nome}`
            : `Vendas de ${descreverPeriodo(periodo.de, periodo.ate)}`
        }
        acoes={
          <>
            <button type="button" className="botao botao-secundario" onClick={() => window.print()}>
              Imprimir
            </button>
            <button type="button" className="botao botao-secundario" onClick={() => r && exportar(r)} disabled={!r}>
              Exportar Excel
            </button>
          </>
        }
      />

      <div className="filtros">
        <FiltroPeriodo periodo={periodo} aoMudar={setPeriodo} atalhos={ATALHOS} />
      </div>

      {longoDemais ? (
        <p className="alerta alerta-aviso">Escolha um período de até {MAXIMO_DIAS} dias (um ano).</p>
      ) : (
        <Consulta consulta={consulta}>{(dados) => <Conteudo r={dados} />}</Consulta>
      )}
    </>
  )
}

function Conteudo({ r }: { r: ClimaVendasDto }) {
  const semClimaNenhum = r.diasSemClima === r.dias.length
  const nasMedias = r.dias.filter((d) => d.entraNasMedias).length

  return (
    <div className="painel">
      {!r.local ? (
        <p className="alerta alerta-aviso">
          Para guardar o clima de cada dia, informe a cidade da loja em <Link to="/configuracoes">Configurações</Link>. As vendas já
          aparecem abaixo; o clima entra assim que o local for confirmado (o histórico desde a primeira venda vem junto).
        </p>
      ) : semClimaNenhum ? (
        <p className="alerta alerta-aviso">
          O clima deste período ainda não foi buscado. Em <Link to="/configuracoes">Configurações</Link>, toque em “Atualizar agora”.
        </p>
      ) : (
        <p className="suave">
          Cada dia fica guardado com o clima e as vendas. Com uns 3 meses de dados, o sistema vai conseguir estimar quanto a loja
          vende (e quanto pedir) pela previsão do tempo.
          {r.diasSemClima > 0 && ` ${r.diasSemClima} ${r.diasSemClima === 1 ? 'dia' : 'dias'} do período ainda sem clima.`}
        </p>
      )}

      {r.proximosDias.length > 0 && <ProximosDias r={r} />}

      <Secao
        titulo="Temperatura × faturamento"
        acao={
          <span className="suave pequeno">
            {nasMedias} {nasMedias === 1 ? 'dia' : 'dias'} com clima e venda
          </span>
        }
      >
        <GraficoClimaVendas dias={r.dias} />
      </Secao>

      <Secao titulo="Médias por dia" className="secao-tabela" acao={<span className="suave pequeno">pela máxima do dia e pela chuva</span>}>
        <TabelaMedias
          grupos={[
            {
              titulo: 'Temperatura máxima',
              linhas: r.porTemperatura.map((f) => ({ chave: f.faixa, nome: f.faixa, uteis: f.diasUteis, fds: f.fimDeSemanaEFeriado })),
            },
            {
              titulo: 'Chuva (1 mm ou mais no dia)',
              linhas: r.porChuva.map((c) => ({
                chave: c.situacao,
                nome: `${c.comChuva ? '🌧️' : '☀️'} ${c.situacao}`,
                uteis: c.diasUteis,
                fds: c.fimDeSemanaEFeriado,
              })),
            },
          ]}
        />
        <p className="suave pequeno">
          Ficam fora das médias: dia sem venda (loja fechada), dia sem clima e hoje (ainda aberto). Ponto facultativo conta como
          feriado. Média com menos de {POUCOS_DIAS} dias ainda diz pouco.
        </p>
      </Secao>

      <DiaADia r={r} />

      <p className="suave pequeno">
        <a href={r.atribuicaoLink} target="_blank" rel="noreferrer">
          {r.atribuicao}
        </a>
      </p>
    </div>
  )
}

function ProximosDias({ r }: { r: ClimaVendasDto }) {
  return (
    <Secao titulo="Próximos dias" acao={<span className="suave pequeno">previsão de {formatarDia(r.proximosDias[0].feitaNoDia)}</span>}>
      <ul className="previsao-dias">
        {r.proximosDias.map((p) => (
          <li key={p.data} className="previsao-dia" title={p.condicao ?? undefined}>
            <span className="previsao-data">{descreverDia(p.data)}</span>
            <span className="previsao-simbolo" aria-hidden="true">
              {simboloDoClima(p.icone)}
            </span>
            <strong>{formatarTemperatura(p.tempMax)}</strong>
            <span className="suave pequeno">mín. {formatarTemperatura(p.tempMin)}</span>
            {p.chanceChuvaPct !== null && p.chanceChuvaPct > 0 && <span className="suave pequeno">chuva {formatarChance(p.chanceChuvaPct)}</span>}
            {p.condicao && <span className="escondido">{p.condicao}</span>}
          </li>
        ))}
      </ul>
    </Secao>
  )
}

interface LinhaMedia {
  chave: string
  nome: string
  uteis: MediaDiasDto
  fds: MediaDiasDto
}

/** Médias que vieram da API, lado a lado: dias úteis e fins de semana e feriados (RN-CL-06). */
function TabelaMedias({ grupos }: { grupos: { titulo: string; linhas: LinhaMedia[] }[] }) {
  const maior = Math.max(0, ...grupos.flatMap((g) => g.linhas.flatMap((l) => [l.uteis.faturamentoMedio ?? 0, l.fds.faturamentoMedio ?? 0])))
  return (
    <div className="tabela-caixa">
      <table className="tabela tabela-medias">
        <thead>
          <tr>
            <th scope="col" rowSpan={2}>
              <span className="escondido">Dia</span>
            </th>
            <th scope="colgroup" colSpan={2} className="numero">
              Dias úteis
            </th>
            <th scope="colgroup" colSpan={2} className="numero">
              Fins de semana e feriados
            </th>
          </tr>
          <tr>
            <th scope="col" className="numero">
              Faturamento
            </th>
            <th scope="col" className="numero">
              Vendas
            </th>
            <th scope="col" className="numero">
              Faturamento
            </th>
            <th scope="col" className="numero">
              Vendas
            </th>
          </tr>
        </thead>
        {grupos.map((g) => (
          <tbody key={g.titulo}>
            <tr className="tabela-medias-grupo">
              <th scope="colgroup" colSpan={5}>
                {g.titulo}
              </th>
            </tr>
            {g.linhas.map((l) => (
              <tr key={l.chave}>
                <th scope="row">{l.nome}</th>
                <CelulasMedia media={l.uteis} maior={maior} />
                <CelulasMedia media={l.fds} maior={maior} />
              </tr>
            ))}
          </tbody>
        ))}
      </table>
    </div>
  )
}

function CelulasMedia({ media, maior }: { media: MediaDiasDto; maior: number }) {
  if (media.dias === 0 || media.faturamentoMedio === null) {
    return (
      <>
        <td className="numero suave">—</td>
        <td className="numero suave">—</td>
      </>
    )
  }
  return (
    <>
      <td className="numero">
        <strong>{formatarMoeda(media.faturamentoMedio)}</strong>
        <span className="suave bloco pequeno">
          {media.dias} {media.dias === 1 ? 'dia' : 'dias'}
          {media.dias < POUCOS_DIAS && ' · poucos dados'}
        </span>
        <Barra fracao={maior > 0 ? media.faturamentoMedio / maior : 0} tom="neutro" />
      </td>
      <td className="numero">{media.vendasMedias?.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}</td>
    </>
  )
}

function DiaADia({ r }: { r: ClimaVendasDto }) {
  const [todos, setTodos] = useState(false)
  const recentes = [...r.dias].reverse()
  const visiveis = todos ? recentes : recentes.slice(0, 31)

  return (
    <Secao titulo="Dia a dia" className="secao-tabela" acao={<span className="suave pequeno">mais recente primeiro</span>}>
      <div className="tabela-caixa">
        <table className="tabela">
          <thead>
            <tr>
              <th scope="col">Dia</th>
              <th scope="col">Clima</th>
              <th scope="col" className="so-largo">
                Chuva
              </th>
              <th scope="col" className="numero">
                Faturamento
              </th>
              <th scope="col" className="numero">
                Vendas
              </th>
              <th scope="col" className="numero so-largo">
                Ticket médio
              </th>
              <th scope="col" className="numero so-largo" title="Avisos urgentes que o balcão criou no dia (faltou produto)">
                Urgentes
              </th>
            </tr>
          </thead>
          <tbody>
            {visiveis.map((d) => (
              <tr key={d.data} className={d.entraNasMedias ? undefined : 'linha-apagada'}>
                <td>
                  <strong>{descreverDia(d.data)}</strong>
                  {d.feriado && (
                    <span className="bloco">
                      <Selo tom="marca">{d.feriadoFacultativo ? `${d.feriado} (facultativo)` : d.feriado}</Selo>
                    </span>
                  )}
                </td>
                <td title={d.clima ? descreverOrigem(d.clima.origem, d.clima.definitivo) : undefined}>
                  {d.clima ? (
                    <>
                      <span aria-hidden="true">{simboloDoClima(d.clima.icone)} </span>
                      {formatarTemperatura(d.clima.tempMax)} <span className="suave">/ {formatarTemperatura(d.clima.tempMin)}</span>
                      {d.clima.condicao && <span className="suave bloco pequeno">{d.clima.condicao}</span>}
                    </>
                  ) : (
                    <span className="suave">sem clima</span>
                  )}
                </td>
                <td className="so-largo">{d.clima ? formatarChuva(d.clima.chuvaMm) : '—'}</td>
                <td className="numero forte">{formatarMoeda(d.faturamento)}</td>
                <td className="numero">{d.vendas}</td>
                <td className="numero so-largo">{d.vendas > 0 ? formatarMoeda(d.ticketMedio) : '—'}</td>
                <td className="numero so-largo">{d.avisosUrgentes > 0 ? <Selo tom="atencao">{d.avisosUrgentes}</Selo> : ''}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {recentes.length > 31 && (
        <div className="rodape-tabela so-tela">
          <span className="suave">
            {visiveis.length} de {recentes.length} dias
          </span>
          <button type="button" className="botao botao-secundario" onClick={() => setTodos(!todos)}>
            {todos ? 'Mostrar menos' : 'Ver todos'}
          </button>
        </div>
      )}
    </Secao>
  )
}

/** Um dia por linha, com o clima, as vendas e as categorias: a base da estimativa de vendas (só números da API). */
function exportar(r: ClimaVendasDto) {
  const linhas = [...linhasDaPlanilha(r), [], [`${r.atribuicao} (visualcrossing.com)`]]
  baixarCsv(`clima-vendas-${r.de}-a-${r.ate}.csv`, montarCsv([`Clima × vendas — ${NOME_LOJA} — ${descreverPeriodo(r.de, r.ate)}`], linhas))
}
