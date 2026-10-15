import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { CHAVE_COMANDAS, listarComandas, listarTodasAsComandas, VENDAS_POR_PAGINA } from '../../api/comandas.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { ComandaResumoDto, StatusComanda } from '../../api/tipos.ts'
import { CHAVE_USUARIOS, listarUsuarios } from '../../api/usuarios.ts'
import Consulta from '../../componentes/Consulta.tsx'
import FiltroPeriodo, { type Periodo } from '../../componentes/FiltroPeriodo.tsx'
import { CabecalhoPagina, Selo, type Tom } from '../../componentes/Ui.tsx'
import { baixarCsv, celulaDinheiro, montarCsv } from '../../exportar.ts'
import {
  FORMAS_DE_PAGAMENTO,
  formatarData,
  formatarDia,
  formatarHora,
  formatarMoeda,
  hojeNaLoja,
  nomeDaForma,
  nomeDoStatus,
} from '../../formatacao.ts'
import { NOME_LOJA } from '../../marca.ts'

const FILTROS: { valor: StatusComanda | undefined; texto: string }[] = [
  { valor: undefined, texto: 'Todas' },
  { valor: 'Fechada', texto: 'Pagas' },
  { valor: 'Cancelada', texto: 'Canceladas' },
  { valor: 'Estornada', texto: 'Estornadas' },
]

const TOM_DO_STATUS: Record<string, Tom> = { Fechada: 'bom', Cancelada: 'neutro', Estornada: 'ruim', Aberta: 'atencao' }

function descricaoPeriodo({ de, ate }: Periodo): string {
  return de === ate ? formatarDia(de) : `${formatarDia(de)} a ${formatarDia(ate)}`
}

function pagamento(v: ComandaResumoDto): string {
  return v.formasPagamento.map(nomeDaForma).join(' + ') || '—'
}

