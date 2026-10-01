// Espelho em TypeScript dos DTOs de GestaoSorveteria.Contracts (JSON em camelCase).
// Mudou um DTO na API → atualize aqui.

/** Contracts/Auth/LoginRequest */
export interface LoginRequest {
  login: string
  senha: string
}

/** Contracts/Auth/LoginResponse */
export interface LoginResponse {
  token: string
  /** Data/hora UTC em ISO 8601 em que o token deixa de valer. */
  expiraEmUtc: string
  usuario: UsuarioDto
}

/** Contracts/Auth/UsuarioDto */
export interface UsuarioDto {
  id: string
  nome: string
  login: string
  perfil: Perfil
  ativo: boolean
}

export type Perfil = 'Admin' | 'Atendente'

/** Só a administração da loja entra no painel. */
export const PERFIL_ADMIN: Perfil = 'Admin'

/** Contracts/Produtos/ProdutoDto */
export interface ProdutoDto {
  id: string
  nome: string
  categoria: string
  /** Preço em reais, como a API devolve (decimal com 2 casas). */
  preco: number
  /** Self-service: o atendente digita o valor na venda; o preço é só sugestão (RN-PR-02). */
  permiteValorLivre: boolean
  ativo: boolean
  /** Posição do botão no app (RN-PR-05). */
  ordem: number
  atualizadoEmUtc: string
}

/** Contracts/Produtos/CatalogoProdutosResponse */
export interface CatalogoProdutosResponse {
  geradoEmUtc: string
  produtos: ProdutoDto[]
}

/** Contracts/Produtos/SalvarProdutoRequest — POST e PUT /api/produtos. */
export interface SalvarProdutoRequest {
  nome: string
  categoria: string
  preco: number
  permiteValorLivre: boolean
  ordem: number
}

/** Contracts/Caixas/TotalPorFormaDto — soma dos pagamentos de uma forma. */
export interface TotalPorFormaDto {
  forma: string
  total: number
  quantidade: number
}

/**
 * Contracts/Caixas/CaixaResumoDto. Fechado: valores gravados no fechamento. Aberto: vendas em dinheiro e
 * esperado "até agora"; contado e diferença nulos. Todos os valores vêm calculados pela API.
 */
export interface CaixaResumoDto {
  id: string
  status: 'Aberto' | 'Fechado'
  abertoEm: string
  abertoPorNome: string
  fundoTroco: number
  fechadoEm: string | null
  fechadoPorNome: string | null
  totalVendas: number
  quantidadeComandas: number
  totalVendasDinheiro: number | null
  valorEsperado: number | null
  valorContado: number | null
  /** contado − esperado. Negativo = faltou dinheiro na gaveta. */
  diferenca: number | null
  /** RN-CX-10: o fechamento feito no celular não bateu com o do servidor. */
  divergenciaSincronizacao: boolean
  /** RN-CX-08: vendas que chegaram depois do fechamento. */
  vendasRecebidasAposFechamento: number
  dinheiroRecebidoAposFechamento: number
}

/** Contracts/Caixas/MovimentoCaixaDto */
export interface MovimentoCaixaDto {
  id: string
  tipo: 'Sangria' | 'Suprimento'
  valor: number
  motivo: string
  usuarioNome: string
  em: string
}

/** Contracts/Caixas/CaixaDetalheDto */
export interface CaixaDetalheDto {
  resumo: CaixaResumoDto
  movimentos: MovimentoCaixaDto[]
  porFormaPagamento: TotalPorFormaDto[]
  observacao: string | null
}

/** Contracts/Comandas/PaginaDto — totalItens é o total sem paginação. */
export interface PaginaDto<T> {
  itens: T[]
  pagina: number
  tamanho: number
  totalItens: number
}

export type StatusComanda = 'Aberta' | 'Fechada' | 'Cancelada' | 'Estornada'

/** Contracts/Comandas/ComandaResumoDto — linha da lista de vendas. */
export interface ComandaResumoDto {
  id: string
  numero: number
  caixaId: string
  tipo: 'Balcao' | 'Delivery'
  status: StatusComanda
  total: number
  criadaEm: string
  fechadaEm: string | null
  atendenteNome: string
  formasPagamento: string[]
  recebidaAposFechamentoCaixa: boolean
}

/** Contracts/Comandas/ItemComandaDto — produtoId nulo = item livre (self-service, venda avulsa). */
export interface ItemComandaDto {
  id: string
  produtoId: string | null
  descricao: string
  quantidade: number
  precoUnitario: number
  subtotal: number
}

/** Contracts/Comandas/PagamentoDto — troco calculado na venda. */
export interface PagamentoDto {
  id: string
  forma: string
  valor: number
  valorRecebido: number
  troco: number
}

/** Contracts/Comandas/ComandaDetalheDto */
export interface ComandaDetalheDto {
  id: string
  numero: number
  caixaId: string
  tipo: 'Balcao' | 'Delivery'
  status: StatusComanda
  total: number
  observacao: string | null
  /** Hora do celular. */
  criadaEm: string
  /** Hora em que o servidor recebeu. */
  recebidaEm: string
  fechadaEm: string | null
  canceladaEm: string | null
  motivoCancelamento: string | null
  estornadaEm: string | null
  estornadaPorNome: string | null
  motivoEstorno: string | null
  recebidaAposFechamentoCaixa: boolean
  atendenteNome: string
  itens: ItemComandaDto[]
  pagamentos: PagamentoDto[]
}
