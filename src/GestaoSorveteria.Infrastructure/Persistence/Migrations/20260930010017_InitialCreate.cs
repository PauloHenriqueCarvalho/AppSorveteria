using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "produtos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    nome_normalizado = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    preco = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    permite_valor_livre = table.Column<bool>(type: "boolean", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    ordem = table.Column<int>(type: "integer", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_produtos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    login = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    perfil = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "caixas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberto_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aberto_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fundo_troco = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fechado_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fechado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total_vendas_dinheiro = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    valor_esperado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    valor_contado = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    diferenca = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    observacao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_caixas", x => x.id);
                    table.ForeignKey(
                        name: "fk_caixas_usuarios_aberto_por_usuario_id",
                        column: x => x.aberto_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_caixas_usuarios_fechado_por_usuario_id",
                        column: x => x.fechado_por_usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comandas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    caixa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    observacao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    recebida_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fechada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    recebida_apos_fechamento_caixa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comandas", x => x.id);
                    table.ForeignKey(
                        name: "fk_comandas_caixas_caixa_id",
                        column: x => x.caixa_id,
                        principalTable: "caixas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_comandas_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "movimentos_caixa",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    caixa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    motivo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_movimentos_caixa", x => x.id);
                    table.ForeignKey(
                        name: "fk_movimentos_caixa_caixas_caixa_id",
                        column: x => x.caixa_id,
                        principalTable: "caixas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_movimentos_caixa_usuarios_usuario_id",
                        column: x => x.usuario_id,
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "itens_comanda",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    produto_id = table.Column<Guid>(type: "uuid", nullable: true),
                    descricao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    quantidade = table.Column<int>(type: "integer", nullable: false),
                    preco_unitario = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    subtotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_itens_comanda", x => x.id);
                    table.ForeignKey(
                        name: "fk_itens_comanda_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_itens_comanda_produtos_produto_id",
                        column: x => x.produto_id,
                        principalTable: "produtos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "pagamentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comanda_id = table.Column<Guid>(type: "uuid", nullable: false),
                    forma = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    valor_recebido = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    troco = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pagamentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_pagamentos_comandas_comanda_id",
                        column: x => x.comanda_id,
                        principalTable: "comandas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_caixas_aberto_em",
                table: "caixas",
                column: "aberto_em");

            migrationBuilder.CreateIndex(
                name: "ix_caixas_aberto_por_usuario_id",
                table: "caixas",
                column: "aberto_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_caixas_fechado_por_usuario_id",
                table: "caixas",
                column: "fechado_por_usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_caixas_unico_aberto",
                table: "caixas",
                column: "status",
                unique: true,
                filter: "status = 'Aberto'");

            migrationBuilder.CreateIndex(
                name: "ix_comandas_caixa_id_numero",
                table: "comandas",
                columns: new[] { "caixa_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_comandas_fechada_em",
                table: "comandas",
                column: "fechada_em");

            migrationBuilder.CreateIndex(
                name: "ix_comandas_status",
                table: "comandas",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_comandas_usuario_id",
                table: "comandas",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_comanda_comanda_id",
                table: "itens_comanda",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_itens_comanda_produto_id",
                table: "itens_comanda",
                column: "produto_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_caixa_caixa_id",
                table: "movimentos_caixa",
                column: "caixa_id");

            migrationBuilder.CreateIndex(
                name: "ix_movimentos_caixa_usuario_id",
                table: "movimentos_caixa",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_comanda_id",
                table: "pagamentos",
                column: "comanda_id");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_forma",
                table: "pagamentos",
                column: "forma");

            migrationBuilder.CreateIndex(
                name: "ix_produtos_ativo_ordem",
                table: "produtos",
                columns: new[] { "ativo", "ordem" });

            migrationBuilder.CreateIndex(
                name: "ix_produtos_nome_normalizado",
                table: "produtos",
                column: "nome_normalizado",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_login",
                table: "usuarios",
                column: "login",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "itens_comanda");

            migrationBuilder.DropTable(
                name: "movimentos_caixa");

            migrationBuilder.DropTable(
                name: "pagamentos");

            migrationBuilder.DropTable(
                name: "produtos");

            migrationBuilder.DropTable(
                name: "comandas");

            migrationBuilder.DropTable(
                name: "caixas");

            migrationBuilder.DropTable(
                name: "usuarios");
        }
    }
}
