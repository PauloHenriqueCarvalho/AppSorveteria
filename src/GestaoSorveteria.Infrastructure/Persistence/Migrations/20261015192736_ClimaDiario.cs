using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestaoSorveteria.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClimaDiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "latitude",
                table: "configuracao_loja",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "local_nome",
                table: "configuracao_loja",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "longitude",
                table: "configuracao_loja",
                type: "numeric(7,4)",
                precision: 7,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "clima_dias",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    temp_max = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    temp_min = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    temp_media = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    sensacao_max = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    chuva_mm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    chuva_horas_pct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    nuvens_pct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    umidade_pct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    icone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    condicao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    origem = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    definitivo = table.Column<bool>(type: "boolean", nullable: false),
                    fonte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    coletado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clima_dias", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "previsoes_clima",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    feita_no_dia = table.Column<DateOnly>(type: "date", nullable: false),
                    feita_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data = table.Column<DateOnly>(type: "date", nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    longitude = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    temp_max = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    temp_min = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: false),
                    chuva_mm = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    chance_chuva_pct = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    icone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    condicao = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    fonte = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_previsoes_clima", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_clima_dias_por_ponto_e_dia",
                table: "clima_dias",
                columns: new[] { "latitude", "longitude", "data" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_previsoes_clima_latitude_longitude_data",
                table: "previsoes_clima",
                columns: new[] { "latitude", "longitude", "data" });

            migrationBuilder.CreateIndex(
                name: "ux_previsoes_clima_por_ponto_feita_e_dia",
                table: "previsoes_clima",
                columns: new[] { "latitude", "longitude", "feita_no_dia", "data" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clima_dias");

            migrationBuilder.DropTable(
                name: "previsoes_clima");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "configuracao_loja");

            migrationBuilder.DropColumn(
                name: "local_nome",
                table: "configuracao_loja");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "configuracao_loja");
        }
    }
}
