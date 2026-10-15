import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router'
import { atualizarClima, CHAVE_CLIMA, definirLocal, obterSituacaoClima, procurarLocal } from '../../api/clima.ts'
import { mensagemDoErro } from '../../api/erros.ts'
import { CHAVE_RELATORIOS } from '../../api/relatorios.ts'
import type { LocalLojaDto, SituacaoClimaDto } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import Icone from '../../componentes/Icone.tsx'
import { Secao } from '../../componentes/Ui.tsx'
import { formatarDataHora, formatarDia } from '../../formatacao.ts'

const CHAVE_SITUACAO = [...CHAVE_CLIMA, 'situacao'] as const

const coordenada = new Intl.NumberFormat('pt-BR', { minimumFractionDigits: 4, maximumFractionDigits: 4 })

function descreverCoordenadas(local: LocalLojaDto): string {
  return `${coordenada.format(local.latitude)}; ${coordenada.format(local.longitude)}`
}

/**
 * RN-CL-01/04: a cidade da loja, para guardar o clima de cada dia ao lado das vendas. A dona escreve, confere o lugar que o
 * serviço de clima encontrou e confirma; a API já busca o histórico desde a primeira venda e a previsão.
 */
export default function LocalClima() {
  const situacao = useQuery({ queryKey: CHAVE_SITUACAO, queryFn: obterSituacaoClima })
  return (
    <Secao titulo="Local da loja (clima)">
      <Consulta consulta={situacao}>{(dados) => <Conteudo situacao={dados} />}</Consulta>
    </Secao>
  )
}

function Conteudo({ situacao }: { situacao: SituacaoClimaDto }) {
  const queryClient = useQueryClient()
  const [texto, setTexto] = useState('')

  const procurar = useMutation({ mutationFn: procurarLocal })
  const atualizarTudo = async (nova?: SituacaoClimaDto) => {
    if (nova) {
      queryClient.setQueryData(CHAVE_SITUACAO, nova)
    } else {
      await queryClient.invalidateQueries({ queryKey: CHAVE_SITUACAO })
    }
    await queryClient.invalidateQueries({ queryKey: [...CHAVE_RELATORIOS, 'clima-vendas'] })
  }
  const confirmar = useMutation({
    mutationFn: definirLocal,
    onSuccess: async (nova) => {
      procurar.reset()
      setTexto('')
      await atualizarTudo(nova)
    },
  })
  const atualizar = useMutation({ mutationFn: atualizarClima, onSuccess: () => atualizarTudo() })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    confirmar.reset()
    procurar.mutate(texto.trim())
  }

  const encontrado = procurar.data
  return (
    <div className="formulario">
      <p className="suave">
        O sistema guarda o clima de cada dia ao lado das vendas, para mostrar em <Link to="/clima-vendas">Clima × vendas</Link> como o
        calor e a chuva mudam o movimento — e, com alguns meses de dados, estimar quanto vender e quanto pedir.
      </p>

      {!situacao.chaveConfigurada && (
        <p className="alerta alerta-aviso">
          A coleta do clima ainda não foi ligada no servidor: falta configurar a chave do serviço de clima (é grátis). Fale com quem
          cuida do sistema.
        </p>
      )}

      {situacao.local ? (
        <p className="local-atual">
          <Icone nome="local" />
          <span>
            <strong>{situacao.local.nome}</strong>
            <span className="suave bloco pequeno">{descreverCoordenadas(situacao.local)}</span>
          </span>
        </p>
      ) : (
        <p className="alerta alerta-aviso">Nenhum local informado: o clima ainda não está sendo guardado.</p>
      )}

      <form className="linha-busca" onSubmit={enviar}>
        <label className="campo">
          <span>{situacao.local ? 'Mudar o local' : 'Cidade e estado da loja'}</span>
          <input
            required
            minLength={2}
            maxLength={120}
            placeholder="Ribeirão Preto, SP"
            value={texto}
            onChange={(e) => setTexto(e.target.value)}
            disabled={!situacao.chaveConfigurada}
          />
        </label>
        <button type="submit" className="botao botao-secundario" disabled={!situacao.chaveConfigurada || procurar.isPending}>
          {procurar.isPending ? 'Procurando…' : 'Procurar'}
        </button>
      </form>

      {procurar.isError && <p className="alerta alerta-erro">{mensagemDoErro(procurar.error)}</p>}

      {encontrado && (
        <div className="cartao-confirmacao">
          <p>
            Encontramos: <strong>{encontrado.nome}</strong> <span className="suave">({descreverCoordenadas(encontrado)})</span>
          </p>
          <p className="suave pequeno">
            Confira se é a cidade da loja. Ao confirmar, o sistema busca o clima desde a primeira venda e a previsão dos próximos dias
            (pode levar alguns segundos).
          </p>
          <div className="rodape-formulario">
            <button type="button" className="botao botao-secundario" onClick={() => procurar.reset()} disabled={confirmar.isPending}>
              Não é esse
            </button>
            <button
              type="button"
              className="botao"
              onClick={() => confirmar.mutate({ nome: encontrado.nome, latitude: encontrado.latitude, longitude: encontrado.longitude })}
              disabled={confirmar.isPending}
            >
              {confirmar.isPending ? 'Buscando o clima…' : 'Confirmar este local'}
            </button>
          </div>
        </div>
      )}
      {confirmar.isError && <p className="alerta alerta-erro">{mensagemDoErro(confirmar.error)}</p>}

      {situacao.local && (
        <div className="situacao-clima">
          <p>
            {situacao.diasComClima > 0 && situacao.primeiroDia && situacao.ultimoDia ? (
              <>
                Clima guardado de <strong>{formatarDia(situacao.primeiroDia)}</strong> a <strong>{formatarDia(situacao.ultimoDia)}</strong> (
                {situacao.diasComClima} {situacao.diasComClima === 1 ? 'dia' : 'dias'}).
              </>
            ) : (
              'Nenhum dia de clima guardado ainda.'
            )}{' '}
            {situacao.ultimaColetaEm && <span className="suave">Última coleta: {formatarDataHora(situacao.ultimaColetaEm)}.</span>}
          </p>
          <p className="suave pequeno">O sistema busca sozinho a cada 3 horas quando está ligado; se ficou parado, completa os dias que faltaram.</p>
          {situacao.ultimoErro && <p className="alerta alerta-erro">Última tentativa: {situacao.ultimoErro}</p>}
          {atualizar.isSuccess && (
            <p className={atualizar.data.falhou ? 'alerta alerta-erro' : 'alerta alerta-sucesso'}>{atualizar.data.mensagem}</p>
          )}
          {atualizar.isError && <p className="alerta alerta-erro">{mensagemDoErro(atualizar.error)}</p>}
          <div className="rodape-formulario">
            <a className="suave pequeno" href={situacao.atribuicaoLink} target="_blank" rel="noreferrer">
              {situacao.atribuicao}
            </a>
            <button
              type="button"
              className="botao botao-secundario"
              onClick={() => atualizar.mutate()}
              disabled={!situacao.chaveConfigurada || atualizar.isPending}
            >
              {atualizar.isPending ? 'Atualizando…' : 'Atualizar agora'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
