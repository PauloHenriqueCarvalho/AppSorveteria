import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import type { CatalogoEstoqueResponse, GrupoEstoqueDto, ItemEstoqueDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import Icone from '../../componentes/Icone.tsx'
import { CabecalhoPagina, Selo, type Tom } from '../../componentes/Ui.tsx'
import { COLUNAS_CSV, descreverEmbalagem, nomeDoModo, quantidadeParaCampo } from '../../estoque.ts'
import { baixarCsv, celulaDinheiro, montarCsv } from '../../exportar.ts'
import { formatarMoeda, hojeNaLoja } from '../../formatacao.ts'
import { useCatalogoEstoque } from './useCatalogoEstoque.ts'

type Situacao = 'ativos' | 'inativos' | 'todos'

const SITUACOES: { valor: Situacao; texto: string }[] = [
  { valor: 'ativos', texto: 'Ativos' },
  { valor: 'inativos', texto: 'Desativados' },
  { valor: 'todos', texto: 'Todos' },
]

const TOM_DO_MODO: Record<string, Tom> = { Contagem: 'marca', SaldoPelaVenda: 'atencao', SoAviso: 'neutro' }

/** /estoque — catálogo do que a loja compra, agrupado como na planilha da dona. */
export default function CatalogoEstoque() {
  const catalogo = useCatalogoEstoque()
  return (
    <Consulta consulta={catalogo}>
      {(dados) => (dados.itens.length === 0 && dados.grupos.length === 0 ? <CatalogoVazio /> : <Tabela dados={dados} />)}
    </Consulta>
  )
}

function CatalogoVazio() {
  return (
    <>
      <CabecalhoPagina titulo="Catálogo de estoque" subtitulo="O que a loja compra: sorvetes da fábrica, copos, casquinhas e insumos" />
      <section className="secao vazio">
        <Icone nome="estoque" tamanho={36} />
        <h2>Comece pela planilha</h2>
        <p className="suave">
          A planilha de contagem já foi transformada em arquivo. Importe uma vez e todos os sabores aparecem aqui, separados por grupo e com o
          custo de cada caixa. Copos, casquinhas e insumos você cadastra depois, com o fornecedor de cada um.
        </p>
        <div className="acoes acoes-inicio">
          <Link to="/estoque/importar" className="botao">
            Importar planilha
          </Link>
          <Link to="/estoque/grupos/novo" className="botao botao-secundario">
            Criar grupo à mão
          </Link>
        </div>
      </section>
    </>
  )
}

function Tabela({ dados }: { dados: CatalogoEstoqueResponse }) {
  const navegar = useNavigate()
  const [busca, setBusca] = useState('')
  const [grupoId, setGrupoId] = useState('')
  const [fornecedorId, setFornecedorId] = useState('')
  const [situacao, setSituacao] = useState<Situacao>('ativos')

  const termo = busca.trim().toLocaleLowerCase('pt-BR')
  const visiveis = dados.itens.filter(
    (i) =>
      (situacao === 'todos' || i.ativo === (situacao === 'ativos')) &&
      (!grupoId || i.grupoId === grupoId) &&
      (!fornecedorId || i.fornecedorId === fornecedorId) &&
      (!termo || i.nome.toLocaleLowerCase('pt-BR').includes(termo)),
  )
  // Grupos na ordem da planilha; grupo sem item visível some (a não ser que ele seja o filtro).
  const secoes = dados.grupos
    .map((grupo) => ({ grupo, itens: visiveis.filter((i) => i.grupoId === grupo.id) }))
    .filter((s) => s.itens.length > 0 || (s.grupo.id === grupoId && !termo))

  const ativos = dados.itens.filter((i) => i.ativo).length

  function exportar() {
    // Mesmo formato da importação: dá para usar como modelo para cadastrar itens novos em lote.
    const csv = montarCsv(
      [...COLUNAS_CSV, 'ativo'],
      visiveis.map((i) => [
        i.grupoNome,
        i.nome,
        i.unidade,
        i.unidadesPorEmbalagem,
        celulaDinheiro(i.custoEmbalagem),
        i.fornecedorNome,
        i.ativo ? 'sim' : 'não',
      ]),
    )
    baixarCsv(`catalogo-estoque-${hojeNaLoja()}.csv`, csv)
  }

  return (
    <>
      <CabecalhoPagina
        titulo="Catálogo de estoque"
        subtitulo={`${ativos} ${ativos === 1 ? 'item ativo' : 'itens ativos'} em ${dados.grupos.length} ${
          dados.grupos.length === 1 ? 'grupo' : 'grupos'
        } · ${dados.fornecedores.length} ${dados.fornecedores.length === 1 ? 'fornecedor' : 'fornecedores'}`}
        acoes={
          <>
            <button type="button" className="botao botao-secundario" onClick={exportar} disabled={visiveis.length === 0}>
              Exportar Excel
            </button>
            <Link to="/estoque/importar" className="botao botao-secundario">
              Importar planilha
            </Link>
            <Link to="/estoque/grupos/novo" className="botao botao-secundario">
              Novo grupo
            </Link>
            <Link to="/estoque/itens/novo" className="botao">
              Novo item
            </Link>
          </>
        }
      />

      <div className="filtros">
        <input
          type="search"
          className="busca"
          placeholder="Buscar sabor ou item"
          aria-label="Buscar item"
          value={busca}
          onChange={(e) => setBusca(e.target.value)}
        />
        <label className="campo-inline">
          Grupo
          <select className="data-compacta" value={grupoId} onChange={(e) => setGrupoId(e.target.value)}>
            <option value="">Todos os grupos</option>
            {dados.grupos.map((g) => (
              <option key={g.id} value={g.id}>
                {g.nome}
              </option>
            ))}
          </select>
        </label>
        {dados.fornecedores.length > 1 && (
          <label className="campo-inline">
            Fornecedor
            <select className="data-compacta" value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}>
              <option value="">Todos</option>
              {dados.fornecedores.map((f) => (
                <option key={f.id} value={f.id}>
                  {f.nome}
                </option>
              ))}
            </select>
          </label>
        )}
        <div className="abas" role="group" aria-label="Situação">
          {SITUACOES.map((s) => (
            <button
              key={s.valor}
              type="button"
              className={s.valor === situacao ? 'aba aba-ativa' : 'aba'}
              aria-pressed={s.valor === situacao}
              onClick={() => setSituacao(s.valor)}
            >
              {s.texto}
            </button>
          ))}
        </div>
      </div>

      {secoes.length === 0 ? (
        <p className="secao suave">Nenhum item encontrado com esses filtros.</p>
      ) : (
        <section className="secao secao-tabela">
          <div className="tabela-caixa">
            <table className="tabela">
              <thead>
                <tr>
                  <th scope="col">Item</th>
                  <th scope="col" className="so-largo">
                    Fornecedor
                  </th>
                  <th scope="col">Embalagem</th>
                  <th scope="col" className="numero">
                    Custo
                  </th>
                  <th scope="col" className="numero so-largo">
                    Mínimo
                  </th>
                  <th scope="col" className="numero so-largo">
                    Alvo
                  </th>
                </tr>
              </thead>
              {secoes.map(({ grupo, itens }) => (
                <tbody key={grupo.id}>
                  <CabecalhoGrupo grupo={grupo} quantidade={itens.length} />
                  {itens.length === 0 ? (
                    <tr>
                      <td colSpan={6} className="suave">
                        Nenhum item neste grupo ainda.
                      </td>
                    </tr>
                  ) : (
                    itens.map((item) => <LinhaItem key={item.id} item={item} aoAbrir={() => navegar(`/estoque/itens/${item.id}`)} />)
                  )}
                </tbody>
              ))}
            </table>
          </div>
          <div className="rodape-tabela">
            <span className="suave">
              {visiveis.length} de {dados.itens.length} {dados.itens.length === 1 ? 'item' : 'itens'}
            </span>
          </div>
        </section>
      )}
    </>
  )
}

