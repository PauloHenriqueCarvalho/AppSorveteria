interface Props {
  /** Quantas tentativas já falharam (0 = primeira ainda em andamento). */
  falhas?: number
}

/** Aviso enquanto a API acorda (Render Free leva ~1 min — docs/07). */
export default function ConectandoServidor({ falhas = 0 }: Props) {
  return (
    <div className="conectando" role="status" aria-live="polite">
      <span className="girando" aria-hidden="true" />
      <div>
        <strong>Conectando ao servidor…</strong>
        <p>
          Quando o sistema fica um tempo sem uso, ele leva até 1 minuto para acordar. Pode aguardar nesta tela.
        </p>
        {falhas > 0 && <p className="suave">Tentando de novo ({falhas + 1}ª tentativa)…</p>}
      </div>
    </div>
  )
}
