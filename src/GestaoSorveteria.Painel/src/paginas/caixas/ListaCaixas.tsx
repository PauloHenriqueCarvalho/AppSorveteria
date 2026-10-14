import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { CHAVE_CAIXAS, listarCaixas } from '../../api/caixas.ts'
import type { CaixaResumoDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { ResultadoCaixa } from '../../componentes/ConferenciaCaixa.tsx'
import FiltroPeriodo, { type Periodo } from '../../componentes/FiltroPeriodo.tsx'
import { CabecalhoPagina, Selo } from '../../componentes/Ui.tsx'
import { baixarCsv, celulaDinheiro, montarCsv } from '../../exportar.ts'
import { formatarData, formatarDia, formatarHora, formatarMoeda, hojeNaLoja, somarDias } from '../../formatacao.ts'
import { NOME_LOJA } from '../../marca.ts'

function descricaoPeriodo({ de, ate }: Periodo): string {
  return de === ate ? formatarDia(de) : `${formatarDia(de)} a ${formatarDia(ate)}`
}

/** "Bateu", "Faltaram R$ 12,50"… em texto, para o Excel. Só lê a diferença que a API calculou. */
function resultadoEmTexto(c: CaixaResumoDto): string {
  if (c.status === 'Aberto' || c.diferenca === null) {
    return 'Aberto'
  }
  if (c.diferenca === 0) {
    return 'Bateu'
  }
  return c.diferenca < 0 ? 'Faltou' : 'Sobrou'
}

export default function ListaCaixas() {
  const navegar = useNavigate()
  const [periodo, setPeriodo] = useState<Periodo>(() => ({ de: somarDias(hojeNaLoja(), -29), ate: hojeNaLoja() }))
  const caixas = useQuery({
    queryKey: [...CHAVE_CAIXAS, 'lista', periodo.de, periodo.ate],
    queryFn: () => listarCaixas(periodo.de, periodo.ate),
  })

  function exportar(lista: CaixaResumoDto[]) {
    const csv = montarCsv(
      [
        'Data',
        'Abertura',
        'Fechamento',
        'Aberto por',
        'Fechado por',
        'Vendas (R$)',
        'Comandas',
        'Fundo de troco (R$)',
        'Vendas em dinheiro (R$)',
        'Esperado na gaveta (R$)',
        'Contado (R$)',
        'Diferença (R$)',
        'Resultado',
        'Observação',
      ],
      lista.map((c) => [
        formatarData(c.abertoEm),
        formatarHora(c.abertoEm),
        c.fechadoEm ? formatarHora(c.fechadoEm) : '',
        c.abertoPorNome,
        c.fechadoPorNome ?? '',
        celulaDinheiro(c.totalVendas),
        c.quantidadeComandas,
        celulaDinheiro(c.fundoTroco),
        celulaDinheiro(c.totalVendasDinheiro),
        celulaDinheiro(c.valorEsperado),
        celulaDinheiro(c.valorContado),
        celulaDinheiro(c.diferenca),
        resultadoEmTexto(c),
        [
          c.fechamentoForcado ? 'Fechado pelo painel' : '',
          c.divergenciaSincronizacao ? 'Tablet e servidor não bateram' : '',
          c.vendasRecebidasAposFechamento > 0 ? `${c.vendasRecebidasAposFechamento} venda(s) após fechar` : '',
        ]
          .filter(Boolean)
          .join(' · '),
      ]),
    )
    baixarCsv(`caixas-${periodo.de}-a-${periodo.ate}.csv`, csv)
  }

  return (
    <>
      <p className="so-impressao">
        {NOME_LOJA} · Caixas de {descricaoPeriodo(periodo)}
      </p>
      <CabecalhoPagina
        titulo="Caixas"
        subtitulo={
          caixas.data
            ? `${caixas.data.length} caixa${caixas.data.length === 1 ? '' : 's'} em ${descricaoPeriodo(periodo)}`
            : `Caixas de ${descricaoPeriodo(periodo)}`
        }
        acoes={
          <>
            <button type="button" className="botao botao-secundario" onClick={() => window.print()}>
              Imprimir
            </button>
            <button
              type="button"
              className="botao botao-secundario"
              disabled={!caixas.data || caixas.data.length === 0}
              onClick={() => caixas.data && exportar(caixas.data)}
            >
              Exportar Excel
            </button>
          </>
        }
      />

      <div className="filtros">
        <FiltroPeriodo periodo={periodo} aoMudar={setPeriodo} />
      </div>

      <Consulta consulta={caixas}>
        {(lista) =>
          lista.length === 0 ? (
            <p className="secao suave">Nenhum caixa em {descricaoPeriodo(periodo)}.</p>
          ) : (
            <section className="secao secao-tabela">
              <div className="tabela-caixa">
                <table className="tabela">
                  <thead>
                    <tr>
                      <th scope="col">Data</th>
                      <th scope="col">Horário</th>
                      <th scope="col" className="so-largo">
                        Aberto por
                      </th>
                      <th scope="col" className="numero">
                        Vendas
                      </th>
                      <th scope="col" className="numero so-largo">
                        Esperado
                      </th>
                      <th scope="col" className="numero so-largo">
                        Contado
                      </th>
                      <th scope="col">Resultado</th>
                    </tr>
                  </thead>
                  <tbody>
                    {lista.map((c) => (
                      <tr key={c.id} className="linha-clicavel" onClick={() => navegar(`/caixas/${c.id}`)}>
                        <td>
                          <Link to={`/caixas/${c.id}`} className="link-linha">
                            {formatarData(c.abertoEm)}
                          </Link>
                        </td>
                        <td className="suave">
                          {formatarHora(c.abertoEm)}
                          {c.fechadoEm ? ` – ${formatarHora(c.fechadoEm)}` : ' – aberto'}
                        </td>
                        <td className="so-largo">{c.abertoPorNome}</td>
                        <td className="numero forte">
                          {formatarMoeda(c.totalVendas)}
                          <small className="suave bloco">
                            {c.quantidadeComandas} comanda{c.quantidadeComandas === 1 ? '' : 's'}
                          </small>
                        </td>
                        <td className="numero so-largo">{c.valorEsperado === null ? '—' : formatarMoeda(c.valorEsperado)}</td>
                        <td className="numero so-largo">{c.valorContado === null ? '—' : formatarMoeda(c.valorContado)}</td>
                        <td>
                          <div className="selos">
                            <ResultadoCaixa caixa={c} />
                            {(c.divergenciaSincronizacao || c.vendasRecebidasAposFechamento > 0) && (
                              <Selo tom="atencao">Conferir</Selo>
                            )}
                            {c.fechamentoForcado && <Selo>Fechado pelo painel</Selo>}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )
        }
      </Consulta>
    </>
  )
}
