using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarFases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CadastroFasesEncerradoEm",
                table: "Temporadas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TabelaJogosGeradaEm",
                table: "Temporadas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Fases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemporadaCategoriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NumeroTurnos = table.Column<int>(type: "int", nullable: true),
                    JogosPorConfronto = table.Column<int>(type: "int", nullable: true),
                    FaseAnteriorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fases_Fases_FaseAnteriorId",
                        column: x => x.FaseAnteriorId,
                        principalTable: "Fases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Fases_TemporadaCategorias_TemporadaCategoriaId",
                        column: x => x.TemporadaCategoriaId,
                        principalTable: "TemporadaCategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fases_FaseAnteriorId",
                table: "Fases",
                column: "FaseAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_Fases_TemporadaCategoriaId_Ordem",
                table: "Fases",
                columns: new[] { "TemporadaCategoriaId", "Ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Fases");

            migrationBuilder.DropColumn(
                name: "CadastroFasesEncerradoEm",
                table: "Temporadas");

            migrationBuilder.DropColumn(
                name: "TabelaJogosGeradaEm",
                table: "Temporadas");
        }
    }
}
