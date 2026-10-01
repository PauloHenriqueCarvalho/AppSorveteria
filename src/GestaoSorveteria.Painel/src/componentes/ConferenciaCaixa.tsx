import type { CaixaResumoDto } from '../api/tipos.ts'
import { formatarMoeda } from '../formatacao.ts'

/**
 * "O caixa bateu?" em linguagem da dona. Só mostra os valores da API (RN-CX-06);
 * o sinal da diferença decide o texto: negativo = faltou dinheiro, positivo = sobrou.
 */
export function ResultadoCaixa({ caixa }: { caixa: CaixaResumoDto }) {
  if (caixa.status === 'Aberto' || caixa.diferenca === null) {
    return <span className="etiqueta etiqueta-info">Aberto</span>
  }
  if (caixa.diferenca === 0) {
    return <span className="etiqueta etiqueta-ok">Bateu</span>
  }
  const valor = formatarMoeda(Math.abs(caixa.diferenca))
  return caixa.diferenca < 0 ? (
    <span className="etiqueta etiqueta-erro">Faltaram {valor}</span>
  ) : (
    <span className="etiqueta etiqueta-aviso">Sobraram {valor}</span>
  )
}

/** Linhas da conferência: fundo, vendas em dinheiro, esperado, contado. */
export function ConferenciaCaixa({ caixa }: { caixa: CaixaResumoDto }) {
  const aberto = caixa.status === 'Aberto'
  return (
    <dl className="valores">
      <dt>Fundo de troco</dt>
      <dd>{formatarMoeda(caixa.fundoTroco)}</dd>
      {caixa.totalVendasDinheiro !== null && (
        <>
          <dt>Vendas em dinheiro{aberto ? ' (até agora)' : ''}</dt>
          <dd>{formatarMoeda(caixa.totalVendasDinheiro)}</dd>
        </>
      )}
      {caixa.valorEsperado !== null && (
        <>
          <dt>{aberto ? 'Deve ter na gaveta agora' : 'Deveria ter na gaveta'}</dt>
          <dd>{formatarMoeda(caixa.valorEsperado)}</dd>
        </>
      )}
      {caixa.valorContado !== null && (
        <>
          <dt>Contado no fechamento</dt>
          <dd>{formatarMoeda(caixa.valorContado)}</dd>
        </>
      )}
    </dl>
  )
}
