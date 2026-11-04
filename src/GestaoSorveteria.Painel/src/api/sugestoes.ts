import { requisicao } from './cliente.ts'

// Sugestões da dona (RN-SG-01..05): o que ela quer mudar ou ganhar no sistema, com imagens e a conversa.

export type TipoSugestao = 'Ideia' | 'Mudanca' | 'Problema' | 'Duvida'
export type AreaSugestao = 'Balcao' | 'Painel' | 'Estoque' | 'Relatorios' | 'Outro'
export type ImportanciaSugestao = 'Baixa' | 'Media' | 'Alta'
export type StatusSugestao = 'Nova' | 'EmAnalise' | 'Planejada' | 'Feita' | 'Recusada'

export interface SalvarSugestaoRequest {
  titulo: string
  descricao: string
  tipo: TipoSugestao
  area: AreaSugestao
  importancia: ImportanciaSugestao
}

export interface SugestaoResumoDto {
  id: string
  titulo: string
  tipo: TipoSugestao
  area: AreaSugestao
  importancia: ImportanciaSugestao
  status: StatusSugestao
  criadaPorNome: string
  criadaEm: string
  atualizadaEm: string
  comentarios: number
  imagens: number
}

export interface SugestaoComentarioDto {
  id: string
  usuarioId: string
  usuarioNome: string
  texto: string
  em: string
}

export interface SugestaoImagemDto {
  id: string
  nomeArquivo: string
  tipoConteudo: string
  tamanhoBytes: number
  enviadaEm: string
}

export interface SugestaoDto extends Omit<SugestaoResumoDto, 'comentarios' | 'imagens'> {
  descricao: string
  encerrada: boolean
  criadaPorUsuarioId: string
  comentarios: SugestaoComentarioDto[]
  imagens: SugestaoImagemDto[]
}

export const CHAVE_SUGESTOES = ['sugestoes'] as const

const caminho = (id: string) => `/api/sugestoes/${encodeURIComponent(id)}`

/** GET /api/sugestoes (Admin) — abertas primeiro, a mais recente primeiro. */
export function listarSugestoes(): Promise<SugestaoResumoDto[]> {
  return requisicao<SugestaoResumoDto[]>('/api/sugestoes')
}

export function obterSugestao(id: string): Promise<SugestaoDto> {
  return requisicao<SugestaoDto>(caminho(id))
}

export function criarSugestao(dados: SalvarSugestaoRequest): Promise<SugestaoDto> {
  return requisicao<SugestaoDto>('/api/sugestoes', { metodo: 'POST', corpo: dados })
}

export function atualizarSugestao(id: string, dados: SalvarSugestaoRequest): Promise<SugestaoDto> {
  return requisicao<SugestaoDto>(caminho(id), { metodo: 'PUT', corpo: dados })
}

export function comentarSugestao(id: string, texto: string): Promise<SugestaoDto> {
  return requisicao<SugestaoDto>(`${caminho(id)}/comentarios`, { metodo: 'POST', corpo: { texto } })
}

/** RN-SG-03: recusar pede o comentário (o motivo). */
export function alterarStatusSugestao(id: string, status: StatusSugestao, comentario: string | null): Promise<SugestaoDto> {
  return requisicao<SugestaoDto>(`${caminho(id)}/status`, { metodo: 'POST', corpo: { status, comentario } })
}

/** RN-SG-05: até 5 imagens por sugestão, até 2 MB cada (use `prepararImagem` antes). */
export function anexarImagem(id: string, arquivo: Blob, nome: string): Promise<SugestaoDto> {
  const formulario = new FormData()
  formulario.append('arquivo', arquivo, nome)
  return requisicao<SugestaoDto>(`${caminho(id)}/imagens`, { metodo: 'POST', formulario })
}

/** A imagem vem com o token (não dá para usar a URL direto no <img>). */
export function baixarImagem(id: string, imagemId: string): Promise<Blob> {
  return requisicao<Blob>(`${caminho(id)}/imagens/${encodeURIComponent(imagemId)}`, { comoBlob: true })
}

export function removerImagem(id: string, imagemId: string): Promise<SugestaoDto> {
  return requisicao<SugestaoDto>(`${caminho(id)}/imagens/${encodeURIComponent(imagemId)}`, { metodo: 'DELETE' })
}