export default function ListaVendas() {
  const navegar = useNavigate()
  const [periodo, setPeriodo] = useState<Periodo>(() => ({ de: hojeNaLoja(), ate: hojeNaLoja() }))
  const [status, setStatus] = useState<StatusComanda | undefined>(undefined)
  const [atendente, setAtendente] = useState('')
  const [forma, setForma] = useState('')
  const [pagina, setPagina] = useState(1)
  const [exportando, setExportando] = useState(false)
  const [erroExportar, setErroExportar] = useState<string | null>(null)

  const filtro = { de: periodo.de, ate: periodo.ate, status, atendente: atendente || undefined, forma: forma || undefined }
  const vendas = useQuery({
    queryKey: [...CHAVE_COMANDAS, 'lista', filtro, pagina],
    queryFn: () => listarComandas({ ...filtro, pagina }),
    placeholderData: keepPreviousData,
  })
  // Quem vendeu: ativos primeiro (quem já saiu continua aparecendo nas vendas antigas).
  const usuarios = useQuery({ queryKey: CHAVE_USUARIOS, queryFn: listarUsuarios })
  const atendentes = [...(usuarios.data ?? [])].sort((a, b) => Number(b.ativo) - Number(a.ativo) || a.nome.localeCompare(b.nome, 'pt-BR'))
  const nomeAtendente = atendentes.find((u) => u.id === atendente)?.nome
  const descricaoFiltros = [
    status ? FILTROS.find((f) => f.valor === status)?.texto : null,
    nomeAtendente ? `vendidas por ${nomeAtendente}` : null,
    forma ? `com ${nomeDaForma(forma).toLowerCase()}` : null,
  ]
    .filter(Boolean)
    .join(' · ')

  function trocarPeriodo(novo: Periodo) {
    setPeriodo(novo)
    setPagina(1)
  }

  function trocarStatus(novo: StatusComanda | undefined) {
    setStatus(novo)
    setPagina(1)
  }

  function trocarAtendente(novo: string) {
    setAtendente(novo)
    setPagina(1)
  }

  function trocarForma(nova: string) {
    setForma(nova)
    setPagina(1)
  }

  async function exportar() {
    setExportando(true)
    setErroExportar(null)
    try {
      const todas = await listarTodasAsComandas(filtro)
      const csv = montarCsv(
        ['Nº', 'Data', 'Hora', 'Tipo', 'Atendente', 'Pagamento', 'Situação', 'Total (R$)', 'Chegou após fechar o caixa'],
        todas.map((v) => {
          const quando = v.fechadaEm ?? v.criadaEm
          return [
            v.numero,
            formatarData(quando),
            formatarHora(quando),
            v.tipo === 'Delivery' ? 'Delivery' : 'Balcão',
            v.atendenteNome,
            pagamento(v),
            nomeDoStatus(v.status),
            celulaDinheiro(v.total),
            v.recebidaAposFechamentoCaixa ? 'Sim' : '',
          ]
        }),
      )
      baixarCsv(`vendas-${periodo.de}-a-${periodo.ate}.csv`, csv)
    } catch (erro) {
      setErroExportar(mensagemDoErro(erro))
    } finally {
      setExportando(false)
    }
  }

  const total = vendas.data?.totalItens ?? 0

  return (
    <>
      <p className="so-impressao">
        {NOME_LOJA} · Vendas de {descricaoPeriodo(periodo)}
        {descricaoFiltros ? ` · ${descricaoFiltros}` : ''}
      </p>
      <CabecalhoPagina
        titulo="Vendas"
        subtitulo={
          vendas.data
            ? `${total} venda${total === 1 ? '' : 's'} em ${descricaoPeriodo(periodo)}${descricaoFiltros ? ` · ${descricaoFiltros}` : ''}`
            : `Vendas de ${descricaoPeriodo(periodo)}`
        }
        acoes={
          <>
            <button type="button" className="botao botao-secundario" onClick={() => window.print()}>
              Imprimir
            </button>
            <button type="button" className="botao botao-secundario" onClick={() => void exportar()} disabled={exportando || total === 0}>
              {exportando ? 'Exportando…' : 'Exportar Excel'}
            </button>
          </>
        }
      />

      <div className="filtros">
        <FiltroPeriodo periodo={periodo} aoMudar={trocarPeriodo} />
        <div className="abas" role="group" aria-label="Situação">
          {FILTROS.map((f) => (
            <button
              key={f.texto}
              type="button"
              className={f.valor === status ? 'aba aba-ativa' : 'aba'}
              aria-pressed={f.valor === status}
              onClick={() => trocarStatus(f.valor)}
            >
              {f.texto}
            </button>
          ))}
        </div>
        {atendentes.length > 0 && (
          <label className="campo-inline">
            Atendente
            <select value={atendente} onChange={(e) => trocarAtendente(e.target.value)}>
              <option value="">Todos</option>
              {atendentes.map((u) => (
                <option key={u.id} value={u.id}>
                  {u.nome}
                  {u.ativo ? '' : ' (desativado)'}
                </option>
              ))}
            </select>
          </label>
        )}
        <label className="campo-inline">
          Pagamento
          <select value={forma} onChange={(e) => trocarForma(e.target.value)}>
            <option value="">Todas as formas</option>
            {FORMAS_DE_PAGAMENTO.map((f) => (
              <option key={f} value={f}>
                {nomeDaForma(f)}
              </option>
            ))}
          </select>
        </label>
      </div>

      {erroExportar && <p className="alerta alerta-erro">Não foi possível exportar: {erroExportar}</p>}

      <Consulta consulta={vendas}>
        {(resultado) => {
          const paginas = Math.max(1, Math.ceil(resultado.totalItens / VENDAS_POR_PAGINA))
          const primeiro = (pagina - 1) * VENDAS_POR_PAGINA + 1
          if (resultado.itens.length === 0) {
            return (
              <p className="secao suave">
                Nenhuma venda em {descricaoPeriodo(periodo)}
                {descricaoFiltros ? ` com esses filtros (${descricaoFiltros})` : ''}.
              </p>
            )
          }
          return (
            <section className="secao secao-tabela">
              <div className="tabela-caixa">
                <table className="tabela">
                  <thead>
                    <tr>
                      <th scope="col">Nº</th>
                      <th scope="col" className="so-largo">
                        Data
                      </th>
                      <th scope="col">Hora</th>
                      <th scope="col" className="so-largo">
                        Tipo
                      </th>
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
                    {resultado.itens.map((v) => {
                      const quando = v.fechadaEm ?? v.criadaEm
                      return (
                        <tr key={v.id} className="linha-clicavel" onClick={() => navegar(`/vendas/${v.id}`)}>
                          <td>
                            <Link to={`/vendas/${v.id}`} className="link-linha">
                              {v.numero}
                            </Link>
                          </td>
                          <td className="suave so-largo">{formatarData(quando)}</td>
                          <td className="suave">{formatarHora(quando)}</td>
                          <td className="so-largo">{v.tipo === 'Delivery' ? 'Delivery' : 'Balcão'}</td>
                          <td className="so-largo">{v.atendenteNome}</td>
                          <td className="so-largo">{pagamento(v)}</td>
                          <td>
                            <Selo tom={TOM_DO_STATUS[v.status] ?? 'neutro'}>{nomeDoStatus(v.status)}</Selo>
                            {v.recebidaAposFechamentoCaixa && (
                              <>
                                {' '}
                                <Selo tom="atencao">Após fechar o caixa</Selo>
                              </>
                            )}
                          </td>
                          <td className={v.status === 'Fechada' ? 'numero forte' : 'numero riscado'}>
                            {formatarMoeda(v.total)}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
              <div className="rodape-tabela so-tela">
                <span className="suave">
                  Mostrando {primeiro}–{primeiro + resultado.itens.length - 1} de {resultado.totalItens}
                </span>
                {paginas > 1 && (
                  <div className="paginacao-botoes">
                    <button type="button" className="botao botao-secundario" disabled={pagina <= 1} onClick={() => setPagina(pagina - 1)}>
                      Anterior
                    </button>
                    <span className="suave">
                      Página {pagina} de {paginas}
                    </span>
                    <button
                      type="button"
                      className="botao botao-secundario"
                      disabled={pagina >= paginas}
                      onClick={() => setPagina(pagina + 1)}
                    >
                      Próxima
                    </button>
                  </div>
                )}
              </div>
            </section>
          )
        }}
      </Consulta>
    </>
  )
}
