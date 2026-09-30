// Conversão do que a dona digita. Não calcula nada: só lê o número como ela escreveu.

const PRECO_MAXIMO = 99_999.99

/**
 * "12,50", "12.5", "R$ 1.234,56" → 12.5 / 1234.56. Aceita no máximo 2 casas decimais.
 * Devolve null quando o texto não é um preço válido (vazio, letras, 3 casas, negativo, acima do máximo).
 */
export function lerPreco(texto: string): number | null {
  const limpo = texto.replace(/R\$/i, '').replace(/\s/g, '')

  let numero: string
  if (/^\d+([.,]\d{1,2})?$/.test(limpo)) {
    // "12", "12,5", "12.50": vírgula ou ponto como casa decimal.
    numero = limpo.replace(',', '.')
  } else if (/^\d{1,3}(\.\d{3})+(,\d{1,2})?$/.test(limpo)) {
    // "1.234" ou "1.234,56": ponto como separador de milhar.
    numero = limpo.replace(/\./g, '').replace(',', '.')
  } else {
    return null
  }

  const valor = Number(numero)
  return valor <= PRECO_MAXIMO ? valor : null
}

/** 12.5 → "12,50" para preencher o campo ao editar. */
export function precoParaCampo(valor: number): string {
  return valor.toFixed(2).replace('.', ',')
}
