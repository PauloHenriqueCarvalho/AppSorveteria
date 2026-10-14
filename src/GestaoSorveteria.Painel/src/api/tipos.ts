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

/** Contracts/Usuarios/CriarUsuarioRequest — POST /api/usuarios. Senha = PIN (atendente) ou senha (Admin), RN-US-03. */
export interface CriarUsuarioRequest {
  nome: string
  login: string
  senha: string
  perfil: Perfil
}

/** Contracts/Usuarios/RedefinirSenhaRequest — POST /api/usuarios/{id}/redefinir-senha. */
export interface RedefinirSenhaRequest {
  novaSenha: string
}

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
  /** RN-CX-09: fechado pela dona no painel, com contado 0 (o atendente esqueceu de fechar). */
  fechamentoForcado: boolean
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

/** Contracts/Relatorios/ResumoDiaDto — dashboard. Tudo calculado na API. */
export interface ResumoDiaDto {
  /** Dia comercial "2026-09-30". */
  data: string
  totalVendido: number
  quantidadeComandas: number
  ticketMedio: number
  porFormaPagamento: TotalPorFormaDto[]
  /** Vendas do dia estornadas depois (RN-RL-01), fora do total vendido. */
  totalEstornado: number
  quantidadeEstornos: number
  quantidadeCanceladas: number
  vendasRecebidasAposFechamento: number
  /** Caixa aberto agora (último estado recebido do celular), ou null. */
  caixaAtual: CaixaResumoDto | null
  /** RN-RL-06: só as horas com venda (0–23, horário da loja). */
  porHora: VendasNaHoraDto[]
  /** RN-RL-07: mesmo dia da semana anterior. */
  semanaPassada: ComparativoDiaDto
  /** RN-RL-03: até 5 por faturamento; produtoId nulo = self-service / venda avulsa. */
  maisVendidos: ProdutoVendidoDto[]
}

/** Contracts/Relatorios/VendasNaHoraDto */
export interface VendasNaHoraDto {
  hora: number
  total: number
  quantidade: number
}

/** Contracts/Relatorios/ComparativoDiaDto — variações em % já calculadas pela API (null = sem base). */
export interface ComparativoDiaDto {
  data: string
  /** true quando o dia consultado é hoje: a semana passada conta só até o mesmo horário. */
  ateMesmoHorario: boolean
  totalVendido: number
  quantidadeComandas: number
  ticketMedio: number
  variacaoTotalPercentual: number | null
  variacaoQuantidadePercentual: number | null
  variacaoTicketPercentual: number | null
  porHora: VendasNaHoraDto[]
}

/** Contracts/Relatorios/ProdutoVendidoDto */
export interface ProdutoVendidoDto {
  produtoId: string | null
  descricao: string
  quantidade: number
  total: number
}

// ---------------------------------------------------------------- Fase 2 — estoque (Contracts/Estoque)

export type ModoControle = 'Contagem' | 'SaldoPelaVenda' | 'SoAviso'
export type UnidadeCompra = 'Caixa' | 'Balde' | 'Unidade' | 'Pacote'
export type FormaEnvioPedido = 'Impresso' | 'WhatsApp' | 'Ambos'
/** Dias da semana como o .NET escreve (DayOfWeek). */
export type DiaSemana = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'

/** Contracts/Estoque/FornecedorDto (RN-PF-01) */
export interface FornecedorDto {
  id: string
  nome: string
  /** Só dígitos, com DDD. */
  whatsApp: string | null
  diasPedido: DiaSemana[]
  prazoEntregaDias: number
  aceitaMeiaCaixa: boolean
  envio: FormaEnvioPedido
  mostrarPrecos: boolean
  ativo: boolean
}

export interface SalvarFornecedorRequest {
  nome: string
  whatsApp: string | null
  diasPedido: DiaSemana[]
  prazoEntregaDias: number
  aceitaMeiaCaixa: boolean
  envio: FormaEnvioPedido
  mostrarPrecos: boolean
}

