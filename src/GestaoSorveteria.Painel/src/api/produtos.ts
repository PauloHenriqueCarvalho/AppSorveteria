import { requisicao } from './cliente.ts'
import type {
  CatalogoProdutosResponse,
  CustoProdutoDto,
  InformarCustoProdutoRequest,
  LigarProdutoEstoqueRequest,
  ProdutoDto,
  SalvarProdutoRequest,
} from './tipos.ts'

/** Chave do TanStack Query para a lista; o produto único usa [...CHAVE_PRODUTOS, id]. */
export const CHAVE_PRODUTOS = ['produtos'] as const

/** GET /api/produtos — catálogo inteiro, inclusive os desativados. */
export async function listarProdutos(): Promise<ProdutoDto[]> {
  const catalogo = await requisicao<CatalogoProdutosResponse>('/api/produtos')
  return catalogo.produtos
}

/** GET /api/produtos/{id} */
export function obterProduto(id: string): Promise<ProdutoDto> {
  return requisicao<ProdutoDto>(`/api/produtos/${encodeURIComponent(id)}`)
}

/** POST /api/produtos (Admin) */
export function criarProduto(dados: SalvarProdutoRequest): Promise<ProdutoDto> {
  return requisicao<ProdutoDto>('/api/produtos', { metodo: 'POST', corpo: dados })
}

/** PUT /api/produtos/{id} (Admin) */
export function atualizarProduto(id: string, dados: SalvarProdutoRequest): Promise<ProdutoDto> {
  return requisicao<ProdutoDto>(`/api/produtos/${encodeURIComponent(id)}`, { metodo: 'PUT', corpo: dados })
}

/** PUT /api/produtos/{id}/estoque (Admin) — RN-PR-06: liga ao grupo de estoque (nulo = desliga). */
export function ligarProdutoAoEstoque(id: string, dados: LigarProdutoEstoqueRequest): Promise<ProdutoDto> {
  return requisicao<ProdutoDto>(`/api/produtos/${encodeURIComponent(id)}/estoque`, { metodo: 'PUT', corpo: dados })
}

/** Custos dos produtos: dentro de CHAVE_PRODUTOS, então invalidar os produtos também recarrega os custos. */
export const CHAVE_CUSTOS = [...CHAVE_PRODUTOS, 'custos'] as const

/** GET /api/produtos/custos (Admin) — RN-PR-07: custo estimado e margem de cada produto. */
export function listarCustos(): Promise<CustoProdutoDto[]> {
  return requisicao<CustoProdutoDto[]>('/api/produtos/custos')
}

/** PUT /api/produtos/{id}/custo (Admin) — custo de uma unidade vendida; nulo apaga. */
export function informarCusto(id: string, dados: InformarCustoProdutoRequest): Promise<CustoProdutoDto> {
  return requisicao<CustoProdutoDto>(`/api/produtos/${encodeURIComponent(id)}/custo`, { metodo: 'PUT', corpo: dados })
}

/** POST /api/produtos/{id}/ativar ou /desativar (Admin). Produto nunca é apagado (RN-PR-03). */
export function alterarAtivo(id: string, ativo: boolean): Promise<ProdutoDto> {
  const acao = ativo ? 'ativar' : 'desativar'
  return requisicao<ProdutoDto>(`/api/produtos/${encodeURIComponent(id)}/${acao}`, { metodo: 'POST' })
}
