import { NavLink, Outlet } from 'react-router'
import { useSessao } from '../sessao/contexto.ts'

// Itens do menu. Cada tela nova do painel entra aqui.
const MENU = [{ para: '/', texto: 'Início' }]

export default function Layout() {
  const { usuario, sair } = useSessao()

  return (
    <div className="app">
      <header className="topo">
        <span className="marca">Sorveteria</span>
        <div className="topo-usuario">
          <span className="topo-nome">{usuario?.nome}</span>
          <button type="button" className="botao-topo" onClick={() => sair()}>
            Sair
          </button>
        </div>
      </header>

      <main className="conteudo">
        <Outlet />
      </main>

      <nav className="menu" aria-label="Menu principal">
        {MENU.map((item) => (
          <NavLink key={item.para} to={item.para} end className="menu-item">
            {item.texto}
          </NavLink>
        ))}
      </nav>
    </div>
  )
}
