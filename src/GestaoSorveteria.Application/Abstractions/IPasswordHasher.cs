namespace GestaoSorveteria.Application.Abstractions;

/// <summary>RN-US-04: só o hash é gravado.</summary>
public interface IPasswordHasher
{
    string Hash(string senha);

    bool Verificar(string senha, string hashArmazenado);
}
