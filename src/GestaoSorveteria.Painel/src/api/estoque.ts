import { requisicao } from './cliente.ts'
import type {
  CatalogoEstoqueResponse,
  FornecedorDto,
  GrupoEstoqueDto,
  ImportarCatalogoResponse,
  ItemEstoqueDto,
  SalvarFornecedorRequest,
  SalvarGrupoEstoqueRequest,
  SalvarItemEstoqueRequest,
} from './tipos.ts'

/** Uma consulta só para o catálogo inteiro (fornecedores, grupos e itens): as telas de estoque leem dela. */
export const CHAVE_CATALOGO_ESTOQUE = ['estoque', 'catalogo'] as const

/** GET /api/estoque/catalogo (Admin) */
export function obterCatalogoEstoque(): Promise<CatalogoEstoqueResponse> {
  return requisicao<CatalogoEstoqueResponse>('/api/estoque/catalogo')
}

const id = (valor: string) => encodeURIComponent(valor)

/** POST /api/estoque/itens ou PUT /api/estoque/itens/{id} */
export function salvarItemEstoque(itemId: string | undefined, dados: SalvarItemEstoqueRequest): Promise<ItemEstoqueDto> {
  return itemId
    ? requisicao<ItemEstoqueDto>(`/api/estoque/itens/${id(itemId)}`, { metodo: 'PUT', corpo: dados })
    : requisicao<ItemEstoqueDto>('/api/estoque/itens', { metodo: 'POST', corpo: dados })
}

/** Item nunca é apagado: desativar tira da contagem e do pedido. */
export function alterarAtivoItemEstoque(itemId: string, ativo: boolean): Promise<ItemEstoqueDto> {
  return requisicao<ItemEstoqueDto>(`/api/estoque/itens/${id(itemId)}/${ativo ? 'ativar' : 'desativar'}`, { metodo: 'POST' })
}

/** POST /api/estoque/grupos ou PUT /api/estoque/grupos/{id} */
export function salvarGrupoEstoque(grupoId: string | undefined, dados: SalvarGrupoEstoqueRequest): Promise<GrupoEstoqueDto> {
  return grupoId
    ? requisicao<GrupoEstoqueDto>(`/api/estoque/grupos/${id(grupoId)}`, { metodo: 'PUT', corpo: dados })
    : requisicao<GrupoEstoqueDto>('/api/estoque/grupos', { metodo: 'POST', corpo: dados })
}

/** POST /api/fornecedores ou PUT /api/fornecedores/{id} */
export function salvarFornecedor(fornecedorId: string | undefined, dados: SalvarFornecedorRequest): Promise<FornecedorDto> {
  return fornecedorId
    ? requisicao<FornecedorDto>(`/api/fornecedores/${id(fornecedorId)}`, { metodo: 'PUT', corpo: dados })
    : requisicao<FornecedorDto>('/api/fornecedores', { metodo: 'POST', corpo: dados })
}

/** RN-PF-09: a API recusa desativar fornecedor com itens ativos. */
export function alterarAtivoFornecedor(fornecedorId: string, ativo: boolean): Promise<FornecedorDto> {
  return requisicao<FornecedorDto>(`/api/fornecedores/${id(fornecedorId)}/${ativo ? 'ativar' : 'desativar'}`, { metodo: 'POST' })
}

/** RN-ES-10: `simular` = prévia sem gravar. */
export function importarCatalogo(conteudo: string, simular: boolean): Promise<ImportarCatalogoResponse> {
  return requisicao<ImportarCatalogoResponse>('/api/estoque/itens/importar', { metodo: 'POST', corpo: { conteudo, simular } })
}
