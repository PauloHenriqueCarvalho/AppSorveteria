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

const hora = new Intl.DateTimeFormat('pt-BR', { timeZone: FUSO_LOJA, hour: '2-digit', minute: '2-digit' })

// en-CA formata como "2026-09-30" (o formato que a API e o <input type="date"> usam).
const diaIso = new Intl.DateTimeFormat('en-CA', { timeZone: FUSO_LOJA, year: 'numeric', month: '2-digit', day: '2-digit' })

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

/** Data UTC da API → "15:05" no horário da loja. */
export function formatarHora(isoUtc: string): string {
  return hora.format(new Date(isoUtc))
}

/** Dia comercial da API ("2026-09-30", sem hora nem fuso) → "30/09/2026". */
export function formatarDia(dia: string): string {
  const [ano, mes, d] = dia.split('-')
  return `${d}/${mes}/${ano}`
}

/** Hoje no horário da loja, como "2026-09-30". */
export function hojeNaLoja(agora: Date = new Date()): string {
  return diaIso.format(agora)
}

const FORMAS: Record<string, string> = {
  Dinheiro: 'Dinheiro',
  Pix: 'Pix',
  CartaoDebito: 'Cartão de débito',
  CartaoCredito: 'Cartão de crédito',
}

/** "CartaoDebito" → "Cartão de débito". Desconhecida aparece como veio. */
export function nomeDaForma(forma: string): string {
  return FORMAS[forma] ?? forma
}
