import type { ReactNode } from 'react'

// Peças visuais repetidas do painel (padrão de ERP). Só apresentam o que recebem:
// nenhum valor em dinheiro é calculado aqui (ADR 018).

/** Título da página, linha de contexto e ações à direita. */
export function CabecalhoPagina({
  titulo,
  subtitulo,
  acoes,
}: {
  titulo: ReactNode
  subtitulo?: ReactNode
  acoes?: ReactNode
}) {
  return (
    <div className="pagina-cabecalho">
      <div className="pagina-titulos">
        <h1>{titulo}</h1>
        {subtitulo && <p className="suave">{subtitulo}</p>}
      </div>
      {acoes && <div className="pagina-acoes">{acoes}</div>}
    </div>
  )
}

export type Tom = 'neutro' | 'bom' | 'atencao' | 'ruim' | 'marca'

/** Cartão de indicador: rótulo, número grande e uma linha de contexto. */
export function Indicador({
  rotulo,
  valor,
  detalhe,
  tom = 'neutro',
}: {
  rotulo: string
  valor: ReactNode
  detalhe?: ReactNode
  tom?: Tom
}) {
  return (
    <div className={`indicador indicador-${tom}`}>
      <span className="indicador-rotulo">{rotulo}</span>
      <strong className="indicador-valor">{valor}</strong>
      {detalhe && <span className="indicador-detalhe">{detalhe}</span>}
    </div>
  )
}

/** Bloco com título (e uma ação opcional à direita), como os painéis de um ERP. */
export function Secao({
  titulo,
  acao,
  children,
  className,
}: {
  titulo: ReactNode
  acao?: ReactNode
  children: ReactNode
  className?: string
}) {
  return (
    <section className={className ? `secao ${className}` : 'secao'}>
      <div className="secao-cabecalho">
        <h2>{titulo}</h2>
        {acao}
      </div>
      {children}
    </section>
  )
}

/** Selo de situação (Paga, Cancelada, Bateu, Faltaram…). */
export function Selo({ tom = 'neutro', children }: { tom?: Tom; children: ReactNode }) {
  return <span className={`selo selo-${tom}`}>{children}</span>
}

/** Barra horizontal proporcional (formas de pagamento, mais vendidos). `fracao` de 0 a 1. */
export function Barra({ fracao, tom = 'marca' }: { fracao: number; tom?: 'marca' | 'neutro' }) {
  const largura = `${Math.round(Math.min(1, Math.max(0, fracao)) * 100)}%`
  return (
    <span className="barra" aria-hidden="true">
      <span className={`barra-preenchida barra-${tom}`} style={{ width: largura }} />
    </span>
  )
}

/** Blocos cinza no lugar do conteúdo enquanto carrega. */
export function Esqueleto({ linhas = 3 }: { linhas?: number }) {
  return (
    <div className="esqueleto" aria-hidden="true">
      {Array.from({ length: linhas }, (_, i) => (
        <span key={i} className="esqueleto-linha" />
      ))}
    </div>
  )
}
