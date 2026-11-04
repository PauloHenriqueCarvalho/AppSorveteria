import type { AreaSugestao, ImportanciaSugestao, StatusSugestao, TipoSugestao } from './api/sugestoes.ts'
import type { Tom } from './componentes/Ui.tsx'

// Textos da tela de sugestões (RN-SG-01/03) e o preparo das imagens (RN-SG-05).

export const TIPOS: { valor: TipoSugestao; nome: string; explicacao: string }[] = [
  { valor: 'Ideia', nome: 'Ideia nova', explicacao: 'Algo que o sistema ainda não faz' },
  { valor: 'Mudanca', nome: 'Mudança', explicacao: 'Mudar como algo já funciona' },
  { valor: 'Problema', nome: 'Problema', explicacao: 'Algo deu errado ou está estranho' },
  { valor: 'Duvida', nome: 'Dúvida', explicacao: 'Não sei como fazer algo' },
]

export const AREAS: { valor: AreaSugestao; nome: string }[] = [
  { valor: 'Balcao', nome: 'Tablet do balcão' },
  { valor: 'Painel', nome: 'Painel' },
  { valor: 'Estoque', nome: 'Estoque e pedidos' },
  { valor: 'Relatorios', nome: 'Relatórios' },
  { valor: 'Outro', nome: 'Outro' },
]

export const IMPORTANCIAS: { valor: ImportanciaSugestao; nome: string; explicacao: string }[] = [
  { valor: 'Baixa', nome: 'Baixa', explicacao: 'Seria bom ter' },
  { valor: 'Media', nome: 'Média', explicacao: 'Ajuda no dia a dia' },
  { valor: 'Alta', nome: 'Alta', explicacao: 'Faz falta na loja' },
]

export const STATUS: { valor: StatusSugestao; nome: string; tom: Tom }[] = [
  { valor: 'Nova', nome: 'Nova', tom: 'marca' },
  { valor: 'EmAnalise', nome: 'Em análise', tom: 'atencao' },
  { valor: 'Planejada', nome: 'Planejada', tom: 'atencao' },
  { valor: 'Feita', nome: 'Feita', tom: 'bom' },
  { valor: 'Recusada', nome: 'Não vai ser feita', tom: 'neutro' },
]

export const nomeTipo = (v: TipoSugestao) => TIPOS.find((t) => t.valor === v)?.nome ?? v
export const nomeArea = (v: AreaSugestao) => AREAS.find((a) => a.valor === v)?.nome ?? v
export const nomeImportancia = (v: ImportanciaSugestao) => IMPORTANCIAS.find((i) => i.valor === v)?.nome ?? v
export const status = (v: StatusSugestao) => STATUS.find((s) => s.valor === v) ?? { valor: v, nome: v, tom: 'neutro' as Tom }

export const IMAGENS_MAXIMO = 5
export const TIPOS_IMAGEM = ['image/jpeg', 'image/png', 'image/webp']
/** O servidor aceita até 2 MB; acima disso (ou foto grande do celular) o painel reduz antes de enviar. */
export const TAMANHO_SEM_REDUZIR = 1.5 * 1024 * 1024
export const LADO_MAXIMO = 1600

/** Novo tamanho para caber em `LADO_MAXIMO` sem distorcer; não aumenta imagem pequena. */
export function dimensaoReduzida(largura: number, altura: number, lado = LADO_MAXIMO): { largura: number; altura: number } {
  const maior = Math.max(largura, altura)
  if (maior <= lado) {
    return { largura, altura }
  }
  const fator = lado / maior
  return { largura: Math.round(largura * fator), altura: Math.round(altura * fator) }
}

/** "foto.HEIC" → "foto.jpg" (a imagem reduzida sai em JPEG). */
export function nomeEmJpeg(nome: string): string {
  const base = nome.replace(/\.[^./\\]+$/, '') || 'imagem'
  return `${base}.jpg`
}

export function formatarTamanho(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / (1024 * 1024)).toFixed(1).replace('.', ',')} MB`
}

/**
 * Deixa a imagem pronta para enviar: print pequeno vai como está; foto grande (ou outro formato que o navegador abre,
 * como HEIC no iPhone) vira JPEG de até 1600 px.
 */
export async function prepararImagem(arquivo: File): Promise<{ conteudo: Blob; nome: string }> {
  if (TIPOS_IMAGEM.includes(arquivo.type) && arquivo.size <= TAMANHO_SEM_REDUZIR) {
    return { conteudo: arquivo, nome: arquivo.name }
  }
  const bitmap = await createImageBitmap(arquivo)
  const { largura, altura } = dimensaoReduzida(bitmap.width, bitmap.height)
  const tela = document.createElement('canvas')
  tela.width = largura
  tela.height = altura
  const contexto = tela.getContext('2d')
  if (!contexto) {
    throw new Error('Não foi possível preparar a imagem neste navegador.')
  }
  contexto.drawImage(bitmap, 0, 0, largura, altura)
  bitmap.close()
  const conteudo = await new Promise<Blob | null>((ok) => tela.toBlob(ok, 'image/jpeg', 0.85))
  if (!conteudo) {
    throw new Error('Não foi possível preparar a imagem neste navegador.')
  }
  return { conteudo, nome: nomeEmJpeg(arquivo.name) }
}
