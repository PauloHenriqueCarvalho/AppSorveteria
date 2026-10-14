import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { alterarAtivoFornecedor, CHAVE_CATALOGO_ESTOQUE, salvarFornecedor } from '../../api/estoque.ts'
import { CHAVE_PEDIDOS, obterHistoricoFornecedor } from '../../api/pedidos.ts'
import type { CatalogoEstoqueResponse, DiaSemana, FormaEnvioPedido, FornecedorDto, SalvarFornecedorRequest } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Indicador, Secao } from '../../componentes/Ui.tsx'
import { DIAS, ENVIOS, formatarEmbalagens, formatarWhatsApp } from '../../estoque.ts'
import { formatarMoeda } from '../../formatacao.ts'
import { formatarPercentual } from '../../pedidos.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

/** /fornecedores/novo e /fornecedores/:id — RN-PF-01 e RN-PF-09. */
export default function EditarFornecedor() {
  const { id } = useParams()
  const catalogo = useCatalogoEstoque()
  return (
    <Consulta consulta={catalogo}>
      {(dados) => {
        const fornecedor = id ? dados.fornecedores.find((f) => f.id === id) : undefined
        if (id && !fornecedor) {
          return (
            <section className="secao">
              <p className="alerta alerta-erro">Fornecedor não encontrado.</p>
              <Link to="/fornecedores">Voltar para os fornecedores</Link>
            </section>
          )
        }
        return <Formulario key={fornecedor?.id ?? 'novo'} dados={dados} fornecedor={fornecedor} />
      }}
    </Consulta>
  )
}

function Formulario({ dados, fornecedor }: { dados: CatalogoEstoqueResponse; fornecedor?: FornecedorDto }) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const [nome, setNome] = useState(fornecedor?.nome ?? '')
  const [whatsApp, setWhatsApp] = useState(fornecedor?.whatsApp ? formatarWhatsApp(fornecedor.whatsApp) : '')
  const [dias, setDias] = useState<DiaSemana[]>(fornecedor?.diasPedido ?? [])
  const [prazo, setPrazo] = useState(fornecedor ? String(fornecedor.prazoEntregaDias) : '2')
  const [aceitaMeiaCaixa, setAceitaMeiaCaixa] = useState(fornecedor?.aceitaMeiaCaixa ?? false)
  const [envio, setEnvio] = useState<FormaEnvioPedido>(fornecedor?.envio ?? 'Impresso')
  const [mostrarPrecos, setMostrarPrecos] = useState(fornecedor?.mostrarPrecos ?? true)
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const itensAtivos = fornecedor ? dados.itens.filter((i) => i.fornecedorId === fornecedor.id && i.ativo).length : 0

  const salvar = useMutation({
    mutationFn: (pedido: SalvarFornecedorRequest) => salvarFornecedor(fornecedor?.id, pedido),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE })
      navegar('/fornecedores')
    },
  })

  const ativar = useMutation({
    mutationFn: (ativo: boolean) => alterarAtivoFornecedor(fornecedor!.id, ativo),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE }),
  })

  function alternarDia(dia: DiaSemana) {
    setDias((atuais) => (atuais.includes(dia) ? atuais.filter((d) => d !== dia) : [...atuais, dia]))
  }

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroCampo(null)
    if (dias.length === 0) {
      setErroCampo('Escolha pelo menos um dia da semana para o pedido.')
      return
    }
    const valorPrazo = Number(prazo)
    if (!Number.isInteger(valorPrazo) || valorPrazo < 0 || valorPrazo > 30) {
      setErroCampo('O prazo de entrega deve ser de 0 a 30 dias.')
      return
    }
    if (envio !== 'Impresso' && whatsApp.trim() === '') {
      setErroCampo('Para mandar o pedido pelo WhatsApp, informe o número do fornecedor.')
      return
    }
    salvar.mutate({
      nome: nome.trim(),
      whatsApp: whatsApp.trim() === '' ? null : whatsApp.trim(), // a API guarda só os dígitos e confere o tamanho
      diasPedido: dias,
      prazoEntregaDias: valorPrazo,
      aceitaMeiaCaixa,
      envio,
      mostrarPrecos,
    })
  }

  const ocupado = salvar.isPending || ativar.isPending

  return (
    <>
      <p className="voltar">
        <Link to="/fornecedores">← Fornecedores</Link>
      </p>
      <CabecalhoPagina
        titulo={fornecedor ? fornecedor.nome : 'Novo fornecedor'}
        subtitulo={
          fornecedor
            ? `${itensAtivos} ${itensAtivos === 1 ? 'item ativo' : 'itens ativos'} no catálogo`
            : 'Fábrica, distribuidora de descartáveis, casquinhas…'
        }
      />

      <form className="secao formulario" onSubmit={enviar}>
        {fornecedor && !fornecedor.ativo && (
          <p className="alerta alerta-aviso">Fornecedor desativado: não recebe pedido nem itens novos.</p>
        )}

        <div className="grade-campos">
          <label className="campo">
            <span>Nome</span>
            <input required minLength={2} maxLength={80} value={nome} onChange={(e) => setNome(e.target.value)} />
          </label>
          <label className="campo">
            <span>WhatsApp {envio === 'Impresso' ? '(opcional)' : ''}</span>
            <input
              type="tel"
              inputMode="tel"
              placeholder="(11) 98765-4321"
              value={whatsApp}
              onChange={(e) => setWhatsApp(e.target.value)}
            />
          </label>
        </div>

        <fieldset className="campo">
          <legend>Dias em que você faz o pedido</legend>
          <div className="chips" role="group">
            {DIAS.map((d) => (
              <button
                key={d.valor}
                type="button"
                className={dias.includes(d.valor) ? 'aba aba-ativa' : 'aba'}
                aria-pressed={dias.includes(d.valor)}
                title={d.nome}
                onClick={() => alternarDia(d.valor)}
              >
                {d.curto}
              </button>
            ))}
          </div>
          <span className="suave dica">No dia do pedido o painel monta o pedido sozinho; na véspera o tablet lembra da contagem.</span>
        </fieldset>

        <div className="grade-campos">
          <label className="campo">
            <span>Prazo de entrega (dias)</span>
            <input
              required
              type="number"
              inputMode="numeric"
              min={0}
              max={30}
              step={1}
              value={prazo}
              onChange={(e) => setPrazo(e.target.value)}
            />
          </label>
          <label className="campo">
            <span>Como o pedido é enviado</span>
            <select value={envio} onChange={(e) => setEnvio(e.target.value as FormaEnvioPedido)}>
              {ENVIOS.map((e) => (
                <option key={e.valor} value={e.valor}>
                  {e.nome}
                </option>
              ))}
            </select>
          </label>
        </div>

        <label className="campo-marcar">
          <input type="checkbox" checked={aceitaMeiaCaixa} onChange={(e) => setAceitaMeiaCaixa(e.target.checked)} />
          <span>
            Aceita meia caixa
            <span className="suave"> (o pedido sugerido arredonda para 0,5 em vez de caixa inteira)</span>
          </span>
        </label>
        <label className="campo-marcar">
          <input type="checkbox" checked={mostrarPrecos} onChange={(e) => setMostrarPrecos(e.target.checked)} />
          <span>
            Mostrar preços na folha impressa
            <span className="suave"> (desmarque se o fornecedor não precisa ver os valores)</span>
          </span>
        </label>

        {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
        {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}

        <div className="rodape-formulario">
          <Link to="/fornecedores" className="botao botao-secundario">
            Cancelar
          </Link>
          <button type="submit" className="botao" disabled={ocupado}>
            {salvar.isPending ? 'Salvando…' : 'Salvar'}
          </button>
        </div>
      </form>

      {fornecedor && <Historico fornecedorId={fornecedor.id} />}

      {fornecedor && (
        <Secao titulo={fornecedor.ativo ? 'Desativar' : 'Ativar'}>
          <p className="suave">
            {!fornecedor.ativo
              ? 'Ativar faz o fornecedor voltar a receber pedidos e itens novos.'
              : itensAtivos > 0
                ? `Ainda há ${itensAtivos} ${itensAtivos === 1 ? 'item ativo' : 'itens ativos'} deste fornecedor. Troque o fornecedor deles ou desative os itens no catálogo antes, para nenhum sumir do pedido sem você perceber.`
                : 'Desativar tira o fornecedor dos pedidos. O histórico continua.'}
          </p>
          {ativar.isError && <p className="alerta alerta-erro">{mensagemDoErro(ativar.error)}</p>}
          <button
            type="button"
            className="botao botao-secundario"
            disabled={ocupado || (fornecedor.ativo && itensAtivos > 0)}
            onClick={() => ativar.mutate(!fornecedor.ativo)}
          >
            {ativar.isPending ? 'Aguarde…' : fornecedor.ativo ? 'Desativar fornecedor' : 'Ativar fornecedor'}
          </button>
        </Secao>
      )}
    </>
  )
}

