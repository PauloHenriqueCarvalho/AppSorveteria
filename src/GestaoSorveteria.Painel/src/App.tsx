import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { createBrowserRouter, RouterProvider } from 'react-router'
import { deveTentarDeNovo, ESPERA_ENTRE_TENTATIVAS_MS } from './api/erros.ts'
import Layout from './componentes/Layout.tsx'
import RotaProtegida from './componentes/RotaProtegida.tsx'
import Inicio from './paginas/Inicio.tsx'
import Login from './paginas/Login.tsx'
import DetalheCaixa from './paginas/caixas/DetalheCaixa.tsx'
import ListaCaixas from './paginas/caixas/ListaCaixas.tsx'
import NaoEncontrada from './paginas/NaoEncontrada.tsx'
import DetalheAviso from './paginas/avisos/DetalheAviso.tsx'
import ListaAvisos from './paginas/avisos/ListaAvisos.tsx'
import NovoAviso from './paginas/avisos/NovoAviso.tsx'
import ResumoDoDia from './paginas/avisos/ResumoDoDia.tsx'
import Configuracoes from './paginas/configuracoes/Configuracoes.tsx'
import EditarContagem from './paginas/contagens/EditarContagem.tsx'
import ListaContagens from './paginas/contagens/ListaContagens.tsx'
import ClimaVendas from './paginas/clima/ClimaVendas.tsx'
import CatalogoEstoque from './paginas/estoque/CatalogoEstoque.tsx'
import Relatorios from './paginas/relatorios/Relatorios.tsx'
import EditarGrupoEstoque from './paginas/estoque/EditarGrupoEstoque.tsx'
import EditarItemEstoque from './paginas/estoque/EditarItemEstoque.tsx'
import ImportarCatalogo from './paginas/estoque/ImportarCatalogo.tsx'
import SaldosPotes from './paginas/estoque/SaldosPotes.tsx'
import EditarFornecedor from './paginas/fornecedores/EditarFornecedor.tsx'
import ListaFornecedores from './paginas/fornecedores/ListaFornecedores.tsx'
import ConferirEntrega from './paginas/pedidos/ConferirEntrega.tsx'
import DetalhePedido from './paginas/pedidos/DetalhePedido.tsx'
import FolhaPedido from './paginas/pedidos/FolhaPedido.tsx'
import ListaPedidos from './paginas/pedidos/ListaPedidos.tsx'
import EditarProduto from './paginas/produtos/EditarProduto.tsx'
import ListaProdutos from './paginas/produtos/ListaProdutos.tsx'
import DetalheSugestao from './paginas/sugestoes/DetalheSugestao.tsx'
import ListaSugestoes from './paginas/sugestoes/ListaSugestoes.tsx'
import NovaSugestao from './paginas/sugestoes/NovaSugestao.tsx'
import DetalheUsuario from './paginas/usuarios/DetalheUsuario.tsx'
import ListaUsuarios from './paginas/usuarios/ListaUsuarios.tsx'
import NovoUsuario from './paginas/usuarios/NovoUsuario.tsx'
import DetalheVenda from './paginas/vendas/DetalheVenda.tsx'
import ListaVendas from './paginas/vendas/ListaVendas.tsx'
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
      // Folha impressa do pedido (RN-PF-07): sem menu, só a folha.
      { path: 'pedidos/:id/imprimir', element: <FolhaPedido /> },
      {
        element: <Layout />,
        children: [
          { index: true, element: <Inicio /> },
          { path: 'vendas', element: <ListaVendas /> },
          { path: 'vendas/:id', element: <DetalheVenda /> },
          { path: 'caixas', element: <ListaCaixas /> },
          { path: 'relatorios', element: <Relatorios /> },
          { path: 'clima-vendas', element: <ClimaVendas /> },
          { path: 'caixas/:id', element: <DetalheCaixa /> },
          { path: 'produtos', element: <ListaProdutos /> },
          { path: 'produtos/novo', element: <EditarProduto /> },
          { path: 'produtos/:id', element: <EditarProduto /> },
          { path: 'avisos', element: <ListaAvisos /> },
          { path: 'avisos/novo', element: <NovoAviso /> },
          { path: 'avisos/resumo', element: <ResumoDoDia /> },
          { path: 'avisos/:id', element: <DetalheAviso /> },
          { path: 'contagens', element: <ListaContagens /> },
          { path: 'contagens/nova', element: <EditarContagem /> },
          { path: 'contagens/:id', element: <EditarContagem /> },
          { path: 'pedidos', element: <ListaPedidos /> },
          { path: 'pedidos/:id', element: <DetalhePedido /> },
          { path: 'pedidos/:id/conferir', element: <ConferirEntrega /> },
          { path: 'potes', element: <SaldosPotes /> },
          { path: 'estoque', element: <CatalogoEstoque /> },
          { path: 'estoque/importar', element: <ImportarCatalogo /> },
          { path: 'estoque/itens/novo', element: <EditarItemEstoque /> },
          { path: 'estoque/itens/:id', element: <EditarItemEstoque /> },
          { path: 'estoque/grupos/novo', element: <EditarGrupoEstoque /> },
          { path: 'estoque/grupos/:id', element: <EditarGrupoEstoque /> },
          { path: 'fornecedores', element: <ListaFornecedores /> },
          { path: 'fornecedores/novo', element: <EditarFornecedor /> },
          { path: 'fornecedores/:id', element: <EditarFornecedor /> },
          { path: 'usuarios', element: <ListaUsuarios /> },
          { path: 'usuarios/novo', element: <NovoUsuario /> },
          { path: 'usuarios/:id', element: <DetalheUsuario /> },
          { path: 'configuracoes', element: <Configuracoes /> },
          { path: 'sugestoes', element: <ListaSugestoes /> },
          { path: 'sugestoes/nova', element: <NovaSugestao /> },
          { path: 'sugestoes/:id', element: <DetalheSugestao /> },
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
