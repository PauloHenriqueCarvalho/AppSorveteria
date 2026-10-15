import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { mensagemDoErro } from '../../api/erros.ts'
import {
  alterarAtivo,
  atualizarProduto,
  CHAVE_PRODUTOS,
  criarProduto,
  ligarProdutoAoEstoque,
  listarProdutos,
  obterProduto,
} from '../../api/produtos.ts'
import type { ProdutoDto, SalvarProdutoRequest } from '../../api/tipos.ts'
import ConectandoServidor from '../../componentes/ConectandoServidor.tsx'
import { lerPreco, precoParaCampo } from '../../entrada.ts'
import { useCatalogoEstoque } from '../estoque/useCatalogoEstoque.ts'

/** /produtos/novo (sem id) e /produtos/:id. */
export default function EditarProduto() {
  const { id } = useParams()
  const produto = useQuery({
    queryKey: [...CHAVE_PRODUTOS, id],
    queryFn: () => obterProduto(id!),
    enabled: id !== undefined,
  })

  if (id !== undefined && produto.isPending) {
    return <ConectandoServidor falhas={produto.failureCount} />
  }
  if (id !== undefined && produto.isError) {
    return (
      <div className="cartao">
        <p className="alerta alerta-erro">{mensagemDoErro(produto.error)}</p>
        <Link to="/produtos">Voltar para os produtos</Link>
      </div>
    )
  }

  // key: formulário novo ao trocar de produto; ativar/desativar só atualiza o aviso, sem perder o que foi digitado.
  return <Formulario key={produto.data?.id ?? 'novo'} produto={produto.data} />
}

function Formulario({ produto }: { produto?: ProdutoDto }) {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const lista = useQuery({ queryKey: CHAVE_PRODUTOS, queryFn: listarProdutos })

  const [nome, setNome] = useState(produto?.nome ?? '')
  const [categoria, setCategoria] = useState(produto?.categoria ?? '')
  const [preco, setPreco] = useState(produto ? precoParaCampo(produto.preco) : '')
  const [permiteValorLivre, setPermiteValorLivre] = useState(produto?.permiteValorLivre ?? false)
  const [ordem, setOrdem] = useState(produto ? String(produto.ordem) : '')
  const [erroCampo, setErroCampo] = useState<string | null>(null)

  const categorias = [...new Set((lista.data ?? []).map((p) => p.categoria))].sort((a, b) => a.localeCompare(b, 'pt-BR'))
  // Produto novo vai para o fim da fila de botões, se a dona não escolher outra posição.
  const proximaOrdem = (lista.data ?? []).reduce((maior, p) => Math.max(maior, p.ordem + 1), 0)

  const salvar = useMutation({
    mutationFn: (dados: SalvarProdutoRequest) => (produto ? atualizarProduto(produto.id, dados) : criarProduto(dados)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: CHAVE_PRODUTOS })
      navigate('/produtos')
    },
  })

  const ativar = useMutation({
    mutationFn: (ativo: boolean) => alterarAtivo(produto!.id, ativo),
    onSuccess: (atualizado) => {
      queryClient.setQueryData([...CHAVE_PRODUTOS, atualizado.id], atualizado)
      void queryClient.invalidateQueries({ queryKey: CHAVE_PRODUTOS, exact: true })
    },
  })

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErroCampo(null)

    const valor = lerPreco(preco === '' && permiteValorLivre ? '0' : preco)
    if (valor === null) {
      setErroCampo('Preço inválido. Escreva só o valor, por exemplo 12,50.')
      return
    }
    const posicao = ordem.trim() === '' ? proximaOrdem : Number(ordem)
    if (!Number.isInteger(posicao) || posicao < 0) {
      setErroCampo('A posição no app deve ser um número inteiro, 0 ou maior.')
      return
    }

    salvar.mutate({
      nome: nome.trim(),
      categoria: categoria.trim(),
      preco: valor,
      permiteValorLivre,
      ordem: posicao,
    })
  }

  const ocupado = salvar.isPending || ativar.isPending

  return (
    <>
      <p>
        <Link to="/produtos">← Produtos</Link>
      </p>

      <form className="cartao formulario" onSubmit={enviar}>
        <h1>{produto ? 'Editar produto' : 'Novo produto'}</h1>

        {produto && !produto.ativo && (
          <p className="alerta alerta-aviso">Este produto está desativado: não aparece no app do balcão.</p>
        )}

        <label className="campo">
          <span>Nome</span>
          <input required minLength={2} maxLength={80} value={nome} onChange={(e) => setNome(e.target.value)} />
        </label>

        <label className="campo">
          <span>Categoria</span>
          <input
            required
            maxLength={50}
            list="categorias"
            value={categoria}
            onChange={(e) => setCategoria(e.target.value)}
          />
          <datalist id="categorias">
            {categorias.map((c) => (
              <option key={c} value={c} />
            ))}
          </datalist>
        </label>

        <label className="campo-marcar">
          <input type="checkbox" checked={permiteValorLivre} onChange={(e) => setPermiteValorLivre(e.target.checked)} />
          <span>
            Valor digitado na hora da venda
            <span className="suave"> (ex.: self-service por quilo)</span>
          </span>
        </label>

        <label className="campo">
          <span>{permiteValorLivre ? 'Preço sugerido (opcional)' : 'Preço (R$)'}</span>
          <input
            inputMode="decimal"
            placeholder="0,00"
            required={!permiteValorLivre}
            value={preco}
            onChange={(e) => setPreco(e.target.value)}
          />
        </label>

        <label className="campo">
          <span>Posição do botão no app</span>
          <input
            type="number"
            inputMode="numeric"
            min={0}
            step={1}
            placeholder={String(proximaOrdem)}
            value={ordem}
            onChange={(e) => setOrdem(e.target.value)}
          />
          <span className="suave dica">Números menores aparecem primeiro no celular do balcão.</span>
        </label>

        {erroCampo && <p className="alerta alerta-erro">{erroCampo}</p>}
        {salvar.isError && <p className="alerta alerta-erro">{mensagemDoErro(salvar.error)}</p>}

        <button type="submit" className="botao" disabled={ocupado}>
          {salvar.isPending ? 'Salvando…' : 'Salvar'}
        </button>
      </form>

      {produto && <LigarAoEstoque key={produto.id} produto={produto} />}

      {produto && (
        <div className="cartao formulario">
          <p className="suave">
            {produto.ativo
              ? 'Desativar tira o produto do app do balcão. As vendas antigas continuam registradas.'
              : 'Ativar faz o produto voltar a aparecer no app do balcão.'}
          </p>
          {ativar.isError && <p className="alerta alerta-erro">{mensagemDoErro(ativar.error)}</p>}
          <button
            type="button"
            className={produto.ativo ? 'botao botao-secundario' : 'botao'}
            disabled={ocupado}
            onClick={() => ativar.mutate(!produto.ativo)}
          >
            {ativar.isPending ? 'Aguarde…' : produto.ativo ? 'Desativar produto' : 'Ativar produto'}
          </button>
        </div>
      )}
    </>
  )
}

