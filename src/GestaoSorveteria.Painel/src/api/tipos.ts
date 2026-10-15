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

// ---------------------------------------------------------------- Fase 2 — contagem semanal (Contracts/Contagens)

export interface ContagemResumoDto {
  id: string
  contadoPorNome: string
  origem: 'App' | 'Painel'
  iniciadaEm: string
  concluidaEm: string | null
  itensContados: number
  itensNaoContados: number
}

/** Uma linha por item que entra na contagem, na ordem da planilha. Quantidades em unidades e em embalagens. */
export interface LinhaContagemDto {
  itemEstoqueId: string
  grupoId: string
  grupoNome: string
  itemNome: string
  unidade: UnidadeCompra
  unidadesPorEmbalagem: number
  /** 0,5 para caixa (meia caixa), 1 para o resto. */
  passoEmbalagens: number
  contadoUnidades: number | null
  contadoEmbalagens: number | null
  anteriorUnidades: number | null
  anteriorEmbalagens: number | null
  saldoSistemaUnidades: number | null
}

export interface ContagemDto {
  id: string
  contadoPorNome: string
  origem: 'App' | 'Painel'
  iniciadaEm: string
  concluidaEm: string | null
  linhas: LinhaContagemDto[]
}

export interface SalvarContagemRequest {
  id: string
  linhas: { itemEstoqueId: string; embalagens: number | null }[]
  concluir: boolean
}

// ---------------------------------------------------------------- Fase 2 — pedido ao fornecedor (Contracts/Pedidos)

export type StatusPedido = 'Rascunho' | 'Enviado' | 'Recebido' | 'Cancelado'
export type OrigemLinhaPedido = 'Sugestao' | 'Aviso' | 'FaltouAnterior' | 'AdicionadoPelaDona' | 'NaoPedido'
export type SituacaoRecebimento = 'Certo' | 'Faltou' | 'VeioAMais' | 'NaoPedido'

/** GET /api/pedidos-fornecedor — últimos 90 dias e os que ainda não chegaram. */
export interface PedidoResumoDto {
  id: string
  numero: number
  fornecedorId: string
  fornecedorNome: string
  status: StatusPedido
  criadoEm: string
  enviadoEm: string | null
  recebidoEm: string | null
  canceladoEm: string | null
  totalPedido: number
  totalRecebido: number | null
  percentualAtendido: number | null
  itensComQuantidade: number
}

/** Linha do pedido, na ordem da planilha. Valores em R$ calculados pela API. */
export interface LinhaPedidoDto {
  itemEstoqueId: string
  grupoId: string
  grupoNome: string
  itemNome: string
  unidade: UnidadeCompra
  unidadesPorEmbalagem: number
  /** Passo do pedido: 0,5 (caixa, fornecedor aceita meia) ou 1. */
  passo: number
  /** Última contagem concluída, em unidades. */
  contadoUnidades: number | null
  sugeridoEmbalagens: number
  pedidoEmbalagens: number
  recebidoEmbalagens: number | null
  custoEmbalagem: number
  subtotal: number
  alteradoPelaDona: boolean
  origem: OrigemLinhaPedido
  observacao: string | null
  pedirDeNovo: boolean
  situacao: SituacaoRecebimento | null
  diferencaValor: number | null
}

/** GET /api/pedidos-fornecedor/{id} — tela, folha impressa (RN-PF-07) e conferência. */
export interface PedidoDto {
  id: string
  numero: number
  fornecedorId: string
  fornecedorNome: string
  envio: FormaEnvioPedido
  mostrarPrecos: boolean
  aceitaMeiaCaixa: boolean
  status: StatusPedido
  criadoPorNome: string
  criadoEm: string
  enviadoEm: string | null
  recebidoEm: string | null
  recebidoPorNome: string | null
  canceladoEm: string | null
  canceladoPorNome: string | null
  motivoCancelamento: string | null
  /** Dia comercial ("2026-10-21"): envio (ou hoje) + prazo do fornecedor. */
  entregaPrevista: string
  totalPedido: number
  totalRecebido: number | null
  percentualAtendido: number | null
  linhas: LinhaPedidoDto[]
}

export interface LinhaAlteradaRequest {
  itemEstoqueId: string
  embalagens: number
}

