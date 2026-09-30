import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { CHAVE_PRODUTOS, listarProdutos } from '../../api/produtos.ts'
import type { ProdutoDto } from '../../api/tipos.ts'
import ConectandoServidor from '../../componentes/ConectandoServidor.tsx'
import { formatarMoeda } from '../../formatacao.ts'

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

export default function ListaProdutos() {
  const [filtro, setFiltro] = useState<Filtro>('ativos')
  const [busca, setBusca] = useState('')
  const produtos = useQuery({ queryKey: CHAVE_PRODUTOS, queryFn: listarProdutos })

  if (produtos.isPending) {
    return <ConectandoServidor falhas={produtos.failureCount} />
  }
  if (produtos.isError) {
    return (
      <div className="cartao">
        <p className="alerta alerta-erro">{mensagemDoErro(produtos.error)}</p>
        <button type="button" className="botao" onClick={() => void produtos.refetch()}>
          Tentar de novo
        </button>
      </div>
    )
  }

  const termo = busca.trim().toLocaleLowerCase('pt-BR')
  const visiveis = produtos.data
    .filter((p) => filtro === 'todos' || p.ativo === (filtro === 'ativos'))
    .filter((p) => !termo || `${p.nome} ${p.categoria}`.toLocaleLowerCase('pt-BR').includes(termo))
    .sort(naOrdemDoApp)

  return (
    <>
      <div className="cabecalho-pagina">
        <h1>Produtos</h1>
        <Link to="/produtos/novo" className="botao">
          Novo produto
        </Link>
      </div>

      <div className="filtros">
        <input
          type="search"
          className="busca"
          placeholder="Buscar por nome ou categoria"
          aria-label="Buscar produto"
          value={busca}
          onChange={(e) => setBusca(e.target.value)}
        />
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

      {visiveis.length === 0 ? (
        <p className="cartao suave">
          {produtos.data.length === 0 ? 'Nenhum produto cadastrado ainda.' : 'Nenhum produto encontrado.'}
        </p>
      ) : (
        <ul className="lista">
          {visiveis.map((p) => (
            <li key={p.id}>
              <Link to={`/produtos/${p.id}`} className="lista-item">
                <span className="lista-principal">
                  <strong>{p.nome}</strong>
                  <span className="suave">{p.categoria}</span>
                </span>
                <span className="lista-lateral">
                  <span>{p.permiteValorLivre ? 'Valor na hora' : formatarMoeda(p.preco)}</span>
                  {!p.ativo && <span className="etiqueta">Desativado</span>}
                </span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </>
  )
}
