using GestaoSorveteria.Contracts.Auth;
using GestaoSorveteria.Domain.Usuarios;

namespace GestaoSorveteria.Application.Auth;

public static class UsuarioMapper
{
    public static UsuarioDto ToDto(this Usuario usuario) =>
        new(usuario.Id, usuario.Nome, usuario.Login, usuario.Perfil.ToString(), usuario.Ativo);
}
