import { hojeNaLoja, inicioDoMes, somarDias } from '../formatacao.ts'

export interface Periodo {
  de: string
  ate: string
}

export interface Atalho {
  texto: string
  periodo: (hoje: string) => Periodo
}

const ATALHOS: Atalho[] = [
  { texto: 'Hoje', periodo: (hoje) => ({ de: hoje, ate: hoje }) },
  { texto: 'Ontem', periodo: (hoje) => ({ de: somarDias(hoje, -1), ate: somarDias(hoje, -1) }) },
  { texto: '7 dias', periodo: (hoje) => ({ de: somarDias(hoje, -6), ate: hoje }) },
  { texto: '30 dias', periodo: (hoje) => ({ de: somarDias(hoje, -29), ate: hoje }) },
  { texto: 'Este mês', periodo: (hoje) => ({ de: inicioDoMes(hoje), ate: hoje }) },
]

/** De / Até com atalhos. Os dias são do calendário da loja ("2026-10-14"); a API limita o tamanho (93 dias nas listas). */
export default function FiltroPeriodo({
  periodo,
  aoMudar,
  atalhos = ATALHOS,
}: {
  periodo: Periodo
  aoMudar: (p: Periodo) => void
  atalhos?: Atalho[]
}) {
  const hoje = hojeNaLoja()
  return (
    <>
      <label className="campo-inline">
        <span>De</span>
        <input
          type="date"
          value={periodo.de}
          max={periodo.ate || hoje}
          onChange={(e) => aoMudar({ ...periodo, de: e.target.value || periodo.de })}
        />
      </label>
      <label className="campo-inline">
        <span>Até</span>
        <input
          type="date"
          value={periodo.ate}
          min={periodo.de}
          max={hoje}
          onChange={(e) => aoMudar({ ...periodo, ate: e.target.value || periodo.ate })}
        />
      </label>
      <div className="abas" role="group" aria-label="Atalhos de período">
        {atalhos.map((a) => {
          const p = a.periodo(hoje)
          const ativo = p.de === periodo.de && p.ate === periodo.ate
          return (
            <button
              key={a.texto}
              type="button"
              className={ativo ? 'aba aba-ativa' : 'aba'}
              aria-pressed={ativo}
              onClick={() => aoMudar(p)}
            >
              {a.texto}
            </button>
          )
        })}
      </div>
    </>
  )
}
