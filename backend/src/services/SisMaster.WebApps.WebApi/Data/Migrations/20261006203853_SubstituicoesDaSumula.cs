using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubstituicoesDaSumula : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubstituicoesSumula",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SumulaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    JogadorSaiId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JogadorEntraId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Periodo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TempoJogoSegundos = table.Column<int>(type: "int", nullable: false),
                    NoInicio = table.Column<bool>(type: "bit", nullable: false),
                    RegistradoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubstituicoesSumula", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubstituicoesSumula_Sumulas_SumulaId",
                        column: x => x.SumulaId,
                        principalTable: "Sumulas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubstituicoesSumula_SumulaId_Ordem",
                table: "SubstituicoesSumula",
                columns: new[] { "SumulaId", "Ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubstituicoesSumula");
        }
    }
}
