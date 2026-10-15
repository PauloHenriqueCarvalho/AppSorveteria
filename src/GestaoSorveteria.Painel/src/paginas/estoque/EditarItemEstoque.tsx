import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import { alterarAtivoItemEstoque, CHAVE_CATALOGO_ESTOQUE, salvarItemEstoque } from '../../api/estoque.ts'
import type { CatalogoEstoqueResponse, ItemEstoqueDto, SalvarItemEstoqueRequest, UnidadeCompra } from '../../api/tipos.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Secao } from '../../componentes/Ui.tsx'
import { lerPreco, precoParaCampo } from '../../entrada.ts'
import { lerQuantidade, quantidadeParaCampo, UNIDADES } from '../../estoque.ts'
import { useCatalogoEstoque } from './useCatalogoEstoque.ts'

/** /estoque/itens/novo (?grupo=) e /estoque/itens/:id — RN-ES-01. */
export default function EditarItemEstoque() {
  const { id } = useParams()
  const catalogo = useCatalogoEstoque()
  return (
    <Consulta consulta={catalogo}>
      {(dados) => {
        const item = id ? dados.itens.find((i) => i.id === id) : undefined
        if (id && !item) {
          return (
            <section className="secao">
              <p className="alerta alerta-erro">Item não encontrado.</p>
              <Link to="/estoque">Voltar para o catálogo</Link>
            </section>
          )
        }
        return <Formulario key={item?.id ?? 'novo'} dados={dados} item={item} />
      }}
    </Consulta>
  )
}

