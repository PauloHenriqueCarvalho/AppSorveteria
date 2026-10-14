// Textos do estoque na linguagem da dona e leitura do arquivo da planilha. Nenhuma conta de dinheiro aqui (ADR 018).
import type { DiaSemana, FormaEnvioPedido, ModoControle, PrioridadeAviso, UnidadeCompra } from './api/tipos.ts'
import type { Tom } from './componentes/Ui.tsx'

export const MODOS: { valor: ModoControle; nome: string; explicacao: string }[] = [
  {
    valor: 'Contagem',
    nome: 'Contagem semanal',
    explicacao: 'Conta o que tem no freezer uma vez por semana (picolé, massa, açaí). É a partir dela que sai o pedido.',
  },
  {
    valor: 'SaldoPelaVenda',
    nome: 'Saldo pela venda',
    explicacao: 'O atendente escolhe o sabor na venda e o sistema desconta sozinho (teste dos potes). A contagem só confere.',
  },
  {
    valor: 'SoAviso',
    nome: 'Só aviso',
    explicacao: 'Não conta nem desconta: entra no pedido quando alguém avisa que está acabando (copos, casquinhas, insumos).',
  },
]

export function nomeDoModo(modo: ModoControle): string {
  return MODOS.find((m) => m.valor === modo)?.nome ?? modo
}

export const UNIDADES: { valor: UnidadeCompra; nome: string; variasPorEmbalagem: boolean }[] = [
  { valor: 'Caixa', nome: 'Caixa', variasPorEmbalagem: true },
  { valor: 'Balde', nome: 'Balde', variasPorEmbalagem: false },
  { valor: 'Unidade', nome: 'Unidade', variasPorEmbalagem: false },
  { valor: 'Pacote', nome: 'Pacote', variasPorEmbalagem: true },
]

/** ("Caixa", 28) → "Caixa com 28"; ("Balde", 1) → "Balde". */
export function descreverEmbalagem(unidade: UnidadeCompra, porEmbalagem: number): string {
  const nome = UNIDADES.find((u) => u.valor === unidade)?.nome ?? unidade
  return porEmbalagem > 1 ? `${nome} com ${porEmbalagem}` : nome
}

/** Semana começando na segunda, como a loja enxerga. */
export const DIAS: { valor: DiaSemana; curto: string; nome: string }[] = [
  { valor: 'Monday', curto: 'Seg', nome: 'Segunda' },
  { valor: 'Tuesday', curto: 'Ter', nome: 'Terça' },
  { valor: 'Wednesday', curto: 'Qua', nome: 'Quarta' },
  { valor: 'Thursday', curto: 'Qui', nome: 'Quinta' },
  { valor: 'Friday', curto: 'Sex', nome: 'Sexta' },
  { valor: 'Saturday', curto: 'Sáb', nome: 'Sábado' },
  { valor: 'Sunday', curto: 'Dom', nome: 'Domingo' },
]

/** ["Thursday", "Monday"] → "Seg e Qui"; um dia só sai por extenso ("Segunda"). */
export function descreverDias(dias: DiaSemana[]): string {
  const escolhidos = DIAS.filter((d) => dias.includes(d.valor))
  if (escolhidos.length === 1) {
    return escolhidos[0].nome
  }
  const curtos = escolhidos.map((d) => d.curto)
  return curtos.length <= 1 ? curtos.join('') : `${curtos.slice(0, -1).join(', ')} e ${curtos[curtos.length - 1]}`
}

export const ENVIOS: { valor: FormaEnvioPedido; nome: string }[] = [
  { valor: 'Impresso', nome: 'Impresso' },
  { valor: 'WhatsApp', nome: 'WhatsApp' },
  { valor: 'Ambos', nome: 'Impresso e WhatsApp' },
]

export function nomeDoEnvio(envio: FormaEnvioPedido): string {
  return ENVIOS.find((e) => e.valor === envio)?.nome ?? envio
}

/** "11987654321" → "(11) 98765-4321"; com o 55 do Brasil (12 ou 13 dígitos) → "+55 (11) 98765-4321". */
export function formatarWhatsApp(digitos: string): string {
  const comPais = digitos.length > 11
  const nacional = comPais ? digitos.slice(2) : digitos
  const ddd = nacional.slice(0, 2)
  const numero = nacional.slice(2)
  const meio = numero.length - 4
  return `${comPais ? `+${digitos.slice(0, 2)} ` : ''}(${ddd}) ${numero.slice(0, meio)}-${numero.slice(meio)}`
}

