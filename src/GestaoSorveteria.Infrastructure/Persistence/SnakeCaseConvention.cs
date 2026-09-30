using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace GestaoSorveteria.Infrastructure.Persistence;

/// <summary>
/// ADR-007: tabelas, colunas, chaves e índices em snake_case (padrão PostgreSQL, legível no DBeaver).
/// Aplicado depois das configurações, então nomes definidos explicitamente também são normalizados.
/// </summary>
internal static class SnakeCaseConvention
{
    private static readonly Regex Separador = new("(?<=[a-z0-9])([A-Z])", RegexOptions.Compiled);

    public static void AplicarSnakeCase(this ModelBuilder modelBuilder)
    {
        foreach (var entidade in modelBuilder.Model.GetEntityTypes())
        {
            var tabela = entidade.GetTableName();
            if (tabela is not null)
            {
                entidade.SetTableName(ParaSnakeCase(tabela));
            }

            foreach (var propriedade in entidade.GetProperties())
            {
                propriedade.SetColumnName(ParaSnakeCase(propriedade.GetColumnName()));
            }

            foreach (var chave in entidade.GetKeys())
            {
                var nome = chave.GetName();
                if (nome is not null)
                {
                    chave.SetName(ParaSnakeCase(nome));
                }
            }

            foreach (var fk in entidade.GetForeignKeys())
            {
                var nome = fk.GetConstraintName();
                if (nome is not null)
                {
                    fk.SetConstraintName(ParaSnakeCase(nome));
                }
            }

            foreach (var indice in entidade.GetIndexes())
            {
                var nome = indice.GetDatabaseName();
                if (nome is not null)
                {
                    indice.SetDatabaseName(ParaSnakeCase(nome));
                }
            }
        }
    }

    /// <summary>AbertoPorUsuarioId → aberto_por_usuario_id; PK_usuarios → pk_usuarios.</summary>
    public static string ParaSnakeCase(string nome) =>
        Separador.Replace(nome, "_$1").ToLowerInvariant();
}