function Formulario({ dados, item }: { dados: CatalogoEstoqueResponse; item?: ItemEstoqueDto }) {
  const navegar = useNavigate()
  const queryClient = useQueryClient()
  const [parametros] = useSearchParams()

  const grupoInicial = item?.grupoId ?? (dados.grupos.some((g) => g.id === parametros.get('grupo')) ? parametros.get('grupo')! : '')
  const fornecedoresAtivos = dados.fornecedores.filter((f) => f.ativo || f.id === item?.fornecedorId)

  const [grupoId, setGrupoId] = useState(grupoInicial)
  const [nome, setNome] = useState(item?.nome ?? '')
  const [fornecedorId, setFornecedorId] = useState(item?.fornecedorId ?? (fornecedoresAtivos.length === 1 ? fornecedoresAtivos[0].id : ''))
  const [unidade, setUnidade] = useState<UnidadeCompra>(item?.unidade ?? 'Caixa')
  const [porEmbalagem, setPorEmbalagem] = useState(item ? String(item.unidadesPorEmbalagem) : '')
  const [custo, setCusto] = useState(item ? precoParaCampo(item.custoEmbalagem) : '')
  const [minimo, setMinimo] = useState(item?.minimoUnidades != null ? String(item.minimoUnidades) : '')
  const [alvo, setAlvo] = useState(item?.estoqueAlvoEmbalagens != null ? quantidadeParaCampo(item.estoqueAlvoEmbalagens) : '')
  const [ordem, setOrdem] = useState(item ? String(item.ordem) : '')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const variasPorEmbalagem = UNIDADES.find((u) => u.valor === unidade)?.variasPorEmbalagem ?? false
  const proximaOrdem = dados.itens.filter((i) => i.grupoId === grupoId).reduce((maior, i) => Math.max(maior, i.ordem + 1), 1)

  const salvar = useMutation({
    mutationFn: (pedido: SalvarItemEstoqueRequest) => salvarItemEstoque(item?.id, pedido),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE })
      navegar('/estoque')
    },
  })

  const ativar = useMutation({
    mutationFn: (ativo: boolean) => alterarAtivoItemEstoque(item!.id, ativo),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: CHAVE_CATALOGO_ESTOQUE }),
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroCampo(null)

    const valorCusto = lerPreco(custo)
    if (valorCusto === null) {
      setErroCampo('Custo inválido. Escreva só o valor da embalagem, por exemplo 25,20.')
      return
    }
    const unidades = variasPorEmbalagem ? Number(porEmbalagem) : 1
    if (!Number.isInteger(unidades) || unidades < 1) {
      setErroCampo(`Informe quantas unidades vêm em cada ${unidade.toLowerCase()} (número inteiro).`)
      return
    }
    const valorMinimo = minimo.trim() === '' ? null : Number(minimo)
    if (valorMinimo !== null && (!Number.isInteger(valorMinimo) || valorMinimo < 0)) {
      setErroCampo('O mínimo deve ser um número inteiro de unidades, 0 ou maior.')
      return
    }
    const valorAlvo = alvo.trim() === '' ? null : lerQuantidade(alvo)
    if (alvo.trim() !== '' && valorAlvo === null) {
      setErroCampo('Estoque alvo inválido. Use, por exemplo, 3 ou 2,5.')
      return
    }
    const posicao = ordem.trim() === '' ? proximaOrdem : Number(ordem)
    if (!Number.isInteger(posicao) || posicao < 0) {
      setErroCampo('A posição deve ser um número inteiro, 0 ou maior.')
      return
    }

    salvar.mutate({
      grupoId,
      fornecedorId,
      nome: nome.trim(),
      unidade,
      unidadesPorEmbalagem: unidades,
      custoEmbalagem: valorCusto,
      ordem: posicao,
      estoqueAlvoEmbalagens: valorAlvo,
      minimoUnidades: valorMinimo,
    })
  }

  const ocupado = salvar.isPending || ativar.isPending
  const fornecedorDesativado = item && !dados.fornecedores.find((f) => f.id === item.fornecedorId)?.ativo

  return (
    <>
      <p className="voltar">
        <Link to="/estoque">← Catálogo de estoque</Link>
      </p>
      <CabecalhoPagina
        titulo={item ? item.nome : 'Novo item'}
        subtitulo={item ? `${item.grupoNome} · ${item.fornecedorNome}` : 'Sabor da fábrica, copo, casquinha ou insumo'}
      />

      {dados.grupos.length === 0 || fornecedoresAtivos.length === 0 ? (
        <section className="secao">
          <p className="alerta alerta-aviso">
            Para cadastrar um item é preciso ter pelo menos um grupo e um fornecedor ativo.
          </p>
          <div className="acoes acoes-inicio">
            <Link to="/estoque/grupos/novo" className="botao botao-secundario">
              Novo grupo
            </Link>
            <Link to="/fornecedores/novo" className="botao botao-secundario">
              Novo fornecedor
            </Link>
          </div>
        </section>
      ) : (
        <form className="secao formulario" onSubmit={enviar}>
          {item && !item.ativo && (
            <p className="alerta alerta-aviso">Este item está desativado: não entra na contagem nem no pedido.</p>
          )}

          <div className="grade-campos">
            <label className="campo">
              <span>Grupo</span>
              <select required value={grupoId} onChange={(e) => setGrupoId(e.target.value)}>
                <option value="" disabled>
                  Escolha o grupo
                </option>
                {dados.grupos.map((g) => (
                  <option key={g.id} value={g.id}>
                    {g.nome}
                  </option>
                ))}
              </select>
            </label>

            <label className="campo">
              <span>Sabor ou nome do item</span>
              <input required maxLength={80} value={nome} onChange={(e) => setNome(e.target.value)} />
            </label>

            <label className="campo">
              <span>Fornecedor</span>
              <select required value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}>
                <option value="" disabled>
                  Escolha o fornecedor
                </option>
                {fornecedoresAtivos.map((f) => (
                  <option key={f.id} value={f.id}>
                    {f.nome}
                    {f.ativo ? '' : ' (desativado)'}
                  </option>
                ))}
              </select>
            </label>

            <label className="campo">
              <span>Comprado por</span>
              <select value={unidade} onChange={(e) => setUnidade(e.target.value as UnidadeCompra)}>
                {UNIDADES.map((u) => (
                  <option key={u.valor} value={u.valor}>
                    {u.nome}
                  </option>
                ))}
              </select>
            </label>

            {variasPorEmbalagem && (
              <label className="campo">
                <span>Unidades em cada {unidade.toLowerCase()}</span>
                <input
                  required
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={1000}
                  step={1}
                  placeholder="28"
                  value={porEmbalagem}
                  onChange={(e) => setPorEmbalagem(e.target.value)}
                />
              </label>
            )}

            <label className="campo">
              <span>Custo de cada {unidade.toLowerCase()} (R$)</span>
              <input required inputMode="decimal" placeholder="0,00" value={custo} onChange={(e) => setCusto(e.target.value)} />
            </label>

            <label className="campo">
              <span>Estoque alvo ({unidade === 'Caixa' ? 'caixas' : 'embalagens'}, opcional)</span>
              <input inputMode="decimal" placeholder={unidade === 'Caixa' ? '2,5' : '3'} value={alvo} onChange={(e) => setAlvo(e.target.value)} />
              <span className="suave dica">Quanto você quer ter depois da entrega. O pedido usa enquanto não houver contagens.</span>
            </label>

            <label className="campo">
              <span>Mínimo (unidades, opcional)</span>
              <input
                type="number"
                inputMode="numeric"
                min={0}
                step={1}
                value={minimo}
                onChange={(e) => setMinimo(e.target.value)}
              />
              <span className="suave dica">Nos potes, ao chegar no mínimo você recebe um aviso. Pote novo sem mínimo fica com 2.</span>
            </label>

            <label className="campo">
              <span>Posição na contagem</span>
              <input
                type="number"
                inputMode="numeric"
                min={0}
                step={1}
                placeholder={String(proximaOrdem)}
                value={ordem}
                onChange={(e) => setOrdem(e.target.value)}
              />
              <span className="suave dica">Ordem do item na contagem e na folha do pedido, como na planilha.</span>
            </label>
          </div>

          {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
          {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}

          <div className="rodape-formulario">
            <Link to="/estoque" className="botao botao-secundario">
              Cancelar
            </Link>
            <button type="submit" className="botao" disabled={ocupado}>
              {salvar.isPending ? 'Salvando…' : 'Salvar'}
            </button>
          </div>
        </form>
      )}

      {item && (
        <Secao titulo={item.ativo ? 'Desativar' : 'Ativar'}>
          <p className="suave">
            {item.ativo
              ? 'Desativar tira o item da contagem e do pedido (ex.: a fábrica parou de fazer o sabor). O histórico continua.'
              : fornecedorDesativado
                ? 'O fornecedor deste item está desativado. Troque o fornecedor acima ou ative o fornecedor antes.'
                : 'Ativar faz o item voltar para a contagem e para o pedido.'}
          </p>
          {ativar.isError && <p className="alerta alerta-erro">{mensagemDoErro(ativar.error)}</p>}
          <button
            type="button"
            className="botao botao-secundario"
            disabled={ocupado || (!item.ativo && fornecedorDesativado)}
            onClick={() => ativar.mutate(!item.ativo)}
          >
            {ativar.isPending ? 'Aguarde…' : item.ativo ? 'Desativar item' : 'Ativar item'}
          </button>
        </Secao>
      )}
    </>
  )
}
