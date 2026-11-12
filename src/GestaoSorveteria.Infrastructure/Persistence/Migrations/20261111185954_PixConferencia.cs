using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PixConferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pagamentos_pix_txid",
                table: "pagamentos");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "cobrancas_pix",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_pix_txid",
                table: "pagamentos",
                column: "pix_txid",
                unique: true,
                filter: "pix_txid IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pagamentos_pix_txid",
                table: "pagamentos");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "cobrancas_pix");

            migrationBuilder.CreateIndex(
                name: "ix_pagamentos_pix_txid",
                table: "pagamentos",
                column: "pix_txid");
        }
    }
}