/** Contracts/Estoque/GrupoEstoqueDto (RN-ES-02) */
export interface GrupoEstoqueDto {
  id: string
  nome: string
  modoControle: ModoControle
  ordem: number
}

export interface SalvarGrupoEstoqueRequest {
  nome: string
  modoControle: ModoControle
  ordem: number
}

/** Contracts/Estoque/ItemEstoqueDto (RN-ES-01) */
export interface ItemEstoqueDto {
  id: string
  grupoId: string
  grupoNome: string
  fornecedorId: string
  fornecedorNome: string
  nome: string
  unidade: UnidadeCompra
  unidadesPorEmbalagem: number
  custoEmbalagem: number
  /** Em embalagens (meia caixa vale). */
  estoqueAlvoEmbalagens: number | null
  /** Em unidades (potes). */
  minimoUnidades: number | null
  ordem: number
  ativo: boolean
}

export interface SalvarItemEstoqueRequest {
  grupoId: string
  fornecedorId: string
  nome: string
  unidade: UnidadeCompra
  unidadesPorEmbalagem: number
  custoEmbalagem: number
  ordem: number
  estoqueAlvoEmbalagens: number | null
  minimoUnidades: number | null
}

/** GET /api/estoque/catalogo */
export interface CatalogoEstoqueResponse {
  fornecedores: FornecedorDto[]
  grupos: GrupoEstoqueDto[]
  itens: ItemEstoqueDto[]
}

/** POST /api/estoque/itens/importar (RN-ES-10) */
export interface ImportarCatalogoRequest {
  conteudo: string
  simular: boolean
}

export interface ImportarCatalogoResponse {
  simulacao: boolean
  fornecedoresNovos: string[]
  gruposNovos: string[]
  itensNovos: number
  itensJaCadastrados: number
  avisos: LinhaImportacaoDto[]
  erros: LinhaImportacaoDto[]
}

/** Linha como o Excel mostra (cabeçalho = 1). */
export interface LinhaImportacaoDto {
  linha: number
  mensagem: string
}

// ---------------------------------------------------------------- Fase 2 — avisos "Está acabando" (Contracts/Avisos)

export type PrioridadeAviso = 'Urgente' | 'Alta' | 'Media'

/** Contracts/Avisos/AvisoDto — descrição já pronta ("Picolé ao leite Coco preto" ou o texto livre). */
export interface AvisoDto {
  id: string
  itemEstoqueId: string | null
  descricao: string
  grupoNome: string | null
  fornecedorNome: string | null
  /** Na menor unidade (picolé, pote, balde, copo); 0 = acabou. */
  quantidadeAtual: number
  prioridade: PrioridadeAviso
  observacao: string | null
  /** "Sistema" quando nasceu sozinho (pote no mínimo). */
  criadoPorNome: string
  criadoEm: string
  atualizadoPorNome: string | null
  atualizadoEm: string
  status: 'Aberto' | 'Fechado'
  fechadoEm: string | null
  fechadoPorNome: string | null
  motivoFechamento: string | null
  whatsAppAbertoEm: string | null
}

export interface CriarAvisoRequest {
  id: string | null
  itemEstoqueId: string | null
  textoLivre: string | null
  quantidadeAtual: number
  prioridade: PrioridadeAviso
  observacao: string | null
}

export interface AindaFaltaRequest {
  quantidadeAtual: number
  prioridade: PrioridadeAviso
  observacao: string | null
}

export interface AvisoSalvoResponse {
  aviso: AvisoDto
  /** O item já tinha aviso aberto: ele foi atualizado em vez de criar outro (RN-AV-03). */
  jaExistia: boolean
  enviarAgora: boolean
  mensagemWhatsApp: string | null
  linkWhatsApp: string | null
}

/** GET /api/avisos/resumo-do-dia (RN-AV-07) */
export interface ResumoDoDiaResponse {
  data: string
  texto: string
  linkWhatsApp: string
  urgentes: number
  altas: number
  medias: number
}

/** GET/PUT /api/configuracoes */
export interface ConfiguracaoLojaDto {
  nomeLoja: string
  whatsAppDona: string | null
}
