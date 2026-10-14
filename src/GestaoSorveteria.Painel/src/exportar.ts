// Exportação para Excel em CSV no padrão brasileiro: separador ";", vírgula decimal e BOM UTF-8
// (sem o BOM o Excel abre os acentos errados). Só formata o que a API devolveu: nada é recalculado (ADR 018).

export type Celula = string | number | null | undefined

const decimal = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2, useGrouping: false })

/** Valor em reais vindo da API → "1234,50" (número que o Excel BR reconhece e soma). */
export function celulaDinheiro(valor: number | null | undefined): Celula {
  return valor === null || valor === undefined ? '' : decimal.format(valor)
}

function escapar(celula: Celula): string {
  if (celula === null || celula === undefined) {
    return ''
  }
  const texto = String(celula)
  return /[";\r\n]/.test(texto) ? `"${texto.replace(/"/g, '""')}"` : texto
}

/** Monta o conteúdo do CSV (com BOM) a partir do cabeçalho e das linhas. */
export function montarCsv(cabecalho: string[], linhas: Celula[][]): string {
  const corpo = [cabecalho, ...linhas].map((linha) => linha.map(escapar).join(';')).join('\r\n')
  return `\uFEFF${corpo}\r\n`
}

/** Baixa o CSV no navegador com o nome informado (ex.: "vendas-2026-10-01-a-2026-10-14.csv"). */
export function baixarCsv(nomeArquivo: string, conteudo: string): void {
  const url = URL.createObjectURL(new Blob([conteudo], { type: 'text/csv;charset=utf-8' }))
  const link = document.createElement('a')
  link.href = url
  link.download = nomeArquivo
  document.body.append(link)
  link.click()
  link.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1_000)
}
