using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CaixaDivergenciaSincronizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "divergencia_sincronizacao",
                table: "caixas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "total_vendas_dinheiro_app",
                table: "caixas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "valor_esperado_app",
                table: "caixas",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "divergencia_sincronizacao",
                table: "caixas");

            migrationBuilder.DropColumn(
                name: "total_vendas_dinheiro_app",
                table: "caixas");

            migrationBuilder.DropColumn(
                name: "valor_esperado_app",
                table: "caixas");
        }
    }
}
