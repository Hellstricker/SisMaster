using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class DescontosPorCombinacaoDeCategorias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DescontosMultiCategoria");

            migrationBuilder.CreateTable(
                name: "DescontosPorCombinacao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Combinacao = table.Column<string>(type: "nvarchar(800)", maxLength: 800, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescontosPorCombinacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DescontosPorCombinacao_Temporadas_TemporadaId",
                        column: x => x.TemporadaId,
                        principalTable: "Temporadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DescontosPorCombinacao_TemporadaId_Combinacao",
                table: "DescontosPorCombinacao",
                columns: new[] { "TemporadaId", "Combinacao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DescontosPorCombinacao");

            migrationBuilder.CreateTable(
                name: "DescontosMultiCategoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantidadeCategorias = table.Column<int>(type: "int", nullable: false),
                    TemporadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DescontosMultiCategoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DescontosMultiCategoria_Temporadas_TemporadaId",
                        column: x => x.TemporadaId,
                        principalTable: "Temporadas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DescontosMultiCategoria_TemporadaId_QuantidadeCategorias",
                table: "DescontosMultiCategoria",
                columns: new[] { "TemporadaId", "QuantidadeCategorias" },
                unique: true);
        }
    }
}
