import { useEffect, useMemo } from 'react'
import { formatarTamanho, IMAGENS_MAXIMO } from '../../sugestoes.ts'

/** RN-SG-05: escolhe imagens (print, foto) com prévia antes de enviar. Aceita colar um print (Ctrl+V). */
export default function EscolherImagens({
  arquivos,
  aoMudar,
  jaAnexadas = 0,
}: {
  arquivos: File[]
  aoMudar: (arquivos: File[]) => void
  jaAnexadas?: number
}) {
  const vagas = IMAGENS_MAXIMO - jaAnexadas - arquivos.length
  const previas = useMemo(() => arquivos.map((a) => URL.createObjectURL(a)), [arquivos])
  useEffect(() => () => previas.forEach((url) => URL.revokeObjectURL(url)), [previas])

  // Colar um print da tela direto na página
  useEffect(() => {
    function colar(evento: ClipboardEvent) {
      const imagens = [...(evento.clipboardData?.files ?? [])].filter((f) => f.type.startsWith('image/'))
      if (imagens.length > 0 && vagas > 0) {
        evento.preventDefault()
        aoMudar([...arquivos, ...imagens.slice(0, vagas)])
      }
    }
    window.addEventListener('paste', colar)
    return () => window.removeEventListener('paste', colar)
  }, [arquivos, aoMudar, vagas])

  return (
    <div className="campo">
      <span>Imagens (opcional)</span>
      <span className="suave dica">
        Um print da tela ou uma foto ajuda a entender. Até {IMAGENS_MAXIMO} por sugestão; fotos grandes são reduzidas sozinhas. No
        computador, dá para colar um print com Ctrl+V.
      </span>
      {arquivos.length > 0 && (
        <ul className="sugestao-imagens">
          {arquivos.map((arquivo, i) => (
            <li key={`${arquivo.name}-${i}`} className="sugestao-imagem">
              <img src={previas[i]} alt={arquivo.name} />
              <span className="suave">
                {arquivo.name} · {formatarTamanho(arquivo.size)}
              </span>
              <button type="button" className="botao botao-secundario botao-pequeno" onClick={() => aoMudar(arquivos.filter((_, j) => j !== i))}>
                Tirar
              </button>
            </li>
          ))}
        </ul>
      )}
      {vagas > 0 ? (
        <label className="botao botao-secundario botao-arquivo">
          Escolher imagem
          <input
            type="file"
            accept="image/*"
            multiple
            onChange={(e) => {
              const escolhidas = [...(e.target.files ?? [])].slice(0, vagas)
              e.target.value = ''
              aoMudar([...arquivos, ...escolhidas])
            }}
          />
        </label>
      ) : (
        <span className="suave dica">Limite de {IMAGENS_MAXIMO} imagens atingido.</span>
      )}
    </div>
  )
}