/**
 * RN-PR-06: liga o produto a um grupo de estoque. Grupo de potes (saldo pela venda) faz o tablet pedir o sabor e o saldo
 * descer sozinho a cada venda (RN-ES-09). Separado do cadastro: salvar nome e preço não mexe nisto.
 */
function LigarAoEstoque({ produto }: { produto: ProdutoDto }) {
  const queryClient = useQueryClient()
  const catalogo = useCatalogoEstoque()
  const [grupoId, setGrupoId] = useState(produto.grupoEstoqueId ?? '')
  const [unidades, setUnidades] = useState(String(produto.unidadesPorVenda ?? 1))
  const [erro, setErro] = useState<string | null>(null)
  const [salvo, setSalvo] = useState(false)

  const ligar = useMutation({
    mutationFn: () =>
      // Desligado volta para 1: o número só vale com grupo.
      ligarProdutoAoEstoque(produto.id, { grupoEstoqueId: grupoId || null, unidadesPorVenda: grupoId ? Number(unidades) : 1 }),
    onSuccess: (atualizado) => {
      queryClient.setQueryData([...CHAVE_PRODUTOS, atualizado.id], atualizado)
      void queryClient.invalidateQueries({ queryKey: CHAVE_PRODUTOS, exact: true })
      setSalvo(true)
    },
  })

  const grupos = (catalogo.data?.grupos ?? []).filter((g) => g.modoControle !== 'SoAviso').sort((a, b) => a.ordem - b.ordem)
  const escolhido = grupos.find((g) => g.id === grupoId)
  const mudou = grupoId !== (produto.grupoEstoqueId ?? '') || (grupoId !== '' && Number(unidades) !== (produto.unidadesPorVenda ?? 1))

  function enviar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    setErro(null)
    setSalvo(false)
    const n = Number(unidades)
    if (grupoId !== '' && (!Number.isInteger(n) || n < 1 || n > 50)) {
      setErro('Unidades por venda vai de 1 a 50.')
      return
    }
    ligar.mutate()
  }

  if (produto.permiteValorLivre) {
    return (
      <div className="cartao formulario">
        <h2>Estoque</h2>
        <p className="suave">Produto de valor digitado (self-service) não baixa estoque.</p>
      </div>
    )
  }

  return (
    <form className="cartao formulario" onSubmit={enviar}>
      <h2>Estoque</h2>
      <p className="suave">
        Ligue os potes ao grupo deles: o tablet passa a pedir o sabor na venda e o saldo de cada sabor desce sozinho. Picolé e
        massa continuam pela contagem semanal.
      </p>
      <div className="grade-campos">
        <label className="campo">
          <span>Grupo de estoque</span>
          <select value={grupoId} onChange={(e) => setGrupoId(e.target.value)} disabled={ligar.isPending}>
            <option value="">Não mexe no estoque</option>
            {grupos.map((g) => (
              <option key={g.id} value={g.id}>
                {g.nome}
                {g.modoControle === 'SaldoPelaVenda' ? ' — pede o sabor' : ''}
              </option>
            ))}
          </select>
        </label>
        {grupoId !== '' && (
          <label className="campo">
            <span>Quantos saem por venda</span>
            <input type="number" inputMode="numeric" min={1} max={50} step={1} value={unidades} onChange={(e) => setUnidades(e.target.value)} />
            <span className="suave dica">Ex.: "2 potes de 1L" = 2.</span>
          </label>
        )}
      </div>
      {escolhido && escolhido.modoControle !== 'SaldoPelaVenda' && (
        <p className="suave dica">{escolhido.nome} é controlado pela contagem semanal: a venda não pede sabor nem baixa saldo.</p>
      )}
      {erro && <p className="alerta alerta-erro">{erro}</p>}
      {ligar.isError && <p className="alerta alerta-erro">{mensagemDoErro(ligar.error)}</p>}
      {salvo && !mudou && <p className="alerta alerta-sucesso">Salvo. O tablet recebe na próxima sincronização.</p>}
      <button type="submit" className="botao botao-secundario" disabled={ligar.isPending || !mudou}>
        {ligar.isPending ? 'Salvando…' : 'Salvar estoque'}
      </button>
    </form>
  )
}
