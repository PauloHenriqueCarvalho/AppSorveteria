using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PotesSaldoPelaVenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "grupo_estoque_id",
                table: "produtos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "unidades_por_venda",
                table: "produtos",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "chegou_em",
                table: "pedidos_fornecedor",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "contado_em",
                table: "itens_contagem",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "item_estoque_id",
                table: "itens_comanda",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "movimentos_estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_estoque_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    unidades = table.Column<int>(type: "integer", nullable: false),
                    em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    registrado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_comanda_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentos_estoque", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentos_estoque_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentos_estoque_itens_comanda_item_comanda_id",
                        column: x => x.item_comanda_id,
                        principalTable: "itens_comanda",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentos_estoque_itens_estoque_item_estoque_id",
                        column: x => x.item_estoque_id,
                        principalTable: "itens_estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentos_estoque_pedidos_fornecedor_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos_fornecedor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_produtos_grupo_estoque_id",
                table: "produtos",
                column: "grupo_estoque_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_comanda_item_estoque_id",
                table: "itens_comanda",
                column: "item_estoque_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_estoque_comanda_id",
                table: "movimentos_estoque",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_estoque_item_comanda_id",
                table: "movimentos_estoque",
                column: "item_comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_estoque_item_estoque_id_em",
                table: "movimentos_estoque",
                columns: new[] { "item_estoque_id", "em" });

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_estoque_pedido_id",
                table: "movimentos_estoque",
                column: "pedido_id");

            migrationBuilder.CreateIndex(
                name: "ux_movimentos_estoque_entrada_por_pedido",
                table: "movimentos_estoque",
                columns: new[] { "tipo", "pedido_id", "item_estoque_id" },
                unique: true,
                filter: "pedido_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_movimentos_estoque_por_linha_de_venda",
                table: "movimentos_estoque",
                columns: new[] { "tipo", "item_comanda_id" },
                unique: true,
                filter: "item_comanda_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_itens_comanda_itens_estoque_item_estoque_id",
                table: "itens_comanda",
                column: "item_estoque_id",
                principalTable: "itens_estoque",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_produtos_grupos_estoque_grupo_estoque_id",
                table: "produtos",
                column: "grupo_estoque_id",
                principalTable: "grupos_estoque",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_itens_comanda_itens_estoque_item_estoque_id",
                table: "itens_comanda");

            migrationBuilder.DropForeignKey(
                name: "fk_produtos_grupos_estoque_grupo_estoque_id",
                table: "produtos");

            migrationBuilder.DropTable(
                name: "movimentos_estoque");

            migrationBuilder.DropIndex(
                name: "ix_produtos_grupo_estoque_id",
                table: "produtos");

            migrationBuilder.DropIndex(
                name: "ix_itens_comanda_item_estoque_id",
                table: "itens_comanda");

            migrationBuilder.DropColumn(
                name: "grupo_estoque_id",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "unidades_por_venda",
                table: "produtos");

            migrationBuilder.DropColumn(
                name: "chegou_em",
                table: "pedidos_fornecedor");

            migrationBuilder.DropColumn(
                name: "contado_em",
                table: "itens_contagem");

            migrationBuilder.DropColumn(
                name: "item_estoque_id",
                table: "itens_comanda");
        }
    }
}
