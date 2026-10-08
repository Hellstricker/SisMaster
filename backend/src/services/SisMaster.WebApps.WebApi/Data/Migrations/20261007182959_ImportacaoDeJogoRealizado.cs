using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class ImportacaoDeJogoRealizado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoExterno",
                table: "Sumulas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DadosExternosJson",
                table: "Sumulas",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ImportadaEm",
                table: "Sumulas",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sumulas_CodigoExterno",
                table: "Sumulas",
                column: "CodigoExterno",
                unique: true,
                filter: "[CodigoExterno] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sumulas_CodigoExterno",
                table: "Sumulas");

            migrationBuilder.DropColumn(
                name: "CodigoExterno",
                table: "Sumulas");

            migrationBuilder.DropColumn(
                name: "DadosExternosJson",
                table: "Sumulas");

            migrationBuilder.DropColumn(
                name: "ImportadaEm",
                table: "Sumulas");
        }
    }
}
