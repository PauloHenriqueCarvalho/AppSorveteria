// O painel nunca calcula dinheiro: só formata o que a API devolve (ADR 018).

const FUSO_LOJA = 'America/Sao_Paulo'

const moeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

const dataHora = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO_LOJA,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

const data = new Intl.DateTimeFormat('pt-BR', {
  timeZone: FUSO_LOJA,
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
})

/** 12.5 → "R$ 12,50". */
export function formatarMoeda(valor: number): string {
  return moeda.format(valor)
}

/** Data/hora UTC da API ("2026-09-30T18:05:00Z") → "30/09/2026, 15:05" no horário da loja. */
export function formatarDataHora(isoUtc: string): string {
  return dataHora.format(new Date(isoUtc))
}

/** Data UTC da API → "30/09/2026" no horário da loja. */
export function formatarData(isoUtc: string): string {
  return data.format(new Date(isoUtc))
}
