import { NavLink, Outlet } from 'react-router'

// Itens do menu. Cada tela nova do painel entra aqui.
const MENU = [{ para: '/', texto: 'Início' }]

export default function Layout() {
  return (
    <div className="app">
      <header className="topo">
        <span className="marca">Sorveteria</span>
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
