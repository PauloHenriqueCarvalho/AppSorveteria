using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ComandaEstorno : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "estornada_em",
                table: "comandas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "estornada_por_usuario_id",
                table: "comandas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_estorno",
                table: "comandas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "comandas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "ix_comandas_estornada_por_usuario_id",
                table: "comandas",
                column: "estornada_por_usuario_id");

            migrationBuilder.AddForeignKey(
                name: "fk_comandas_usuarios_estornada_por_usuario_id",
                table: "comandas",
                column: "estornada_por_usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_comandas_usuarios_estornada_por_usuario_id",
                table: "comandas");

            migrationBuilder.DropIndex(
                name: "ix_comandas_estornada_por_usuario_id",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "estornada_em",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "estornada_por_usuario_id",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "motivo_estorno",
                table: "comandas");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "comandas");
        }
    }
}
