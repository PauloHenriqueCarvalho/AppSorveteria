import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { CHAVE_AVISOS, criarAviso } from '../../api/avisos.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { CatalogoEstoqueResponse, PrioridadeAviso } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina } from '../../componentes/Ui.tsx'
import { PRIORIDADES } from '../../estoque.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

const OUTRO = 'outro'

/** /avisos/novo — a dona também pode avisar (ex.: viu pelo balcão que acabou). Mesmas regras do tablet (RN-AV-01…03). */
export default function NovoAviso() {
  const catalogo = useCatalogoEstoque()
  return <Consulta consulta={catalogo}>{(dados) => <Formulario dados={dados} />}</Consulta>
}

function Formulario({ dados }: { dados: CatalogoEstoqueResponse }) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const [itemId, setItemId] = useState('')
  const [texto, setTexto] = useState('')
  const [quantidade, setQuantidade] = useState('')
  const [prioridade, setPrioridade] = useState<PrioridadeAviso | null>(null)
  const [observacao, setObservacao] = useState('')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const ativos = dados.itens.filter((i) => i.ativo)
  const sugestao: PrioridadeAviso | null = quantidade.trim() === '0' ? 'Urgente' : null // RN-AV-02: "acabou" já sugere urgente
  const escolhida = prioridade ?? sugestao

  const salvar = useMutation({
    mutationFn: criarAviso,
    onSuccess: async (resposta) => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_AVISOS })
      navegar(`/avisos/${resposta.aviso.id}`, { state: { jaExistia: resposta.jaExistia } })
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroCampo(null)
    const valor = Number(quantidade)
    if (quantidade.trim() === '' || !Number.isInteger(valor) || valor < 0) {
      setErroCampo('Informe quanto tem agora (0 = acabou).')
      return
    }
    if (!escolhida) {
      setErroCampo('Escolha a prioridade.')
      return
    }
    salvar.mutate({
      id: crypto.randomUUID(), // reenviar (ex.: clique duplo) não cria outro aviso
      itemEstoqueId: itemId === OUTRO ? null : itemId,
      textoLivre: itemId === OUTRO ? texto.trim() : null,
      quantidadeAtual: valor,
      prioridade: escolhida,
      observacao: observacao.trim() || null,
    })
  }

  return (
    <>
      <p className="voltar">
        <Link to="/avisos">← Avisos de estoque</Link>
      </p>
      <CabecalhoPagina titulo="Novo aviso" subtitulo="Se o item já tem aviso aberto, ele é atualizado em vez de repetir." />

      <form className="secao formulario" onSubmit={enviar}>
        <div className="grade-campos">
          <label className="campo">
            <span>O que está acabando</span>
            <select required value={itemId} onChange={(e) => setItemId(e.target.value)}>
              <option value="" disabled>
                Escolha o item
              </option>
              {dados.grupos.map((g) => {
                const itens = ativos.filter((i) => i.grupoId === g.id)
                return itens.length === 0 ? null : (
                  <optgroup key={g.id} label={g.nome}>
                    {itens.map((i) => (
                      <option key={i.id} value={i.id}>
                        {i.nome}
                      </option>
                    ))}
                  </optgroup>
                )
              })}
              <option value={OUTRO}>Outro (escrever)</option>
            </select>
          </label>

          {itemId === OUTRO ? (
            <label className="campo">
              <span>Qual</span>
              <input required minLength={2} maxLength={120} placeholder="Ex.: guardanapo" value={texto} onChange={(e) => setTexto(e.target.value)} />
            </label>
          ) : (
            <span aria-hidden="true" />
          )}

          <label className="campo">
            <span>Quanto tem agora</span>
            <div className="campo-com-botao">
              <input
                required
                type="number"
                inputMode="numeric"
                min={0}
                max={9999}
                step={1}
                value={quantidade}
                onChange={(e) => setQuantidade(e.target.value)}
              />
              <button type="button" className="botao botao-secundario" onClick={() => setQuantidade('0')}>
                Acabou
              </button>
            </div>
            <span className="suave dica">Em unidades: picolés, potes, baldes, copos.</span>
          </label>

          <label className="campo">
            <span>Observação (opcional)</span>
            <input maxLength={200} placeholder="Ex.: cliente pediu 3 vezes hoje" value={observacao} onChange={(e) => setObservacao(e.target.value)} />
          </label>
        </div>

        <fieldset className="campo">
          <legend>Prioridade</legend>
          <div className="opcoes">
            {PRIORIDADES.map((p) => (
              <label key={p.valor} className={p.valor === escolhida ? 'opcao opcao-ativa' : 'opcao'}>
                <input
                  type="radio"
                  name="prioridade"
                  value={p.valor}
                  checked={p.valor === escolhida}
                  onChange={() => setPrioridade(p.valor)}
                />
                <span>
                  <strong>{p.nome}</strong>
                  <span className="suave bloco">{p.quando}</span>
                  <span className="suave bloco">{p.chega}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>

        {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
        {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}

        <div className="rodape-formulario">
          <Link to="/avisos" className="botao botao-secundario">
            Cancelar
          </Link>
          <button type="submit" className="botao" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : 'Salvar aviso'}
          </button>
        </div>
      </form>
    </>
  )
}
