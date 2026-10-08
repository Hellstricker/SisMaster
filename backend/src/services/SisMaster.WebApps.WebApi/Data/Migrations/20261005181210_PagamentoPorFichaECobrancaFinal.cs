using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class PagamentoPorFichaECobrancaFinal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataPagamento",
                table: "InscricoesCategorias");

            migrationBuilder.DropColumn(
                name: "Valor",
                table: "InscricoesCategorias");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaInscricao",
                table: "Temporadas",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CobrancaGeradaEm",
                table: "Inscricoes",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorDesconto",
                table: "Inscricoes",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorTotal",
                table: "Inscricoes",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DescontosMultiCategoria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantidadeCategorias = table.Column<int>(type: "int", nullable: false),
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

            migrationBuilder.CreateTable(
                name: "PagamentosInscricao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InscricaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Valor = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Data = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PagamentosInscricao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PagamentosInscricao_Inscricoes_InscricaoId",
                        column: x => x.InscricaoId,
                        principalTable: "Inscricoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DescontosMultiCategoria_TemporadaId_QuantidadeCategorias",
                table: "DescontosMultiCategoria",
                columns: new[] { "TemporadaId", "QuantidadeCategorias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PagamentosInscricao_InscricaoId",
                table: "PagamentosInscricao",
                column: "InscricaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DescontosMultiCategoria");

            migrationBuilder.DropTable(
                name: "PagamentosInscricao");

            migrationBuilder.DropColumn(
                name: "TaxaInscricao",
                table: "Temporadas");

            migrationBuilder.DropColumn(
                name: "CobrancaGeradaEm",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "ValorDesconto",
                table: "Inscricoes");

            migrationBuilder.DropColumn(
                name: "ValorTotal",
                table: "Inscricoes");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataPagamento",
                table: "InscricoesCategorias",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Valor",
                table: "InscricoesCategorias",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }
    }
}
