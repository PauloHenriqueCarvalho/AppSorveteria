import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { Link } from 'react-router'
import { CHAVE_AVISOS, obterResumoDoDia } from '../../api/avisos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Secao } from '../../componentes/Ui.tsx'
import { formatarDiaPorExtenso, hojeNaLoja } from '../../formatacao.ts'

/**
 * /avisos/resumo — RN-AV-07. O tablet oferece o resumo no fechamento do caixa; quando o caixa é fechado pelo painel
 * (ou para rever outro dia) ele fica aqui, pronto para copiar ou abrir no WhatsApp.
 */
export default function ResumoDoDia() {
  const [dia, setDia] = useState(hojeNaLoja())
  const [copiado, setCopiado] = useState(false)
  const resumo = useQuery({ queryKey: [...CHAVE_AVISOS, 'resumo', dia], queryFn: () => obterResumoDoDia(dia) })

  async function copiar(texto: string) {
    try {
      await navigator.clipboard.writeText(texto)
      setCopiado(true)
      window.setTimeout(() => setCopiado(false), 2500)
    } catch {
      setCopiado(false)
    }
  }

  return (
    <>
      <p className="voltar">
        <Link to="/avisos">← Avisos de estoque</Link>
      </p>
      <CabecalhoPagina
        titulo="Resumo do dia"
        subtitulo={formatarDiaPorExtenso(dia)}
        acoes={
          <label className="campo-inline">
            Dia
            <input type="date" className="data-compacta" value={dia} max={hojeNaLoja()} onChange={(e) => e.target.value && setDia(e.target.value)} />
          </label>
        }
      />
      <Consulta consulta={resumo}>
        {(r) => (
          <Secao titulo="Mensagem para o WhatsApp">
            <pre className="mensagem-whatsapp">{r.texto}</pre>
            <div className="acoes acoes-inicio">
              <a className="botao" href={r.linkWhatsApp} target="_blank" rel="noreferrer">
                Abrir no WhatsApp
              </a>
              <button type="button" className="botao botao-secundario" onClick={() => void copiar(r.texto)}>
                {copiado ? 'Copiado' : 'Copiar texto'}
              </button>
            </div>
            <p className="suave dica">
              Vendas do dia contam só o que o tablet já enviou. Potes no mínimo e diferenças da contagem entram nas próximas etapas.
            </p>
          </Secao>
        )}
      </Consulta>
    </>
  )
}
