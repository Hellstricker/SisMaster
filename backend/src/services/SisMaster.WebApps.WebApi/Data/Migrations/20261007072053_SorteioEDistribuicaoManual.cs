using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SorteioEDistribuicaoManual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DistribuicaoManual",
                table: "Fases",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SorteiosDeDesempate",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GrupoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Ordem = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DefinidoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SorteiosDeDesempate", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SorteiosDeDesempate_FaseId_GrupoId",
                table: "SorteiosDeDesempate",
                columns: new[] { "FaseId", "GrupoId" },
                unique: true,
                filter: "[GrupoId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SorteiosDeDesempate");

            migrationBuilder.DropColumn(
                name: "DistribuicaoManual",
                table: "Fases");
        }
    }
}
