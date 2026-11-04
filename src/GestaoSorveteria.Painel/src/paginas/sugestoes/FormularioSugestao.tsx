import { useState, type FormEvent, type ReactNode } from 'react'
import type { AreaSugestao, ImportanciaSugestao, SalvarSugestaoRequest, TipoSugestao } from '../../api/sugestoes.ts'
import { AREAS, IMPORTANCIAS, TIPOS } from '../../sugestoes.ts'

/** Campos da sugestão (RN-SG-01): usado para criar e para editar. `extra` entra antes dos botões (ex.: imagens). */
export default function FormularioSugestao({
  inicial,
  salvando,
  erro,
  textoBotao,
  aoSalvar,
  aoCancelar,
  extra,
}: {
  inicial?: SalvarSugestaoRequest
  salvando: boolean
  erro: string | null
  textoBotao: string
  aoSalvar: (dados: SalvarSugestaoRequest) => void
  aoCancelar: () => void
  extra?: ReactNode
}) {
  const [tipo, setTipo] = useState<TipoSugestao | null>(inicial?.tipo ?? null)
  const [area, setArea] = useState<AreaSugestao | ''>(inicial?.area ?? '')
  const [importancia, setImportancia] = useState<ImportanciaSugestao>(inicial?.importancia ?? 'Media')
  const [titulo, setTitulo] = useState(inicial?.titulo ?? '')
  const [descricao, setDescricao] = useState(inicial?.descricao ?? '')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroCampo(null)
    if (!tipo) {
      setErroCampo('Escolha o tipo: ideia nova, mudança, problema ou dúvida.')
      return
    }
    if (!area) {
      setErroCampo('Escolha em que parte do sistema.')
      return
    }
    if (titulo.trim().length < 3) {
      setErroCampo('Escreva um título com pelo menos 3 letras.')
      return
    }
    if (descricao.trim().length === 0) {
      setErroCampo('Descreva a sugestão.')
      return
    }
    aoSalvar({ tipo, area, importancia, titulo: titulo.trim(), descricao: descricao.trim() })
  }

  return (
    <form className="secao formulario" onSubmit={enviar}>
      <fieldset className="campo">
        <legend>O que é</legend>
        <div className="opcoes">
          {TIPOS.map((t) => (
            <label key={t.valor} className={t.valor === tipo ? 'opcao opcao-ativa' : 'opcao'}>
              <input type="radio" name="tipo" value={t.valor} checked={t.valor === tipo} onChange={() => setTipo(t.valor)} />
              <span>
                <strong>{t.nome}</strong>
                <span className="suave bloco">{t.explicacao}</span>
              </span>
            </label>
          ))}
        </div>
      </fieldset>

      <div className="grade-campos">
        <label className="campo">
          <span>Em que parte do sistema</span>
          <select required value={area} onChange={(e) => setArea(e.target.value as AreaSugestao)}>
            <option value="" disabled>
              Escolha
            </option>
            {AREAS.map((a) => (
              <option key={a.valor} value={a.valor}>
                {a.nome}
              </option>
            ))}
          </select>
        </label>

        <label className="campo">
          <span>Título</span>
          <input
            required
            minLength={3}
            maxLength={120}
            placeholder="Ex.: Botão do picolé maior no balcão"
            value={titulo}
            onChange={(e) => setTitulo(e.target.value)}
          />
        </label>
      </div>

      <label className="campo">
        <span>Descreva com calma</span>
        <textarea
          required
          rows={7}
          maxLength={4000}
          placeholder={'O que acontece hoje, o que você gostaria que acontecesse e por quê.\nEx.: Quando chega fila, o atendente demora para achar o picolé. Queria o picolé como primeiro botão.'}
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
        />
        <span className="suave dica">{descricao.length} de 4000 letras. Pode colar mensagens do WhatsApp aqui.</span>
      </label>

      <fieldset className="campo">
        <legend>Importância para a loja</legend>
        <div className="opcoes">
          {IMPORTANCIAS.map((i) => (
            <label key={i.valor} className={i.valor === importancia ? 'opcao opcao-ativa' : 'opcao'}>
              <input
                type="radio"
                name="importancia"
                value={i.valor}
                checked={i.valor === importancia}
                onChange={() => setImportancia(i.valor)}
              />
              <span>
                <strong>{i.nome}</strong>
                <span className="suave bloco">{i.explicacao}</span>
              </span>
            </label>
          ))}
        </div>
      </fieldset>

      {extra}

      {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
      {erro && <p className="alerta alerta-erro">{erro}</p>}

      <div className="rodape-formulario">
        <button type="button" className="botao botao-secundario" onClick={aoCancelar}>
          Cancelar
        </button>
        <button type="submit" className="botao" disabled={salvando}>
          {salvando ? 'Salvando…' : textoBotao}
        </button>
      </div>
    </form>
  )
}
