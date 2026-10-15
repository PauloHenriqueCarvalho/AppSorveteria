import { useQuery } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router'
import type { UsuarioDto } from '../../api/tipos.ts'
import { CHAVE_USUARIOS, listarUsuarios } from '../../api/usuarios.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { CabecalhoPagina, Selo } from '../../componentes/Ui.tsx'
import { TEXTO_PERFIL } from './perfil.ts'

// Ativos primeiro, depois pelo nome.
function ativosPrimeiro(a: UsuarioDto, b: UsuarioDto) {
  return Number(b.ativo) - Number(a.ativo) || a.nome.localeCompare(b.nome, 'pt-BR')
}

/** /usuarios — quem entra no tablet (atendentes, com PIN) e no painel (administração). */
export default function ListaUsuarios() {
  const navegar = useNavigate()
  const usuarios = useQuery({ queryKey: CHAVE_USUARIOS, queryFn: listarUsuarios })
  const ativos = usuarios.data?.filter((u) => u.ativo).length ?? 0

  return (
    <>
      <CabecalhoPagina
        titulo="Usuários"
        subtitulo={
          usuarios.data
            ? `${ativos} ${ativos === 1 ? 'usuário ativo' : 'usuários ativos'} · atendentes entram no tablet, a administração no painel`
            : 'Quem usa o tablet e o painel'
        }
        acoes={
          <Link to="/usuarios/novo" className="botao">
            Novo usuário
          </Link>
        }
      />

      <Consulta consulta={usuarios}>
        {(lista) =>
          lista.length === 0 ? (
            <section className="secao vazio">
              <h2>Nenhum usuário ainda</h2>
              <p className="suave">Cadastre as atendentes para elas entrarem no tablet com o PIN.</p>
            </section>
          ) : (
            <section className="secao secao-tabela">
              <div className="tabela-caixa">
                <table className="tabela">
                  <thead>
                    <tr>
                      <th scope="col">Nome</th>
                      <th scope="col">Login</th>
                      <th scope="col">Perfil</th>
                    </tr>
                  </thead>
                  <tbody>
                    {[...lista].sort(ativosPrimeiro).map((u) => (
                      <tr key={u.id} className="linha-clicavel" onClick={() => navegar(`/usuarios/${u.id}`)}>
                        <td>
                          <Link to={`/usuarios/${u.id}`} className="link-linha" onClick={(e) => e.stopPropagation()}>
                            {u.nome}
                          </Link>
                          {!u.ativo && (
                            <>
                              {' '}
                              <Selo>Desativado</Selo>
                            </>
                          )}
                        </td>
                        <td className="suave">{u.login}</td>
                        <td>
                          <Selo tom={u.perfil === 'Admin' ? 'marca' : 'neutro'}>{TEXTO_PERFIL[u.perfil]}</Selo>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )
        }
      </Consulta>
    </>
  )
}
