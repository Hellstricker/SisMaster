using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SisMaster.WebApps.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class EstruturaEClassificacaoDaFase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassificadosPrimeiros",
                table: "Fases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Distribuicao",
                table: "Fases",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MelhoresExtras",
                table: "Fases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumeroConfrontos",
                table: "Fases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroGrupos",
                table: "Fases",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassificadosPrimeiros",
                table: "Fases");

            migrationBuilder.DropColumn(
                name: "Distribuicao",
                table: "Fases");

            migrationBuilder.DropColumn(
                name: "MelhoresExtras",
                table: "Fases");

            migrationBuilder.DropColumn(
                name: "NumeroConfrontos",
                table: "Fases");

            migrationBuilder.DropColumn(
                name: "NumeroGrupos",
                table: "Fases");
        }
    }
}
