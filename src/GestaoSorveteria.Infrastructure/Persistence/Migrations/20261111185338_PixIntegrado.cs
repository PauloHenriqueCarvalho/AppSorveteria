using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PixIntegrado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pix_txid",
                table: "pagamentos",
                type: "character varying(35)",
                maxLength: 35,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cobrancas_pix",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    txid = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pix_copia_ecola = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    criada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expira_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paga_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_to_end_id = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    valor_pago = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cobrancas_pix", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_pix_txid",
                table: "pagamentos",
                column: "pix_txid");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancas_pix_criada_em",
                table: "cobrancas_pix",
                column: "criada_em");

            migrationBuilder.CreateIndex(
                name: "ix_cobrancas_pix_txid",
                table: "cobrancas_pix",
                column: "txid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cobrancas_pix");

            migrationBuilder.DropIndex(
                name: "ix_pagamentos_pix_txid",
                table: "pagamentos");

            migrationBuilder.DropColumn(
                name: "pix_txid",
                table: "pagamentos");
        }
    }
}
