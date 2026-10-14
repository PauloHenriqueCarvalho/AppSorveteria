using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PedidosFornecedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pedidos_fornecedor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    enviado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recebido_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recebido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    total_pedido = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    total_recebido = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    percentual_atendido = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_fornecedor", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedidos_fornecedor_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidos_fornecedor_usuarios_criado_por_usuario_id",
                        column: x => x.criado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidos_fornecedor_usuarios_recebido_por_usuario_id",
                        column: x => x.recebido_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_pedido_fornecedor",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pedido_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_estoque_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passo = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    sugerido_embalagens = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    pedido_embalagens = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: false),
                    recebido_embalagens = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    custo_embalagem = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    alterado_pela_dona = table.Column<bool>(type: "boolean", nullable: false),
                    origem = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pedir_de_novo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_pedido_fornecedor", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_pedido_fornecedor_itens_estoque_item_estoque_id",
                        column: x => x.item_estoque_id,
                        principalTable: "itens_estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_itens_pedido_fornecedor_pedidos_fornecedor_pedido_id",
                        column: x => x.pedido_id,
                        principalTable: "pedidos_fornecedor",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_itens_pedido_fornecedor_item_estoque_id",
                table: "itens_pedido_fornecedor",
                column: "item_estoque_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_pedido_fornecedor_pedido_id_item_estoque_id",
                table: "itens_pedido_fornecedor",
                columns: new[] { "pedido_id", "item_estoque_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_fornecedor_criado_por_usuario_id",
                table: "pedidos_fornecedor",
                column: "criado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_fornecedor_numero",
                table: "pedidos_fornecedor",
                column: "numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_fornecedor_recebido_por_usuario_id",
                table: "pedidos_fornecedor",
                column: "recebido_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_fornecedor_status_recebido_em",
                table: "pedidos_fornecedor",
                columns: new[] { "status", "recebido_em" });

            migrationBuilder.CreateIndex(
                name: "ux_pedidos_fornecedor_um_rascunho",
                table: "pedidos_fornecedor",
                column: "fornecedor_id",
                unique: true,
                filter: "status = 'Rascunho'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_pedido_fornecedor");

            migrationBuilder.DropTable(
                name: "pedidos_fornecedor");
        }
    }
}
