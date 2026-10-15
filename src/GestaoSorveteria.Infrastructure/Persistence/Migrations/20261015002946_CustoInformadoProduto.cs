using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustoInformadoProduto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "custo_informado",
                table: "produtos",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custo_informado",
                table: "produtos");
        }
    }
}