function CabecalhoGrupo({ grupo, quantidade }: { grupo: GrupoEstoqueDto; quantidade: number }) {
  return (
    <tr className="linha-grupo">
      <th scope="rowgroup" colSpan={6}>
        <span className="linha-grupo-conteudo">
          <strong>{grupo.nome}</strong>
          <Selo tom={TOM_DO_MODO[grupo.modoControle] ?? 'neutro'}>{nomeDoModo(grupo.modoControle)}</Selo>
          <span className="suave">
            {quantidade} {quantidade === 1 ? 'item' : 'itens'}
          </span>
          <Link to={`/estoque/grupos/${grupo.id}`} className="linha-grupo-editar">
            Editar grupo
          </Link>
        </span>
      </th>
    </tr>
  )
}

function LinhaItem({ item, aoAbrir }: { item: ItemEstoqueDto; aoAbrir: () => void }) {
  return (
    <tr className="linha-clicavel" onClick={aoAbrir}>
      <td>
        <Link to={`/estoque/itens/${item.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
          {item.nome}
        </Link>
        {!item.ativo && (
          <>
            {' '}
            <Selo>Desativado</Selo>
          </>
        )}
      </td>
      <td className="so-largo suave">{item.fornecedorNome}</td>
      <td>{descreverEmbalagem(item.unidade, item.unidadesPorEmbalagem)}</td>
      <td className="numero">{formatarMoeda(item.custoEmbalagem)}</td>
      <td className="numero so-largo">{item.minimoUnidades ?? '—'}</td>
      <td className="numero so-largo">{item.estoqueAlvoEmbalagens !== null ? quantidadeParaCampo(item.estoqueAlvoEmbalagens) : '—'}</td>
    </tr>
  )
}