/** POST /api/pedidos-fornecedor/{id}/enviar */
export interface EnviarPedidoResponse {
  pedido: PedidoDto
  mensagemWhatsApp: string | null
  linkWhatsApp: string | null
}

export interface LinhaRecebidaRequest {
  itemEstoqueId: string
  recebidoEmbalagens: number
  pedirDeNovo?: boolean
}

export interface NovoCustoRequest {
  itemEstoqueId: string
  custoEmbalagem: number
}

/** POST /api/pedidos-fornecedor/{id}/receber (RN-RC-01). "Chegou tudo certo" = listas vazias. */
export interface ReceberPedidoRequest {
  diferencas: LinhaRecebidaRequest[]
  naoPedidos: LinhaRecebidaRequest[]
  novosCustos: NovoCustoRequest[]
}

export interface ReceberPedidoResponse {
  pedido: PedidoDto
  avisosFechados: number
  mensagemFornecedor: string | null
  linkWhatsApp: string | null
}

/** GET /api/pedidos-fornecedor/{id}/mensagens — para reabrir o WhatsApp depois. */
export interface MensagensPedidoDto {
  pedido: string | null
  linkPedido: string | null
  divergencias: string | null
  linkDivergencias: string | null
}

/** GET /api/fornecedores/{id}/historico (RN-RC-06): últimos 30 dias. */
export interface HistoricoFornecedorDto {
  fornecedorId: string
  fornecedorNome: string
  pedidosRecebidos: number
  valorPedido: number
  valorRecebido: number
  percentualAtendidoMedio: number | null
  itensQueMaisFaltam: ItemQueMaisFaltaDto[]
}

export interface ItemQueMaisFaltaDto {
  itemEstoqueId: string
  descricao: string
  vezesQueFaltou: number
  embalagensQueFaltaram: number
}

/** Contracts/Relatorios/RelatorioPeriodoDto — GET /api/relatorios/periodo. Tudo calculado na API (RN-RL-01/03/08/09/10). */
export interface RelatorioPeriodoDto {
  de: string
  ate: string
  agrupamento: AgrupamentoRelatorio
  totalVendido: number
  quantidadeComandas: number
  ticketMedio: number
  totalEstornado: number
  quantidadeEstornos: number
  porFormaPagamento: TotalPorFormaDto[]
  anterior: ComparativoPeriodoDto
  /** Um por dia/semana/mês do período, inclusive os sem venda. */
  porTrecho: VendasNoTrechoDto[]
  porHora: VendasNaHoraDto[]
  /** Segunda a domingo (0 = domingo), só os dias da semana que o período tem. */
  porDiaDaSemana: VendasNoDiaDaSemanaDto[]
  produtos: ProdutoNoRankingDto[]
  categorias: CategoriaNoRankingDto[]
  sabores: SaborNoRankingDto[]
}

export type AgrupamentoRelatorio = 'Dia' | 'Semana' | 'Mes'

/** RN-RL-09: período de comparação; trechos pareados por posição com os do período. */
export interface ComparativoPeriodoDto {
  de: string
  ate: string
  ateMesmoHorario: boolean
  totalVendido: number
  quantidadeComandas: number
  ticketMedio: number
  variacaoTotalPercentual: number | null
  variacaoQuantidadePercentual: number | null
  variacaoTicketPercentual: number | null
  porTrecho: VendasNoTrechoDto[]
}

export interface VendasNoTrechoDto {
  de: string
  ate: string
  total: number
  quantidade: number
}

export interface VendasNoDiaDaSemanaDto {
  /** 0 = domingo … 6 = sábado. */
  diaDaSemana: number
  /** Quantos desses dias o período tem. */
  dias: number
  total: number
  quantidade: number
  mediaPorDia: number
}

/** produtoId nulo = self-service / venda avulsa. Percentual = parte do faturamento (1 casa). */
export interface ProdutoNoRankingDto {
  produtoId: string | null
  descricao: string
  categoria: string
  quantidade: number
  total: number
  percentual: number
}

export interface CategoriaNoRankingDto {
  categoria: string
  quantidade: number
  total: number
  percentual: number
}

/** Sabor dos potes ("Pote 1L Flocos"); unidades = potes que saíram. */
export interface SaborNoRankingDto {
  itemEstoqueId: string
  descricao: string
  unidades: number
  total: number
}
