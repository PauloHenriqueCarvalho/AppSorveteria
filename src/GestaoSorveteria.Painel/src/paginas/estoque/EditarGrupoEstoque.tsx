import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { CHAVE_CATALOGO_ESTOQUE, salvarGrupoEstoque } from '../../api/estoque.ts'
import type { CatalogoEstoqueResponse, GrupoEstoqueDto, ModoControle, SalvarGrupoEstoqueRequest } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina } from '../../componentes/Ui.tsx'
import { MODOS } from '../../estoque.ts'
import { useCatalogoEstoque } from './useCatalogoEstoque.ts'

/** /estoque/grupos/novo e /estoque/grupos/:id — RN-ES-02 (o modo de controle é do grupo). */
export default function EditarGrupoEstoque() {
  const { id } = useParams()
  const catalogo = useCatalogoEstoque()
  return (
    <Consulta consulta={catalogo}>
      {(dados) => {
        const grupo = id ? dados.grupos.find((g) => g.id === id) : undefined
        if (id && !grupo) {
          return (
            <section className="secao">
              <p className="alerta alerta-erro">Grupo não encontrado.</p>
              <Link to="/estoque">Voltar para o catálogo</Link>
            </section>
          )
        }
        return <Formulario key={grupo?.id ?? 'novo'} dados={dados} grupo={grupo} />
      }}
    </Consulta>
  )
}

function Formulario({ dados, grupo }: { dados: CatalogoEstoqueResponse; grupo?: GrupoEstoqueDto }) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const [nome, setNome] = useState(grupo?.nome ?? '')
  const [modo, setModo] = useState<ModoControle>(grupo?.modoControle ?? 'Contagem')
  const [ordem, setOrdem] = useState(grupo ? String(grupo.ordem) : '')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const proximaOrdem = dados.grupos.reduce((maior, g) => Math.max(maior, g.ordem + 1), 1)
  const itensDoGrupo = grupo ? dados.itens.filter((i) => i.grupoId === grupo.id && i.ativo).length : 0

  const salvar = useMutation({
    mutationFn: (pedido: SalvarGrupoEstoqueRequest) => salvarGrupoEstoque(grupo?.id, pedido),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE })
      navegar('/estoque')
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    const posicao = ordem.trim() === '' ? proximaOrdem : Number(ordem)
    if (!Number.isInteger(posicao) || posicao < 0) {
      setErroCampo('A posição deve ser um número inteiro, 0 ou maior.')
      return
    }
    setErroCampo(null)
    salvar.mutate({ nome: nome.trim(), modoControle: modo, ordem: posicao })
  }

  return (
    <>
      <p className="voltar">
        <Link to="/estoque">← Catálogo de estoque</Link>
      </p>
      <CabecalhoPagina
        titulo={grupo ? `Grupo ${grupo.nome}` : 'Novo grupo'}
        subtitulo={grupo ? `${itensDoGrupo} ${itensDoGrupo === 1 ? 'item ativo' : 'itens ativos'}` : 'Como uma seção da planilha: Picolé de fruta, Massa, Copos…'}
      />

      <form className="secao formulario" onSubmit={enviar}>
        <div className="grade-campos">
          <label className="campo">
            <span>Nome do grupo</span>
            <input required minLength={2} maxLength={40} value={nome} onChange={(e) => setNome(e.target.value)} />
          </label>
          <label className="campo">
            <span>Posição</span>
            <input
              type="number"
              inputMode="numeric"
              min={0}
              step={1}
              placeholder={String(proximaOrdem)}
              value={ordem}
              onChange={(e) => setOrdem(e.target.value)}
            />
            <span className="suave dica">Ordem do grupo na contagem e na folha do pedido.</span>
          </label>
        </div>

        <fieldset className="campo">
          <legend>Como controlar</legend>
          <div className="opcoes">
            {MODOS.map((m) => (
              <label key={m.valor} className={m.valor === modo ? 'opcao opcao-ativa' : 'opcao'}>
                <input type="radio" name="modo" value={m.valor} checked={m.valor === modo} onChange={() => setModo(m.valor)} />
                <span>
                  <strong>{m.nome}</strong>
                  <span className="suave bloco">{m.explicacao}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>

        {grupo && grupo.modoControle !== modo && (
          <p className="alerta alerta-aviso">
            A troca vale a partir de agora. As contagens e os avisos que já existem continuam no histórico.
          </p>
        )}
        {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
        {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}

        <div className="rodape-formulario">
          <Link to="/estoque" className="botao botao-secundario">
            Cancelar
          </Link>
          <button type="submit" className="botao" disabled={salvar.isPending}>
            {salvar.isPending ? 'Salvando…' : 'Salvar'}
          </button>
        </div>
      </form>
    </>
  )
}
