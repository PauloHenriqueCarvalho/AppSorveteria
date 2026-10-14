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

const STATUS: Record<string, string> = {
  Aberta: 'Aberta',
  Fechada: 'Paga',
  Cancelada: 'Cancelada',
  Estornada: 'Estornada',
}

/** Situação da venda em linguagem da dona ("Fechada" → "Paga"). */
export function nomeDoStatus(status: string): string {
  return STATUS[status] ?? status
}

const diaExtenso = new Intl.DateTimeFormat('pt-BR', { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' })

/** Dia comercial "2026-10-14" → "terça-feira, 14 de outubro" (sem depender do fuso do aparelho). */
export function formatarDiaPorExtenso(dia: string): string {
  return diaExtenso.format(new Date(`${dia}T12:00:00Z`))
}

/** Primeiro nome para a saudação ("Dona Laura Silva" → "Laura"; "Laura" → "Laura"). */
export function primeiroNome(nome: string): string {
  const partes = nome.trim().split(/\s+/).filter((p) => !/^(dona|seu|sr\.?|sra\.?)$/i.test(p))
  return partes[0] ?? nome
}

/** Iniciais para o avatar ("Dona Laura" → "DL"). */
export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/)
  return ((partes[0]?.[0] ?? '') + (partes.length > 1 ? partes[partes.length - 1][0] : '')).toUpperCase()
}

const horaDoDia = new Intl.DateTimeFormat('pt-BR', { timeZone: FUSO_LOJA, hour: 'numeric', hourCycle: 'h23' })

/** "Bom dia" / "Boa tarde" / "Boa noite" pelo relógio da loja. */
export function saudacao(agora: Date = new Date()): string {
  const h = Number(horaDoDia.format(agora))
  return h < 12 ? 'Bom dia' : h < 18 ? 'Boa tarde' : 'Boa noite'
}

const percentual = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 1, maximumFractionDigits: 1 })

/** 12.4 → "+12,4%"; −3 → "−3,0%" (variação já calculada pela API). */
export function formatarVariacao(valor: number): string {
  const sinal = valor > 0 ? '+' : valor < 0 ? '−' : ''
  return `${sinal}${percentual.format(Math.abs(valor))}%`
}

/** Soma dias a um dia comercial: ("2026-10-14", −6) → "2026-10-08". */
export function somarDias(dia: string, dias: number): string {
  const data = new Date(`${dia}T12:00:00Z`)
  data.setUTCDate(data.getUTCDate() + dias)
  return data.toISOString().slice(0, 10)
}

/** Primeiro dia do mês: "2026-10-14" → "2026-10-01". */
export function inicioDoMes(dia: string): string {
  return `${dia.slice(0, 8)}01`
}
