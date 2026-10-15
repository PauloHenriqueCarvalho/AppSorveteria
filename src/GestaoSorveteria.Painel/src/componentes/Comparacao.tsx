import { formatarVariacao } from '../formatacao.ts'

/** "▲ +12,4% vs. 07/10 no mesmo horário". Variação calculada pela API (RN-RL-07); sem base → só o contexto. */
export default function Comparacao({ variacao, contexto, anterior }: { variacao: number | null; contexto: string; anterior: string }) {
  if (variacao === null) {
    return <span>sem vendas para comparar {contexto.replace('vs.', 'em')}</span>
  }
  const classe = variacao > 0 ? 'variacao variacao-sobe' : variacao < 0 ? 'variacao variacao-desce' : 'variacao'
  const seta = variacao > 0 ? '▲' : variacao < 0 ? '▼' : '='
  return (
    <span title={`Antes: ${anterior}`}>
      <span className={classe}>
        <span aria-hidden="true">{seta}</span> {formatarVariacao(variacao)}
      </span>{' '}
      {contexto}
    </span>
  )
}