/** "2,5" / "2.5" / "3" → 2.5 / 3 (embalagens); null se não for número ≥ 0. A API confere o passo de meia caixa. */
export function lerQuantidade(texto: string): number | null {
  const limpo = texto.trim().replace(',', '.')
  if (!/^\d+(\.\d+)?$/.test(limpo)) {
    return null
  }
  return Number(limpo)
}

/** 2.5 → "2,5" para preencher o campo. */
export function quantidadeParaCampo(valor: number): string {
  return String(valor).replace('.', ',')
}

export const TAMANHO_MAXIMO_CSV = 1_000_000

/**
 * Bytes do arquivo → texto. Tenta UTF-8 (o "CSV UTF-8" do Excel e o arquivo do repositório); se não for, lê como
 * Windows-1252, que é o "CSV (separado por vírgulas)" comum do Excel no Windows. Assim os acentos chegam certos nos dois casos.
 */
export function decodificarCsv(bytes: ArrayBuffer): string {
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes)
  } catch {
    return new TextDecoder('windows-1252').decode(bytes)
  }
}

/** Cabeçalho que a importação entende (docs/fase2/catalogo-inicial.csv). */
export const COLUNAS_CSV = [
  'categoria',
  'sabor',
  'unidade_compra',
  'unidades_por_embalagem',
  'custo_embalagem',
  'fornecedor',
] as const

/** RN-AV-02: significado fixo da prioridade, mostrado na tela para não virar tudo urgente. */
export const PRIORIDADES: { valor: PrioridadeAviso; nome: string; quando: string; chega: string }[] = [
  { valor: 'Urgente', nome: 'Urgente', quando: 'Acabou, ou cliente pediu e não tinha', chega: 'Na hora, pelo WhatsApp' },
  { valor: 'Alta', nome: 'Alta', quando: 'Acaba hoje ou amanhã', chega: 'No resumo do fim do dia' },
  { valor: 'Media', nome: 'Média', quando: 'Está baixando; entra no próximo pedido', chega: 'No resumo do fim do dia' },
]

export const TOM_DA_PRIORIDADE: Record<PrioridadeAviso, Tom> = { Urgente: 'ruim', Alta: 'atencao', Media: 'neutro' }

export function nomeDaPrioridade(prioridade: PrioridadeAviso): string {
  return PRIORIDADES.find((p) => p.valor === prioridade)?.nome ?? prioridade
}

/** 0 → "Acabou"; 3 → "Restam 3". */
export function descreverQuantidade(quantidade: number): string {
  return quantidade === 0 ? 'Acabou' : `Restam ${quantidade}`
}

const diaDaLoja = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo', year: 'numeric', month: '2-digit', day: '2-digit' })
const horaDaLoja = new Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' })
const diaMesDaLoja = new Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo', day: '2-digit', month: '2-digit' })

/** Data/hora UTC da API → "hoje 15:42", "ontem 09:10" ou "12/10 14:00" (horário da loja). */
export function quando(isoUtc: string, agora: Date = new Date()): string {
  const data = new Date(isoUtc)
  const dia = diaDaLoja.format(data)
  const hoje = diaDaLoja.format(agora)
  const ontem = diaDaLoja.format(new Date(agora.getTime() - 24 * 60 * 60 * 1000))
  const hora = horaDaLoja.format(data)
  if (dia === hoje) {
    return `hoje ${hora}`
  }
  if (dia === ontem) {
    return `ontem ${hora}`
  }
  return `${diaMesDaLoja.format(data)} ${hora}`
}

/** Nome da embalagem para a quantidade: 1 ou meia → "caixa"; mais → "caixas" ("unid." não muda). */
export function nomeDaEmbalagem(unidade: UnidadeCompra, quantidade: number): string {
  const singular = { Caixa: 'caixa', Balde: 'balde', Unidade: 'unid.', Pacote: 'pacote' }[unidade]
  return quantidade <= 1 || unidade === 'Unidade' ? singular : `${singular}s`
}

/** Para títulos e rótulos sem quantidade: "caixas", "baldes", "unid.", "pacotes". */
export function embalagemNoPlural(unidade: UnidadeCompra): string {
  return nomeDaEmbalagem(unidade, 2)
}

/** 2.5 → "2,5"; null → "—". */
export function formatarEmbalagens(valor: number | null): string {
  return valor === null ? '—' : String(valor).replace('.', ',')
}

/** O valor respeita o passo (meia caixa só em caixa)? Evita mandar "1,5 balde" para a API. */
export function respeitaPasso(valor: number, passo: number): boolean {
  return Math.abs(valor / passo - Math.round(valor / passo)) < 1e-9
}
