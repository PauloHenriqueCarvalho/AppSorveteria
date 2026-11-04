using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sugestoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sugestoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    descricao = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    area = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    importancia = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criada_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sugestoes", x => x.id);
                    table.ForeignKey(
                        name: "fk_sugestoes_usuarios_criada_por_usuario_id",
                        column: x => x.criada_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sugestao_comentarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sugestao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    texto = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sugestao_comentarios", x => x.id);
                    table.ForeignKey(
                        name: "fk_sugestao_comentarios_sugestoes_sugestao_id",
                        column: x => x.sugestao_id,
                        principalTable: "sugestoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sugestao_comentarios_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sugestao_imagens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sugestao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome_arquivo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tipo_conteudo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tamanho_bytes = table.Column<int>(type: "integer", nullable: false),
                    conteudo = table.Column<byte[]>(type: "bytea", nullable: false),
                    enviada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sugestao_imagens", x => x.id);
                    table.ForeignKey(
                        name: "fk_sugestao_imagens_sugestoes_sugestao_id",
                        column: x => x.sugestao_id,
                        principalTable: "sugestoes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sugestao_imagens_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sugestao_comentarios_sugestao_id_em",
                table: "sugestao_comentarios",
                columns: new[] { "sugestao_id", "em" });

            migrationBuilder.CreateIndex(
                name: "ix_sugestao_comentarios_usuario_id",
                table: "sugestao_comentarios",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_sugestao_imagens_sugestao_id_enviada_em",
                table: "sugestao_imagens",
                columns: new[] { "sugestao_id", "enviada_em" });

            migrationBuilder.CreateIndex(
                name: "ix_sugestao_imagens_usuario_id",
                table: "sugestao_imagens",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_sugestoes_atualizada_em",
                table: "sugestoes",
                column: "atualizada_em");

            migrationBuilder.CreateIndex(
                name: "ix_sugestoes_criada_por_usuario_id",
                table: "sugestoes",
                column: "criada_por_usuario_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sugestao_comentarios");

            migrationBuilder.DropTable(
                name: "sugestao_imagens");

            migrationBuilder.DropTable(
                name: "sugestoes");
        }
    }
}
