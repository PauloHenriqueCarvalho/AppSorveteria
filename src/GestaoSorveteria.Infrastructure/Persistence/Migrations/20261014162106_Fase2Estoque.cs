using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Fase2Estoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "contagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    concluida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    origem = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contagens", x => x.id);
                    table.ForeignKey(
                        name: "fk_contagens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fornecedores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    whatsapp = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    prazo_entrega_dias = table.Column<int>(type: "integer", nullable: false),
                    aceita_meia_caixa = table.Column<bool>(type: "boolean", nullable: false),
                    envio = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    mostrar_precos = table.Column<bool>(type: "boolean", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    dias_pedido = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fornecedores", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grupos_estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    modo_controle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_grupos_estoque", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "itens_estoque",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grupo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fornecedor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    unidade_compra = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unidades_por_embalagem = table.Column<int>(type: "integer", nullable: false),
                    custo_embalagem = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    estoque_alvo_embalagens = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    minimo_unidades = table.Column<int>(type: "integer", nullable: true),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_estoque", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_estoque_fornecedores_fornecedor_id",
                        column: x => x.fornecedor_id,
                        principalTable: "fornecedores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_itens_estoque_grupos_estoque_grupo_id",
                        column: x => x.grupo_id,
                        principalTable: "grupos_estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "avisos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_estoque_id = table.Column<Guid>(type: "uuid", nullable: true),
                    texto_livre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    quantidade_atual = table.Column<int>(type: "integer", nullable: false),
                    prioridade = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    observacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    criado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    fechado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fechado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_fechamento = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    fechado_pelo_pedido_id = table.Column<Guid>(type: "uuid", nullable: true),
                    whatsapp_aberto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_avisos", x => x.id);
                    table.ForeignKey(
                        name: "fk_avisos_itens_estoque_item_estoque_id",
                        column: x => x.item_estoque_id,
                        principalTable: "itens_estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_avisos_usuarios_atualizado_por_usuario_id",
                        column: x => x.atualizado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_avisos_usuarios_criado_por_usuario_id",
                        column: x => x.criado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_avisos_usuarios_fechado_por_usuario_id",
                        column: x => x.fechado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_contagem",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contagem_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_estoque_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contado_unidades = table.Column<int>(type: "integer", nullable: true),
                    saldo_sistema_unidades = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_contagem", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_contagem_contagens_contagem_id",
                        column: x => x.contagem_id,
                        principalTable: "contagens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_itens_contagem_itens_estoque_item_estoque_id",
                        column: x => x.item_estoque_id,
                        principalTable: "itens_estoque",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_avisos_atualizado_por_usuario_id",
                table: "avisos",
                column: "atualizado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_criado_por_usuario_id",
                table: "avisos",
                column: "criado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_fechado_por_usuario_id",
                table: "avisos",
                column: "fechado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_avisos_status_criado_em",
                table: "avisos",
                columns: new[] { "status", "criado_em" });

            migrationBuilder.CreateIndex(
                name: "ux_avisos_um_aberto_por_item",
                table: "avisos",
                column: "item_estoque_id",
                unique: true,
                filter: "status = 'Aberto' AND item_estoque_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_contagens_concluida_em",
                table: "contagens",
                column: "concluida_em");

            migrationBuilder.CreateIndex(
                name: "ix_contagens_usuario_id",
                table: "contagens",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_fornecedores_nome_normalizado",
                table: "fornecedores",
                column: "nome_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grupos_estoque_nome_normalizado",
                table: "grupos_estoque",
                column: "nome_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_contagem_contagem_id_item_estoque_id",
                table: "itens_contagem",
                columns: new[] { "contagem_id", "item_estoque_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_itens_contagem_item_estoque_id",
                table: "itens_contagem",
                column: "item_estoque_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_estoque_fornecedor_id",
                table: "itens_estoque",
                column: "fornecedor_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_estoque_grupo_id_nome_normalizado",
                table: "itens_estoque",
                columns: new[] { "grupo_id", "nome_normalizado" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "avisos");

            migrationBuilder.DropTable(
                name: "itens_contagem");

            migrationBuilder.DropTable(
                name: "contagens");

            migrationBuilder.DropTable(
                name: "itens_estoque");

            migrationBuilder.DropTable(
                name: "fornecedores");

            migrationBuilder.DropTable(
                name: "grupos_estoque");
        }
    }
}
