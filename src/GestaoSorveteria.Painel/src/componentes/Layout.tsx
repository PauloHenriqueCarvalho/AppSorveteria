import { useEffect, useState } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { formatarDiaPorExtenso, hojeNaLoja, iniciais } from '../formatacao.ts'
import { LOGO_LOJA, NOME_LOJA, SUBTITULO_LOJA } from '../marca.ts'
import { useSessao } from '../sessao/contexto.ts'
import Icone, { type NomeIcone } from './Icone.tsx'

interface ItemMenu {
  para: string
  texto: string
  icone: NomeIcone
  /** Aparece na barra de baixo do celular. */
  noCelular?: boolean
}

// Menu agrupado como num ERP. Cada tela nova do painel entra no grupo dela (Fase 2: "Estoque e compras").
const GRUPOS: { titulo: string; itens: ItemMenu[] }[] = [
  {
    titulo: 'Operação',
    itens: [
      { para: '/', texto: 'Início', icone: 'inicio', noCelular: true },
      { para: '/vendas', texto: 'Vendas', icone: 'vendas', noCelular: true },
      { para: '/caixas', texto: 'Caixas', icone: 'caixas', noCelular: true },
    ],
  },
  {
    titulo: 'Cadastros',
    itens: [
      { para: '/produtos', texto: 'Produtos', icone: 'produtos', noCelular: true },
      { para: '/usuarios', texto: 'Usuários', icone: 'usuarios' },
    ],
  },
]

const TODOS = GRUPOS.flatMap((g) => g.itens.map((i) => ({ ...i, grupo: g.titulo })))

/** Item do menu da rota atual ("/vendas/123" → Vendas). */
function itemAtual(caminho: string) {
  return (
    TODOS.filter((i) => (i.para === '/' ? caminho === '/' : caminho === i.para || caminho.startsWith(`${i.para}/`)))[0] ??
    null
  )
}

export default function Layout() {
  const { usuario, sair } = useSessao()
  const { pathname } = useLocation()
  // Menu do celular: fica aberto só na página em que foi aberto, então navegar já o fecha.
  const [menuAbertoEm, setMenuAbertoEm] = useState<string | null>(null)
  const menuAberto = menuAbertoEm === pathname
  const abrirMenu = () => setMenuAbertoEm(pathname)
  const fecharMenu = () => setMenuAbertoEm(null)
  const atual = itemAtual(pathname)
  const emDetalhe = atual !== null && pathname !== atual.para

  // Ao navegar, a página volta ao topo.
  useEffect(() => {
    window.scrollTo(0, 0)
  }, [pathname])

  useEffect(() => {
    document.title = atual ? `${atual.texto} — ${NOME_LOJA}` : `${NOME_LOJA} — Painel`
  }, [atual])

  return (
    <div className={menuAberto ? 'app app-menu-aberto' : 'app'}>
      <aside className="lateral" aria-label="Menu principal">
        <Link to="/" className="lateral-marca">
          <img src={LOGO_LOJA} alt="" width={40} height={46} />
          <span>
            <strong>{NOME_LOJA}</strong>
            <small>{SUBTITULO_LOJA}</small>
          </span>
        </Link>
        <nav className="lateral-nav">
          {GRUPOS.map((grupo) => (
            <div key={grupo.titulo} className="lateral-grupo">
              <span className="lateral-titulo">{grupo.titulo}</span>
              {grupo.itens.map((item) => (
                <NavLink key={item.para} to={item.para} end={item.para === '/'} className="lateral-item">
                  <Icone nome={item.icone} />
                  {item.texto}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>
        <button type="button" className="lateral-item lateral-sair" onClick={() => sair()}>
          <Icone nome="sair" />
          Sair
        </button>
      </aside>
      <button
        type="button"
        className="lateral-fundo"
        aria-label="Fechar o menu"
        tabIndex={menuAberto ? 0 : -1}
        onClick={fecharMenu}
      />

      <div className="principal">
        <header className="topo">
          <button
            type="button"
            className="topo-menu"
            aria-label="Abrir o menu"
            aria-expanded={menuAberto}
            onClick={abrirMenu}
          >
            <Icone nome="menu" tamanho={22} />
          </button>
          <nav className="trilha" aria-label="Você está em">
            {atual ? (
              <>
                <span className="trilha-grupo">{atual.grupo}</span>
                <span aria-hidden="true">/</span>
                {emDetalhe ? <Link to={atual.para}>{atual.texto}</Link> : <strong>{atual.texto}</strong>}
                {emDetalhe && (
                  <>
                    <span aria-hidden="true">/</span>
                    <strong>Detalhe</strong>
                  </>
                )}
              </>
            ) : (
              <strong>{NOME_LOJA}</strong>
            )}
          </nav>
          <span className="topo-data">
            <Icone nome="calendario" tamanho={16} />
            {formatarDiaPorExtenso(hojeNaLoja())}
          </span>
          {usuario && (
            <span className="topo-usuario" title={usuario.nome}>
              <span className="avatar" aria-hidden="true">
                {iniciais(usuario.nome)}
              </span>
              <span className="topo-nome">
                <strong>{usuario.nome}</strong>
                <small>Administração</small>
              </span>
            </span>
          )}
        </header>

        <main className="conteudo">
          <Outlet />
        </main>
      </div>

      {/* Celular: atalhos ao alcance do polegar; o resto fica em "Mais". */}
      <nav className="barra-celular" aria-label="Atalhos">
        {TODOS.filter((i) => i.noCelular).map((item) => (
          <NavLink key={item.para} to={item.para} end={item.para === '/'} className="barra-item">
            <Icone nome={item.icone} tamanho={20} />
            {item.texto}
          </NavLink>
        ))}
        <button type="button" className="barra-item" aria-expanded={menuAberto} onClick={abrirMenu}>
          <Icone nome="menu" tamanho={20} />
          Mais
        </button>
      </nav>
    </div>
  )
}
