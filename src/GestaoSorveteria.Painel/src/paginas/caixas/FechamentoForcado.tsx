import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { CHAVE_CAIXAS, fecharCaixaForcado } from '../../api/caixas.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import type { CaixaResumoDto } from '../../api/tipos.ts'
import { formatarMoeda } from '../../formatacao.ts'

/**
 * RN-CX-09: a dona fecha pelo painel o caixa que o atendente esqueceu aberto. Em dois passos para não fechar por engano.
 * O valor contado fica 0 (ninguém contou a gaveta) e a diferença é gravada; a API faz toda a conta.
 */
export default function FechamentoForcado({ caixa }: { caixa: CaixaResumoDto }) {
  const queryClient = useQueryClient()
  const [aberto, setAberto] = useState(false)
  const [motivo, setMotivo] = useState('')

  const fechar = useMutation({
    mutationFn: () => fecharCaixaForcado(caixa.id, motivo.trim()),
    onSuccess: (detalhe) => {
      queryClient.setQueryData([...CHAVE_CAIXAS, detalhe.resumo.id], detalhe)
      void queryClient.invalidateQueries({ queryKey: CHAVE_CAIXAS, exact: true })
      void queryClient.invalidateQueries({ queryKey: ['relatorios'] })
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    fechar.mutate()
  }

  if (!aberto) {
    return (
      <section className="cartao">
        <p className="suave">O caixa é fechado normalmente no celular do balcão. Use esta opção só se o atendente esqueceu de fechar.</p>
        <button type="button" className="botao botao-secundario" onClick={() => setAberto(true)}>
          Fechar este caixa pelo painel
        </button>
      </section>
    )
  }

  return (
    <form className="cartao formulario" onSubmit={enviar}>
      <h2>Fechar o caixa pelo painel</h2>
      <p>
        O caixa fica fechado com valor contado <strong>{formatarMoeda(0)}</strong>, porque ninguém contou a gaveta.
        {caixa.valorEsperado !== null && (
          <>
            {' '}
            A diferença registrada será de <strong>{formatarMoeda(caixa.valorEsperado)}</strong> a menos
            {' '}(o que deveria ter na gaveta agora).
          </>
        )}
      </p>
      <p className="suave">
        Se o atendente ainda fechar o caixa no celular, esse fechamento será recusado e você acerta a diferença no próximo
        caixa. As vendas que ainda estiverem no celular chegam depois, marcadas para conferência.
      </p>
      <label className="campo">
        <span>Motivo</span>
        <textarea
          required
          minLength={3}
          maxLength={300}
          rows={3}
          value={motivo}
          onChange={(e) => setMotivo(e.target.value)}
          placeholder="Ex.: atendente esqueceu de fechar o caixa"
        />
      </label>
      {fechar.isError && <p className="alerta alerta-erro">{mensagemDoErro(fechar.error)}</p>}
      <div className="acoes">
        <button type="button" className="botao botao-secundario" disabled={fechar.isPending} onClick={() => setAberto(false)}>
          Voltar
        </button>
        <button type="submit" className="botao botao-perigo" disabled={fechar.isPending}>
          {fechar.isPending ? 'Fechando…' : 'Confirmar fechamento'}
        </button>
      </div>
    </form>
  )
}