/** RN-RC-06: como o fornecedor tem atendido nos últimos 30 dias (valores calculados pela API). */
function Historico({ fornecedorId }: { fornecedorId: string }) {
  const historico = useQuery({
    queryKey: [...CHAVE_PEDIDOS, 'historico', fornecedorId],
    queryFn: () => obterHistoricoFornecedor(fornecedorId),
  })
  const h = historico.data
  if (!h) {
    return null
  }

  return (
    <Secao titulo="Últimos 30 dias">
      {h.pedidosRecebidos === 0 ? (
        <p className="suave">Nenhuma entrega conferida nos últimos 30 dias.</p>
      ) : (
        <>
          <div className="grade-indicadores">
            <Indicador rotulo="Entregas" valor={h.pedidosRecebidos} detalhe="conferidas" />
            <Indicador rotulo="Pedido" valor={formatarMoeda(h.valorPedido)} />
            <Indicador rotulo="Veio" valor={formatarMoeda(h.valorRecebido)} tom="marca" />
            <Indicador
              rotulo="Atendido"
              valor={formatarPercentual(h.percentualAtendidoMedio)}
              detalhe="média das entregas"
              tom={(h.percentualAtendidoMedio ?? 0) >= 95 ? 'bom' : 'atencao'}
            />
          </div>
          {h.itensQueMaisFaltam.length > 0 && (
            <>
              <h3 className="subtitulo">O que mais falta</h3>
              <ul className="lista-faltas">
                {h.itensQueMaisFaltam.map((f) => (
                  <li key={f.itemEstoqueId}>
                    <span>{f.descricao}</span>
                    <span className="suave">
                      faltou {f.vezesQueFaltou} {f.vezesQueFaltou === 1 ? 'vez' : 'vezes'} · {formatarEmbalagens(f.embalagensQueFaltaram)} no total
                    </span>
                  </li>
                ))}
              </ul>
            </>
          )}
        </>
      )}
    </Secao>
  )
}
