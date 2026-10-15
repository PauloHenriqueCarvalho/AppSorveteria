import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useNavigate } from 'react-router'
import { CHAVE_CUSTOS, CHAVE_PRODUTOS, listarCustos, listarProdutos } from '../../api/produtos.ts'
import type { GrupoEstoqueDto, ProdutoDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Selo } from '../../componentes/Ui.tsx'
import { formatarMoeda } from '../../formatacao.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

type Filtro = 'ativos' | 'inativos' | 'todos'

const FILTROS: { valor: Filtro; texto: string }[] = [
  { valor: 'ativos', texto: 'Ativos' },
  { valor: 'inativos', texto: 'Desativados' },
  { valor: 'todos', texto: 'Todos' },
]

// Mesma ordem dos botões no app (RN-PR-05); empate pelo nome.
function naOrdemDoApp(a: ProdutoDto, b: ProdutoDto) {
  return a.ordem - b.ordem || a.nome.localeCompare(b.nome, 'pt-BR')
}

/** RN-PR-06: "Pote 1L · pede o sabor", "Pote 1L · 2 por venda" ou nada. */
function descreverEstoque(p: ProdutoDto, grupos: Map<string, GrupoEstoqueDto>): string | null {
  if (!p.grupoEstoqueId) {
    return null
  }
  const grupo = grupos.get(p.grupoEstoqueId)
  const partes = [grupo?.nome ?? 'Grupo de estoque']
  if ((p.unidadesPorVenda ?? 1) > 1) {
    partes.push(`${p.unidadesPorVenda} por venda`)
  }
  if (p.pedeSabor) {
    partes.push('pede o sabor')
  }
  return partes.join(' · ')
}

/** /produtos — cardápio do tablet, na ordem dos botões, com preço e o que cada um baixa do estoque. */
export default function ListaProdutos() {
  const navegar = useNavigate()
  const [filtro, setFiltro] = useState<Filtro>('ativos')
  const [busca, setBusca] = useState('')
  const [categoria, setCategoria] = useState('')
  const produtos = useQuery({ queryKey: CHAVE_PRODUTOS, queryFn: listarProdutos })
  const catalogo = useCatalogoEstoque()
  // RN-PR-07/RN-RL-04: custo e margem calculados pela API (só o painel vê).
  const custos = useQuery({ queryKey: CHAVE_CUSTOS, queryFn: listarCustos })
  const custoDe = new Map((custos.data ?? []).map((c) => [c.produtoId, c]))
  const grupos = new Map((catalogo.data?.grupos ?? []).map((g) => [g.id, g]))

  const ativos = produtos.data?.filter((p) => p.ativo).length ?? 0
  const categorias = [...new Set((produtos.data ?? []).map((p) => p.categoria))].sort((a, b) => a.localeCompare(b, 'pt-BR'))

  return (
    <>
      <CabecalhoPagina
        titulo="Produtos"
        subtitulo={
          produtos.data
            ? `${ativos} ${ativos === 1 ? 'produto ativo' : 'produtos ativos'} em ${categorias.length} ${categorias.length === 1 ? 'categoria' : 'categorias'} · na ordem dos botões do tablet`
            : 'Cardápio do tablet'
        }
        acoes={
          <Link to="/produtos/novo" className="botao">
            Novo produto
          </Link>
        }
      />

      <div className="filtros">
        <input
          type="search"
          className="busca"
          placeholder="Buscar por nome ou categoria"
          aria-label="Buscar produto"
          value={busca}
          onChange={(e) => setBusca(e.target.value)}
        />
        {categorias.length > 1 && (
          <label className="campo-inline">
            Categoria
            <select value={categoria} onChange={(e) => setCategoria(e.target.value)}>
              <option value="">Todas</option>
              {categorias.map((c) => (
                <option key={c} value={c}>
                  {c}
                </option>
              ))}
            </select>
          </label>
        )}
        <div className="abas" role="group" aria-label="Mostrar">
          {FILTROS.map((f) => (
            <button
              key={f.valor}
              type="button"
              className={f.valor === filtro ? 'aba aba-ativa' : 'aba'}
              aria-pressed={f.valor === filtro}
              onClick={() => setFiltro(f.valor)}
            >
              {f.texto}
            </button>
          ))}
        </div>
      </div>

      <Consulta consulta={produtos}>
        {(lista) => {
          const termo = busca.trim().toLocaleLowerCase('pt-BR')
          const visiveis = lista
            .filter((p) => filtro === 'todos' || p.ativo === (filtro === 'ativos'))
            .filter((p) => !categoria || p.categoria === categoria)
            .filter((p) => !termo || `${p.nome} ${p.categoria}`.toLocaleLowerCase('pt-BR').includes(termo))
            .sort(naOrdemDoApp)

          if (lista.length === 0) {
            return (
              <section className="secao vazio">
                <h2>Nenhum produto ainda</h2>
                <p className="suave">Cadastre o que aparece nos botões do tablet: picolés, potes, açaí, self-service…</p>
              </section>
            )
          }
          if (visiveis.length === 0) {
            return <p className="secao suave">Nenhum produto encontrado.</p>
          }
          return (
            <section className="secao secao-tabela">
              <div className="tabela-caixa">
                <table className="tabela">
                  <thead>
                    <tr>
                      <th scope="col" className="numero so-largo">
                        Ordem
                      </th>
                      <th scope="col">Produto</th>
                      <th scope="col" className="so-largo">
                        Categoria
                      </th>
                      <th scope="col" className="so-largo">
                        Estoque
                      </th>
                      <th scope="col" className="numero">
                        Preço
                      </th>
                      <th scope="col" className="numero so-largo">
                        Custo
                      </th>
                      <th scope="col" className="numero so-largo">
                        Margem
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {visiveis.map((p) => {
                      const estoque = descreverEstoque(p, grupos)
                      const custo = custoDe.get(p.id)
                      return (
                        <tr key={p.id} className="linha-clicavel" onClick={() => navegar(`/produtos/${p.id}`)}>
                          <td className="numero suave so-largo">{p.ordem}</td>
                          <td>
                            <Link to={`/produtos/${p.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
                              {p.nome}
                            </Link>
                            {!p.ativo && (
                              <>
                                {' '}
                                <Selo>Desativado</Selo>
                              </>
                            )}
                            <span className="suave bloco pequeno so-estreito">{p.categoria}</span>
                          </td>
                          <td className="so-largo">{p.categoria}</td>
                          <td className="so-largo suave">{estoque ?? '—'}</td>
                          <td className="numero forte">{p.permiteValorLivre ? <span className="suave">Valor na hora</span> : formatarMoeda(p.preco)}</td>
                          <td className="numero so-largo suave">{custo?.custoEstimado != null ? formatarMoeda(custo.custoEstimado) : 'n/d'}</td>
                          <td className="numero so-largo">
                            {custo?.margem != null ? (
                              <>
                                {formatarMoeda(custo.margem)}{' '}
                                <span className="suave pequeno">{custo.margemPercentual?.toLocaleString('pt-BR', { maximumFractionDigits: 1 })}%</span>
                              </>
                            ) : (
                              <span className="suave">n/d</span>
                            )}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            </section>
          )
        }}
      </Consulta>
    </>
  )
}
