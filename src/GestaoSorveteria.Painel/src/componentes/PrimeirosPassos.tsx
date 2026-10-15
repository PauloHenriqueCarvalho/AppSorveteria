import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import { CHAVE_PRIMEIROS_PASSOS, obterPrimeirosPassos } from '../api/avisos.ts'
import type { PrimeirosPassosDto } from '../api/tipos.ts'
import { Barra, Secao } from './Ui.tsx'

interface Passo {
  chave: keyof PrimeirosPassosDto
  titulo: string
  detalhe: string
  para?: string
  acao?: string
}

/** RN-LJ-01, na ordem em que a loja costuma fazer. */
const PASSOS: Passo[] = [
  {
    chave: 'catalogoDeEstoque',
    titulo: 'Importar o catálogo de estoque',
    detalhe: 'A planilha de picolés, massas e potes. Sem ela não tem contagem do freezer nem pedido ao fornecedor.',
    para: '/estoque/importar',
    acao: 'Importar planilha',
  },
  {
    chave: 'produtos',
    titulo: 'Cadastrar o cardápio',
    detalhe: 'Os produtos que aparecem nos botões do tablet, com o preço.',
    para: '/produtos/novo',
    acao: 'Novo produto',
  },
  {
    chave: 'atendentes',
    titulo: 'Cadastrar as atendentes',
    detalhe: 'Cada uma entra no tablet com o PIN dela.',
    para: '/usuarios/novo',
    acao: 'Novo usuário',
  },
  {
    chave: 'whatsAppDona',
    titulo: 'Informar o seu WhatsApp',
    detalhe: 'Para receber na hora o aviso urgente e, no fim do dia, o resumo.',
    para: '/configuracoes',
    acao: 'Configurações',
  },
  {
    chave: 'contagemConcluida',
    titulo: 'Fazer a primeira contagem do freezer',
    detalhe: 'É o ponto de partida do pedido sugerido e do saldo dos potes. Pode ser pelo tablet ou por aqui.',
    para: '/contagens/nova',
    acao: 'Nova contagem',
  },
  {
    chave: 'vendaDoTablet',
    titulo: 'Receber a primeira venda do tablet',
    detalhe: 'Instale o app no tablet, entre com o PIN, abra o caixa e faça uma venda: ela aparece aqui no Início.',
  },
]

/** Lista de primeiros passos no Início (RN-LJ-01): some sozinha quando tudo está feito. */
export default function PrimeirosPassos() {
  const passos = useQuery({ queryKey: CHAVE_PRIMEIROS_PASSOS, queryFn: obterPrimeirosPassos })
  if (!passos.data) {
    return null
  }

  const feitos = PASSOS.filter((p) => passos.data[p.chave]).length
  if (feitos === PASSOS.length) {
    return null
  }

  return (
    <Secao titulo="Primeiros passos" acao={<span className="suave pequeno">{`${feitos} de ${PASSOS.length} feitos`}</span>}>
      <Barra fracao={feitos / PASSOS.length} />
      <ol className="passos">
        {PASSOS.map((p, i) => {
          const feito = passos.data[p.chave]
          return (
            <li key={p.chave} className={feito ? 'passo passo-feito' : 'passo'}>
              <span className="passo-marca" aria-hidden="true">
                {feito ? '✓' : i + 1}
              </span>
              <div className="passo-corpo">
                <strong>{p.titulo}</strong>
                {feito ? <span className="suave"> · feito</span> : <span className="suave bloco">{p.detalhe}</span>}
              </div>
              {!feito && p.para && (
                <Link to={p.para} className="botao botao-secundario">
                  {p.acao}
                </Link>
              )}
            </li>
          )
        })}
      </ol>
    </Secao>
  )
}
