import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router'
import type { UsuarioDto } from '../../api/tipos.ts'
import { CHAVE_USUARIOS, listarUsuarios } from '../../api/usuarios.ts'
import Consulta from '../../componentes/Consulta.tsx'
import { TEXTO_PERFIL } from './perfil.ts'

// Ativos primeiro, depois pelo nome.
function ativosPrimeiro(a: UsuarioDto, b: UsuarioDto) {
  return Number(b.ativo) - Number(a.ativo) || a.nome.localeCompare(b.nome, 'pt-BR')
}

export default function ListaUsuarios() {
  const usuarios = useQuery({ queryKey: CHAVE_USUARIOS, queryFn: listarUsuarios })

  return (
    <>
      <div className="cabecalho-pagina">
        <h1>Usuários</h1>
        <Link to="/usuarios/novo" className="botao">
          Novo usuário
        </Link>
      </div>

      <Consulta consulta={usuarios}>
        {(lista) =>
          lista.length === 0 ? (
            <p className="cartao suave">Nenhum usuário cadastrado ainda.</p>
          ) : (
            <ul className="lista">
              {[...lista].sort(ativosPrimeiro).map((u) => (
                <li key={u.id}>
                  <Link to={`/usuarios/${u.id}`} className="lista-item">
                    <span className="lista-principal">
                      <strong>{u.nome}</strong>
                      <span className="suave">{u.login}</span>
                    </span>
                    <span className="lista-lateral">
                      <span>{TEXTO_PERFIL[u.perfil]}</span>
                      {!u.ativo && <span className="etiqueta">Desativado</span>}
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          )
        }
      </Consulta>
    </>
  )
}
