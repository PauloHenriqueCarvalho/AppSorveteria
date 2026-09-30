import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { deveTentarDeNovo, ESPERA_ENTRE_TENTATIVAS_MS } from './api/erros.ts'
import Layout from './componentes/Layout.tsx'
import RotaProtegida from './componentes/RotaProtegida.tsx'
import Inicio from './paginas/Inicio.tsx'
import Login from './paginas/Login.tsx'
import NaoEncontrada from './paginas/NaoEncontrada.tsx'
import EditarProduto from './paginas/produtos/EditarProduto.tsx'
import ListaProdutos from './paginas/produtos/ListaProdutos.tsx'
import SessaoProvider from './sessao/SessaoProvider.tsx'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: true,
      // Consultas tentam de novo enquanto a API acorda; erro de regra/acesso não repete.
      retry: deveTentarDeNovo,
      retryDelay: ESPERA_ENTRE_TENTATIVAS_MS,
    },
  },
})

const router = createBrowserRouter([
  { path: '/login', element: <Login /> },
  {
    element: <RotaProtegida />,
    children: [
      {
        element: <Layout />,
        children: [
          { index: true, element: <Inicio /> },
          { path: 'produtos', element: <ListaProdutos /> },
          { path: 'produtos/novo', element: <EditarProduto /> },
          { path: 'produtos/:id', element: <EditarProduto /> },
          { path: '*', element: <NaoEncontrada /> },
        ],
      },
    ],
  },
])

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <SessaoProvider>
        <RouterProvider router={router} />
      </SessaoProvider>
    </QueryClientProvider>
  )
}
